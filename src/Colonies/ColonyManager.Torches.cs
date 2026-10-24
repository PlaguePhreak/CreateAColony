using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace CreateAColony.Colonies;

/// <summary>
/// Lights in buildings (Aaron, 0.109.0). Lanterns take metal, so when the colony has none a builder puts up a torch
/// where the blueprint has a lantern. Torches burn out, so couriers bring a lit torch from the warehouse to every
/// torch in a finished building that has gone out or burned out, and put it up in its place.
/// </summary>
public partial class ColonyManager
{
    /// <summary>The colony has a lantern somewhere a builder can get one, checked at most every few seconds.</summary>
    readonly ConditionalWeakTable<Colony, StrongBox<(long At, bool Has)>> lanternStock = new();
    /// <summary>Torches in finished buildings that have gone out, found at most every TorchCheckSeconds.</summary>
    readonly ConditionalWeakTable<Colony, StrongBox<(long At, List<BlockPos> Out)>> torchesOut = new();
    /// <summary>The steps of a blueprint that put up a light (a torch or a lantern).</summary>
    readonly ConditionalWeakTable<Blueprint, List<BuildStep>> lightSteps = new();

    static readonly string[] TorchFaces = { "up", "north", "east", "south", "west" };

    /// <summary>A vanilla lantern block, any size, standing, hanging or on a wall.</summary>
    public static bool IsLantern(Block? block) => block?.Code?.Domain == "game" && block.Code.Path.StartsWith("lantern-");

    bool ColonyHasLantern(Colony colony)
    {
        long now = sapi.World.ElapsedMilliseconds;
        var box = lanternStock.GetOrCreateValue(colony);
        if (box.Value.At > 0 && now - box.Value.At < 10_000) return box.Value.Has;
        static bool IsLanternStack(ItemStack s) => IsLantern(s.Block);
        bool has = CountItems(WarehouseContainers(colony), IsLanternStack) > 0
            || colony.Buildings.Where(b => b.Type == BuildingTypes.Builder).Any(b => CountItems(HutStorage(colony, b), IsLanternStack) > 0)
            || colony.Citizens.Any(c => c.Alive && c.JobCode == Jobs.Builder && PackCount(c, IsLanternStack) > 0);
        box.Value = (now, has);
        return has;
    }

    /// <summary>
    /// A lit torch to put up instead of the blueprint's lantern when the colony has no lantern: on the same wall, or
    /// on the floor or another wall when the lantern hangs from the ceiling. Null to keep the lantern: the colony has
    /// one, or a torch would not hold anywhere there (yet).
    /// </summary>
    Block? TorchForLantern(Colony colony, Block lantern, BlockPos pos)
    {
        if (!Config.LanternTorchFallback || !IsLantern(lantern) || ColonyHasLantern(colony)) return null;
        var ba = sapi.World.BlockAccessor;
        string own = lantern.Variant.TryGetValue("orientation", out var o) ? o : lantern.LastCodePart();
        foreach (string face in TorchFaces.OrderBy(f => f == own ? 0 : 1))
        {
            if (sapi.World.GetBlock(new AssetLocation("game", "torch-basic-lit-" + face)) is not { } torch) continue;
            var facing = BlockFacing.FromCode(face);
            var on = pos.AddCopy(facing.Opposite);
            if (ba.GetBlock(on).CanAttachBlockAt(ba, torch, on, facing)) return torch;
        }
        return null;
    }

    /// <summary>What a torch put up in place of a lantern costs: one torch that is lit or has only gone out.</summary>
    MaterialCost? TorchCost(Block torch)
    {
        if (!Config.BuildersNeedMaterials) return null;
        var up = sapi.World.GetBlock(torch.CodeWithVariant("orientation", "up")) ?? torch;
        return new MaterialCost("game:torch-*-lit-up|game:torch-*-extinct-up", new ItemStack(up), 1);
    }

    // ---- Torches that went out --------------------------------------------------------------

    /// <summary>A placed torch that gives no light any more: gone out or burned out.</summary>
    static bool IsOutTorch(Block block) => IsTorch(block) && !block.Code.Path.Contains("-lit-");

    List<BuildStep> LightSteps(Blueprint blueprint) => lightSteps.GetValue(blueprint, b => b.Steps
        .Where(s => sapi.World.GetBlock(s.BlockId) is { } block && (IsTorch(block) || IsLantern(block))).ToList());

    /// <summary>Torches in the colony's finished buildings that have gone out, nearest the warehouse first.</summary>
    List<BlockPos> TorchesOut(Colony colony)
    {
        long now = sapi.World.ElapsedMilliseconds;
        var box = torchesOut.GetOrCreateValue(colony);
        if (box.Value.Out != null && now - box.Value.At < Config.TorchCheckSeconds * 1000) return box.Value.Out;
        var found = new List<BlockPos>();
        var ba = sapi.World.BlockAccessor;
        foreach (var building in colony.Buildings.Where(b => b.Level > 0 && !b.HasWorkOrder && !b.Removing))
        {
            if (Blueprints.Get(building, building.Level) is not { } blueprint) continue;
            foreach (var step in LightSteps(blueprint))
            {
                var pos = StepPos(building, step);
                if (ba.GetChunkAtBlockPos(pos) != null && IsOutTorch(ba.GetBlock(pos)) && CanWorkAt(colony, pos)) found.Add(pos);
            }
        }
        if (colony.WarehousePos is { } w) found = found.OrderBy(p => p.DistanceSqTo(w.X, w.Y, w.Z)).ToList();
        box.Value = (now, found);
        return found;
    }

    /// <summary>A torch that went out and a warehouse chest with a lit torch for it, for a courier; null when there is none.</summary>
    public (BlockPos Torch, BlockEntityContainer Source)? FindTorchToRelight(Colony colony, ICollection<BlockPos> taken)
    {
        if (!Config.RelightTorches) return null;
        var spot = TorchesOut(colony).FirstOrDefault(p => !taken.Contains(p) && IsOutTorch(sapi.World.BlockAccessor.GetBlock(p)));
        if (spot == null) return null;
        var source = WarehouseContainers(colony).FirstOrDefault(c => c.Inventory.Any(s => IsTorch(s.Itemstack)));
        return source == null ? null : (spot, source);
    }

    /// <summary>Puts a lit torch from the settler's inventory up where a torch went out. False when there is nothing to do or no torch.</summary>
    public bool RelightTorch(Colony colony, CitizenRecord citizen, BlockPos pos)
    {
        var ba = sapi.World.BlockAccessor;
        var old = ba.GetBlock(pos);
        if (!IsOutTorch(old) || ba.GetBlock(LitTorch(old.Code)) is not { } lit) return false;
        if (TakeFromPack(colony, citizen, IsTorch, 1) == null) return false;
        ba.SetBlock(lit.Id, pos);
        ba.MarkBlockDirty(pos);
        if (lit.Sounds?.Place != null) sapi.World.PlaySoundAt(lit.Sounds.Place, pos, 0);
        return true;
    }
}
