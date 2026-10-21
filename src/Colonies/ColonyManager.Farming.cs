using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using ItemMatch = System.Func<Vintagestory.API.Common.ItemStack, bool>;

namespace CreateAColony.Colonies;

/// <summary>What a farmer has to do, most urgent first.</summary>
public enum FieldWork { None, Harvest, Pick, Weed, Water, Sow, Till, Refill }

/// <summary>
/// The farmer's trade. A Field marker marks out a square of ground around it; on that ground the farmer
/// tills soil into farmland with a hoe, sows seeds, pulls weeds and harvests ripe crops, the same way a
/// player does, so the crops grow by the game's own rules (water, soil nutrients, seasons). On fields
/// where any crop will do it sows the crop that suits the soil best, which rotates crops by itself.
/// </summary>
public partial class ColonyManager
{
    /// <summary>Asked for when a farmer runs out and the field has no crop chosen.</summary>
    const string AnySeed = "game:seeds-*";
    const int SeedRequestSize = 16;

    readonly Dictionary<string, Block?> farmlandFor = new();

    /// <summary>A seed a farmer can sow: it grows into a crop on farmland.</summary>
    public bool IsSowable(ItemStack stack) => stack.Collectible is ItemPlantableSeed && CropFor(stack) != null;

    /// <summary>Any crop seed, sowable or not, for keeping a stock of.</summary>
    public static bool IsCropSeed(ItemStack stack) => stack.Collectible is ItemPlantableSeed;

    /// <summary>The first growth stage of the crop a seed grows into, or null (bell pepper seeds are sown differently).</summary>
    public Block? CropFor(ItemStack seed)
    {
        string? type = VariantOf(seed.Collectible, "type");
        if (type == null || type == "bellpepper") return null;
        var crop = sapi.World.GetBlock(seed.Collectible.CodeWithPath("crop-" + type + "-1"));
        return crop?.CropProps == null ? null : crop;
    }

    // ---- Fields ---------------------------------------------------------------------------

    public FieldRecord? FieldAt(Colony colony, BlockPos pos) => colony.Fields.Find(f => f.Pos.Equals(pos));

    public FieldRecord AddField(Colony colony, BlockPos pos)
    {
        var field = FieldAt(colony, pos);
        if (field != null) return field;
        field = new FieldRecord { Pos = pos.Copy() };
        colony.Fields.Add(field);
        MarkDirty(colony);
        return field;
    }

    public void RemoveField(Colony colony, BlockPos pos)
    {
        if (colony.Fields.RemoveAll(f => f.Pos.Equals(pos)) > 0) MarkDirty(colony);
    }

    /// <summary>
    /// The fields this farmer looks after: the ones it already has, topped up with fields nobody farms,
    /// nearest to its hut first, up to FieldsPerFarmer.
    /// </summary>
    public List<FieldRecord> FieldsFor(Colony colony, CitizenRecord farmer, Building hut)
    {
        var mine = colony.Fields.Where(f => f.FarmerCitizenId == farmer.CitizenId).ToList();
        var h = hut.HutPos;
        foreach (var field in colony.Fields.Where(f => f.FarmerCitizenId != farmer.CitizenId).OrderBy(f => f.Pos.DistanceSqTo(h.X, h.Y, h.Z)))
        {
            if (mine.Count >= Config.FieldsPerFarmer) break;
            var owner = colony.Citizens.Find(c => c.CitizenId == field.FarmerCitizenId);
            if (owner != null && owner.Alive && owner.JobCode == Jobs.Farmer) continue;
            field.FarmerCitizenId = farmer.CitizenId;
            mine.Add(field);
            MarkDirty(colony);
        }
        return mine;
    }

    /// <summary>The ground blocks of a field: the top of each column around the marker, except the marker's own.</summary>
    public IEnumerable<BlockPos> FieldGround(FieldRecord field)
    {
        int r = System.Math.Max(1, Config.FieldRadius);
        for (int dx = -r; dx <= r; dx++)
        for (int dz = -r; dz <= r; dz++)
        {
            if (dx == 0 && dz == 0) continue;
            if (GroundAt(field.Pos.X + dx, field.Pos.Y, field.Pos.Z + dz, field.Pos) is { } ground) yield return ground;
        }
    }

    /// <summary>
    /// The farmable block in a column near the marker's height: the highest farmland, or soil that could
    /// be tilled, with open air (or grass, or a crop) above it.
    /// </summary>
    BlockPos? GroundAt(int x, int markerY, int z, BlockPos like)
    {
        var ba = sapi.World.BlockAccessor;
        for (int dy = 0; dy >= -2; dy--)
        {
            var pos = like.Copy();
            pos.Set(x, markerY + dy, z);
            var above = ba.GetBlock(pos.UpCopy());
            if (!Clearable(above) && above is not BlockCrop && above.Code?.Path.StartsWith("deadcrop") != true) continue;
            if (ba.GetBlockEntity(pos) is BlockEntityFarmland || Tillable(ba.GetBlock(pos)) != null) return pos;
        }
        return null;
    }

    /// <summary>What the field needs done at this ground block.</summary>
    public FieldWork WorkAt(Colony colony, BlockPos ground)
    {
        if (!CanWorkAt(colony, ground)) return FieldWork.None;
        var ba = sapi.World.BlockAccessor;
        var above = ba.GetBlock(ground.UpCopy());
        if (ba.GetBlockEntity(ground) is BlockEntityFarmland farmland)
        {
            if (farmland.HasRipeCrop() || above.Code?.Path.StartsWith("deadcrop") == true) return FieldWork.Harvest;
            if (above.Id != 0 && Clearable(above)) return FieldWork.Weed;
            if (farmland.CanPlant()) return FieldWork.Sow;
            if (farmland.GetCrop() != null && farmland.MoistureLevel < Config.FarmerWaterBelow) return FieldWork.Water;
            return FieldWork.None;
        }
        if (Tillable(ba.GetBlock(ground)) != null && Clearable(above)) return FieldWork.Till;
        return FieldWork.None;
    }

    /// <summary>The farmland soil becomes when tilled, or null if it cannot be tilled.</summary>
    Block? Tillable(Block block)
    {
        if (block.Code == null || !block.Code.Path.StartsWith("soil-")) return null;
        string fertility = block.LastCodePart(1);
        string key = block.Code.Domain + ":" + fertility;
        if (!farmlandFor.TryGetValue(key, out var farmland))
        {
            farmland = sapi.World.GetBlock(new AssetLocation(block.Code.Domain, "farmland-dry-" + fertility));
            farmlandFor[key] = farmland;
        }
        return farmland;
    }

    /// <summary>Air, or grass and flowers a farmer pulls up before tilling. Saplings and crops stay.</summary>
    static bool Clearable(Block above) =>
        above.Id == 0
        || (above.BlockMaterial == EnumBlockMaterial.Plant && above.Replaceable >= 5000 && above is not BlockCrop
            && above.Code?.Path.StartsWith("sapling") != true);

    // ---- Working the ground ---------------------------------------------------------------

    /// <summary>
    /// Tills soil into farmland with the hoe in the worker's hand, keeping the soil's nutrients, and wears
    /// the hoe down. Grass in the way is pulled up first and kept.
    /// </summary>
    public bool Till(Colony colony, Building hut, CitizenRecord worker, EntityAgent entity, BlockPos ground, ItemSlot hoe)
    {
        var ba = sapi.World.BlockAccessor;
        var soil = ba.GetBlock(ground);
        var farmland = Tillable(soil);
        var abovePos = ground.UpCopy();
        var above = ba.GetBlock(abovePos);
        if (farmland == null || !Clearable(above) || hoe.Empty) return false;

        if (above.Id != 0)
        {
            StoreHarvest(colony, hut, worker, DropsOf(above, abovePos), abovePos);
            BreakQuietly(abovePos);
        }

        TreeAttribute? nutrients = null;
        if (ba.GetBlockEntity(ground) is BlockEntitySoilNutrition old)
        {
            nutrients = new TreeAttribute();
            old.ToTreeAttributes(nutrients);
        }
        ba.SetBlock(farmland.BlockId, ground);
        if (ba.GetBlockEntity(ground) is BlockEntityFarmland created) created.OnCreatedFromSoil(soil, nutrients);
        ba.MarkBlockDirty(ground);
        if (soil.Sounds?.Place != null) sapi.World.PlaySoundAt(soil.Sounds.Place, ground, 0.4);
        WearTool(entity, hoe);
        return true;
    }

    /// <summary>
    /// Sows a seed from the worker's inventory on farmland. A field with a chosen crop gets that crop;
    /// otherwise the farmer picks the seed whose crop needs the nutrient the soil has most of, so a field
    /// that a crop wore down gets a different crop next. Poor soil gets fertilizer first if the worker has some.
    /// </summary>
    public bool Sow(Colony colony, CitizenRecord worker, EntityAgent entity, BlockPos ground, FieldRecord field, string? lastCrop)
    {
        if (sapi.World.BlockAccessor.GetBlockEntity(ground) is not BlockEntityFarmland farmland || !farmland.CanPlant()) return false;

        var allowed = SeedMatcher(field);
        var best = PackItems(worker).Where(allowed)
            .OrderByDescending(s => CropFor(s) is { } c ? farmland.Nutrients[(int)c.CropProps.RequiredNutrient] : -1)
            .ThenByDescending(s => VariantOf(s.Collectible, "type") == lastCrop)
            .FirstOrDefault();
        if (best == null) return false;
        var seed = TakeFromPack(colony, worker, s => s.Equals(sapi.World, best, GlobalConstants.IgnoredStackAttributes), 1);
        if (seed == null) return false;

        var crop = CropFor(seed);
        if (crop != null) Fertilize(colony, worker, farmland, crop.CropProps.RequiredNutrient);
        var sel = new BlockSelection { Position = ground.Copy(), Face = BlockFacing.UP, HitPosition = new Vec3d(0.5, 1, 0.5) };
        if (crop == null || !farmland.TryPlant(crop, new DummySlot(seed.Clone()), entity, sel))
        {
            AddToPack(colony, worker, seed);
            return false;
        }
        sapi.World.PlaySoundAt(new AssetLocation("sounds/block/plant"), ground, 0.4375);
        return true;
    }

    /// <summary>Pulls up weeds (or any grass) growing on farmland; they go into the worker's inventory.</summary>
    public bool Weed(Colony colony, Building hut, CitizenRecord worker, BlockPos ground)
    {
        var pos = ground.UpCopy();
        var weed = sapi.World.BlockAccessor.GetBlock(pos);
        if (weed.Id == 0 || !Clearable(weed)) return false;
        StoreHarvest(colony, hut, worker, DropsOf(weed, pos), pos);
        BreakQuietly(pos);
        return true;
    }

    /// <summary>Harvests the ripe (or dead) crop on the farmland. Returns the crop's type, so the same can be sown again.</summary>
    public string? Harvest(Colony colony, Building hut, CitizenRecord worker, BlockPos ground)
    {
        var ba = sapi.World.BlockAccessor;
        var cropPos = ground.UpCopy();
        var crop = ba.GetBlock(cropPos);
        if (crop.Id == 0) return null;
        StoreHarvest(colony, hut, worker, DropsOf(crop, cropPos), cropPos);
        BreakQuietly(cropPos);
        if (crop.Sounds?.Break != null) sapi.World.PlaySoundAt(crop.Sounds.Break, cropPos, 0);
        return crop is BlockCrop ? VariantOf(crop, "type") : null;
    }

    /// <summary>The seeds that may be sown on a field.</summary>
    public ItemMatch SeedMatcher(FieldRecord field)
    {
        if (field.Seed.Length == 0) return IsSowable;
        var only = Matcher(field.Seed);
        return s => only(s) && IsSowable(s);
    }

    /// <summary>Asks couriers to bring seeds for a field to the farmer's hut, once at a time.</summary>
    public bool EnsureSeedRequest(Colony colony, Building hut, FieldRecord field)
    {
        var chest = HutStorage(colony, hut).FirstOrDefault();
        if (chest == null) return false;
        string code = field.Seed.Length > 0 ? field.Seed : AnySeed;
        string tag = RequestTag(hut);
        if (!colony.Requests.Any(r => r.RequestedBy == tag && r.ItemCode == code)) AddRequest(colony, code, SeedRequestSize, chest.Pos, tag);
        return true;
    }
}
