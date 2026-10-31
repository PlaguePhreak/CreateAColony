using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace CreateAColony.Colonies;

/// <summary>
/// Farmers swap poor soil on their fields for better soil (Aaron, 0.155.0). When the Farmer's Hut storage holds
/// soil of a higher grade (very low, low, medium, compost, high, as in the game) than a bare spot on a field, the
/// farmer digs the poor soil out, keeps it, and lays the better soil down as fresh farmland with that soil's
/// nutrients, the way a player would break the farmland, place new soil and till it. The worst spots go first,
/// and only spots with nothing growing on them. Better soil that is only in the warehouse is asked for from couriers.
/// </summary>
public partial class ColonyManager
{
    /// <summary>How much soil a farmer asks for or picks up at once.</summary>
    const int SoilBatch = 16;

    /// <summary>The best soil grade there is (high, terra preta).</summary>
    public static int TopSoilRank => BlockEntitySoilNutrition.Fertilities.Count - 1;

    /// <summary>The soil grade a block or farmland stands for: 0 very low up to 4 high; -1 for anything else.</summary>
    public static int SoilRank(Block? block)
    {
        string? path = block?.Code?.Path;
        if (path == null) return -1;
        string? fertility = path.StartsWith("soil-") ? block!.LastCodePart(1) : path.StartsWith("farmland-") ? block!.LastCodePart() : null;
        return fertility == null ? -1 : BlockEntitySoilNutrition.Fertilities.IndexOfKey(fertility);
    }

    /// <summary>The grade of a soil block in an inventory that can be tilled; -1 for anything else.</summary>
    public int SoilRank(ItemStack? stack) => stack?.Block is { } b && b.Code.Path.StartsWith("soil-") && Tillable(b) != null ? SoilRank(b) : -1;

    /// <summary>The grade of the ground a farmer works: farmland, or soil it will till.</summary>
    public int GroundSoilRank(BlockPos ground) => SoilRank(sapi.World.BlockAccessor.GetBlock(ground));

    /// <summary>The best soil grade among these stacks, or -1.</summary>
    public int BestSoilRank(IEnumerable<ItemStack> stacks) => stacks.Select(SoilRank).DefaultIfEmpty(-1).Max();

    /// <summary>
    /// Replaces the bare ground with the best soil the worker carries, if that is better: the old soil goes into
    /// the worker's inventory, and farmland stays farmland (fresh, with the new soil's nutrients). True if it did.
    /// </summary>
    public bool UpgradeSoil(Colony colony, Building hut, CitizenRecord worker, BlockPos ground)
    {
        var ba = sapi.World.BlockAccessor;
        var old = ba.GetBlock(ground);
        int rank = SoilRank(old);
        if (rank < 0 || ba.GetBlock(ground.UpCopy()) is BlockCrop) return false;
        var best = PackItems(worker).Where(s => SoilRank(s) > rank).OrderByDescending(SoilRank).FirstOrDefault();
        if (best == null) return false;
        var soil = TakeFromPack(colony, worker, s => s.Collectible.Code.Equals(best.Collectible.Code), 1);
        if (soil?.Block == null) return false;

        bool wasFarmland = ba.GetBlockEntity(ground) is BlockEntityFarmland;
        Block? place = wasFarmland ? Tillable(soil.Block) : soil.Block;
        if (place == null)
        {
            AddToPack(colony, worker, soil);
            return false;
        }
        // The old soil comes out first, as it would for a player breaking it.
        StoreHarvest(colony, hut, worker, DropsOf(old, ground), ground);
        ba.SetBlock(place.BlockId, ground);
        if (wasFarmland && ba.GetBlockEntity(ground) is BlockEntityFarmland created) created.OnCreatedFromSoil(soil.Block);
        ba.MarkBlockDirty(ground);
        if (soil.Block.Sounds?.Place != null) sapi.World.PlaySoundAt(soil.Block.Sounds.Place, ground, 0.4);
        AddStat(hut, "soil-upgraded");
        return true;
    }

    /// <summary>Asks couriers to bring better soil than this grade from the warehouse to the hut, once at a time.</summary>
    public void EnsureSoilRequest(Colony colony, Building hut, int worse, int count)
    {
        var chest = HutStorage(colony, hut).FirstOrDefault();
        if (chest == null) return;
        string tag = RequestTag(hut);
        if (colony.Requests.Any(r => r.RequestedBy == tag && r.ItemCode.Contains(":soil-"))) return;
        if (CodeInWarehouse(colony, s => SoilRank(s) > worse) is not { } code) return;
        AddRequest(colony, code, System.Math.Clamp(count, 1, SoilBatch), chest.Pos, tag);
    }
}
