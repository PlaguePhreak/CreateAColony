using System.Collections.Generic;
using System.Linq;
using CreateAColony.Colonies;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;
using ItemMatch = System.Func<Vintagestory.API.Common.ItemStack, bool>;

namespace CreateAColony.Settlers;

/// <summary>
/// An armourer works from an Armourer's Hut with the forge and anvil, like a blacksmith, but makes the chains,
/// scales, plates and lamellae armor is made of, and puts metal armor together on the Crafting recipes tab.
/// Between pieces it mends worn metal armor from its hut and the warehouse at the anvil, with one more part of
/// the same kind and metal (see ColonyManager.Armourer.cs).
/// </summary>
public class AiTaskSettlerArmourer : AiTaskSettlerBlacksmith
{
    bool mending;

    public AiTaskSettlerArmourer(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig) { }

    protected override string Job => Jobs.Armourer;
    protected override string HutType => BuildingTypes.Armourer;
    protected override bool WorkNeedsTool => mending || base.WorkNeedsTool;
    protected override float WorkSeconds => mending ? manager!.Config.BlacksmithSecondsPerStrike * 4 : base.WorkSeconds;

    /// <summary>It also carries the armor it is mending and what mends it.</summary>
    protected override bool AlsoKeeps(ItemStack stack)
    {
        if (base.AlsoKeeps(stack)) return true;
        var m = manager!;
        return m.ArmourerMendLeft(stack) > 0.01f || m.PackItems(Citizen!).Any(piece => m.ArmourerMendLeft(piece) > 0.01f && m.ArmourerMends(stack, piece));
    }

    protected override bool PlanWork(Colony colony, CitizenRecord citizen, Building hut, List<BlockEntityContainer> storage)
    {
        mending = false;
        // Never in the middle of a piece: the ingots would cool.
        var m = manager!;
        bool smithing = m.SmithForge(hut)?.WorkItemStack != null || m.PackItems(citizen).Any(s => ColonyManager.IsIngot(s) && ColonyManager.IsWorkable(s));
        if (!smithing && PlanMending(colony, citizen, hut, storage)) return true;
        return base.PlanWork(colony, citizen, hut, storage);
    }

    bool PlanMending(Colony colony, CitizenRecord citizen, Building hut, List<BlockEntityContainer> storage)
    {
        var m = manager!;
        var piece = m.PackItems(citizen).Where(s => m.ArmourerMendLeft(s) > 0.01f).OrderByDescending(m.ArmourerMendLeft).FirstOrDefault();
        if (piece != null)
        {
            ItemMatch mends = s => m.ArmourerMends(s, piece);
            if (m.PackCount(citizen, mends) > 0)
            {
                mending = true;
                SetStatus("armourer-mending");
                return GoWork(m.SmithAnvil(hut)?.Pos ?? hut.HutPos);
            }
            var chest = storage.FirstOrDefault(c => c.Inventory.Any(s => !s.Empty && mends(s.Itemstack)));
            if (chest != null && !m.PackFull(citizen))
            {
                SetStatus("armourer-fetching-parts");
                return FetchSupplies(chest.Pos, mends, 2);
            }
            if (m.CodeInWarehouse(colony, mends) is { } code) m.EnsureCraftRequest(colony, hut, code, 2);
            // Without the part it smiths other things meanwhile; the part may be one of them.
            return false;
        }
        if (m.PackFull(citizen)) return false;
        var worn = storage.Concat(m.WarehouseContainers(colony)).FirstOrDefault(c => c.Inventory.Any(s => !s.Empty && m.ArmourerNeedsMending(s.Itemstack)));
        if (worn == null) return false;
        SetStatus("armourer-fetching-worn");
        return FetchSupplies(worn.Pos, m.ArmourerNeedsMending, 1);
    }

    protected override void DoWork(Colony colony, CitizenRecord citizen, Building hut)
    {
        if (!mending)
        {
            base.DoWork(colony, citizen, hut);
            return;
        }
        var m = manager!;
        if (Hand is { } hand && HoldsTool) m.WearTool(entity, hand);
        entity.World.PlaySoundAt(new AssetLocation("sounds/effect/anvilhit"), target.X + 0.5, target.Y + 0.5, target.Z + 0.5, null, true, 16);
        m.ArmourerMendOnce(colony, hut, citizen);
    }

    protected override string DescribeWork()
    {
        if (!mending || manager is not { } m || Citizen is not { } c) return base.DescribeWork();
        var piece = m.PackItems(c).Where(s => m.ArmourerMendLeft(s) > 0.01f).OrderByDescending(m.ArmourerMendLeft).FirstOrDefault();
        return piece == null ? base.DescribeWork() : Lang.Get("createacolony:activity-armourer-mend", piece.GetName(), (int)System.Math.Round((1 - m.ArmourerMendLeft(piece)) * 100));
    }
}
