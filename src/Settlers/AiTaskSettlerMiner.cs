using System;
using System.Collections.Generic;
using System.Linq;
using CreateAColony.Colonies;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace CreateAColony.Settlers;

/// <summary>
/// A miner works from a Miner's Hut: it digs a shaft beside the hut with a pickaxe, level by level, and
/// digs out the ore it finds there, block by block, as a player would. With "Go to prospected ore" on, it
/// first goes to good ore the colony's prospectors found and digs that out. Rock and ore go into its
/// inventory and from there to the hut's storage.
/// </summary>
public class AiTaskSettlerMiner : AiTaskSettlerDigger
{
    ColonyManager.MineJob job;

    public AiTaskSettlerMiner(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig)
    {
        // The miner walks right up to its spot in the tunnel, and digs from there.
        workRange = 0.9f;
    }

    protected override string Job => Jobs.Miner;
    protected override string HutType => BuildingTypes.Miner;
    protected override float WorkSeconds => job.Kind == ColonyManager.MineWork.Travel ? 0.1f : job.Kind == ColonyManager.MineWork.Fill ? 0.75f : ToolSeconds(manager!.DigSeconds(target, manager.Config.MinerSecondsPerBlock));

    protected override bool PlanWork(Colony colony, CitizenRecord citizen, Building hut, List<BlockEntityContainer> storage)
    {
        if (FetchBetterPick(citizen, storage)) return true;
        if (FetchTorches(colony, citizen, hut, storage)) return true;
        filling = hut.MineSeals.Count > 0;
        bool fetching = false, canFill = filling && HasFill(colony, citizen, hut, storage, out fetching);
        if (fetching) return true;
        var at = new BlockPos((int)Math.Floor(entity.Pos.X), (int)Math.Round(entity.Pos.Y), (int)Math.Floor(entity.Pos.Z));
        var next = manager!.NextMineJob(colony, hut, at, IsUnreachable, canFill, out string status);
        SetStatus(status);
        if (next is not { } found) return false;
        job = found;
        return GoWork(found.Block);
    }

    /// <summary>True while a finished mine waits to be filled in, so the dirt and stone it carries stay with it.</summary>
    bool filling;

    /// <summary>The miner keeps its torches, too, and dirt and stone while it has a mine to fill in.</summary>
    protected override bool AlsoKeeps(ItemStack stack) =>
        base.AlsoKeeps(stack) || ColonyManager.IsTorchStack(stack) || filling && ColonyManager.IsFillStack(stack);

    /// <summary>
    /// True when the miner carries something to fill a finished mine with. Otherwise it walks to fetch dirt
    /// (or else stone) from the hut's storage (fetching set), or asks couriers for dirt and gets on with
    /// other work meanwhile.
    /// </summary>
    bool HasFill(Colony colony, CitizenRecord citizen, Building hut, List<BlockEntityContainer> storage, out bool fetching)
    {
        fetching = false;
        if (manager!.PackCount(citizen, ColonyManager.IsFillStack) > 0) return true;
        foreach (var match in new System.Func<ItemStack?, bool>[] { ColonyManager.IsSoilStack, ColonyManager.IsFillStack })
        {
            var chest = storage.FirstOrDefault(c => c.Inventory.Any(slot => match(slot.Itemstack)));
            if (chest == null) continue;
            fetching = FetchSupplies(chest.Pos, s => match(s), 32);
            return false;
        }
        manager.EnsureFillRequest(colony, hut);
        SetStatus("mine-fill-needs-dirt");
        return false;
    }

    /// <summary>
    /// Tops up the miner's torches from the hut's storage when it is down to its last two, and asks
    /// couriers for some when it has none and the storage has none either.
    /// </summary>
    bool FetchTorches(Colony colony, CitizenRecord citizen, Building hut, List<BlockEntityContainer> storage)
    {
        int want = manager!.Config.MinerTorchesCarried;
        if (manager.Config.MinerTorchLight <= 0 || want <= 0) return false;
        int have = manager.PackCount(citizen, ColonyManager.IsTorchStack);
        if (have > 2) return false;
        var chest = storage.FirstOrDefault(c => c.Inventory.Any(slot => ColonyManager.IsTorchStack(slot.Itemstack)));
        if (chest != null) return FetchSupplies(chest.Pos, ColonyManager.IsTorchStack, want - have);
        if (have == 0) manager.EnsureTorchRequest(colony, hut);
        return false;
    }

    protected override Vec3d WalkGoal() => job.Stand.ToVec3d().Add(0.5, 0, 0.5);

    protected override bool CloseEnough() => entity.Pos.XYZ.DistanceTo(WalkGoal()) <= 1.2;

    protected override bool WorkNeedsTool => job.Kind == ColonyManager.MineWork.Dig;

    protected override void DoWork(Colony colony, CitizenRecord citizen, Building hut)
    {
        if (job.Kind == ColonyManager.MineWork.Fill)
        {
            manager!.FillBlock(colony, citizen, target);
            return;
        }
        if (job.Kind != ColonyManager.MineWork.Dig) return;
        // Too hard for the pick: other ore is tried while a better pick is on its way.
        if (!Dig(colony, citizen, hut)) manager!.BlockOre(hut, hut.MineTarget ?? target);
        // Light the way where it has got dark.
        manager!.PlaceTorchIfDark(colony, citizen, job.Stand);
    }

    protected override string DescribeWork() => Lang.Get(job.Kind == ColonyManager.MineWork.Fill ? "createacolony:activity-miner-fill"
        : job.Kind == ColonyManager.MineWork.Travel ? "createacolony:activity-miner-travel"
        : job.Ore ? "createacolony:activity-miner-ore" : "createacolony:activity-miner-tunnel");
}
