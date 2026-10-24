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
/// A craftsman works from a Primitive Craftsman's hut: it knaps flint and stones into tool heads at the hut
/// (see ColonyManager.Knapping.cs), then puts each head on a stick to make the finished stone tool. Stones
/// and sticks come from the hut storage, which couriers fill from the warehouse; what it makes goes to the
/// hut storage and on to the warehouse. A head is only knapped when the last one has been hafted.
/// </summary>
public class AiTaskSettlerCraftsman : AiTaskSettlerWorker
{
    enum Work { Knap, Haft }

    Work work;
    ColonyManager.KnapKind? kind;
    int strikes;

    public AiTaskSettlerCraftsman(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig) { }

    protected override string Job => Jobs.Craftsman;
    protected override string HutType => BuildingTypes.Craftsman;
    protected override EnumTool Tool => EnumTool.Knife; // unused: knapping is done with a stone in hand
    protected override bool NeedsTool => false;
    protected override bool WorkNeedsTool => false;
    protected override ItemMatch Stock => _ => false;
    protected override float WorkSeconds => work == Work.Knap ? manager!.Config.CraftsmanSecondsPerStrike : manager!.Config.CraftsmanSecondsPerHaft;

    /// <summary>It carries the stone, the head and the handle for what it is making.</summary>
    protected override bool AlsoKeeps(ItemStack stack) =>
        kind != null && (kind.IsStone(stack) || kind.IsHandle(stack) || (kind.Assembly != null && kind.IsPiece(stack)));

    protected override string WorkAnimation(ItemSlot? hand) => "interactstatic";

    protected override bool PlanWork(Colony colony, CitizenRecord citizen, Building hut, List<BlockEntityContainer> storage)
    {
        var m = manager!;
        var wanted = m.WantedKnapping(colony, hut, citizen);
        if (kind != null && !wanted.Contains(kind))
        {
            kind = null;
            strikes = 0;
        }
        if (wanted.Count == 0)
        {
            SetStatus("craftsman-stocked");
            return false;
        }

        // The piece being knapped, while the stone for it is on hand.
        if (kind != null && strikes > 0 && m.PackCount(citizen, kind.IsStone) > 0) return Go(Work.Knap, hut, "craftsman-knapping");

        bool needsHandle = false;
        foreach (var want in wanted)
        {
            if (want.Assembly != null)
            {
                bool hasHead = m.PackCount(citizen, want.IsPiece) > 0;
                if (hasHead && m.HasHandles(citizen, want))
                {
                    kind = want;
                    return Go(Work.Haft, hut, "craftsman-hafting");
                }
                // The handle before the head, so heads do not pile up without sticks to finish them.
                if (FetchHandles(colony, citizen, hut, storage, want, out bool fetching))
                {
                    if (fetching)
                    {
                        kind = want;
                        return true;
                    }
                    needsHandle = true;
                    continue;
                }
                if (hasHead) continue;
                var headChest = storage.FirstOrDefault(c => c.Inventory.Any(s => !s.Empty && want.IsPiece(s.Itemstack)));
                if (headChest != null && !m.PackFull(citizen))
                {
                    kind = want;
                    SetStatus("craftsman-fetching-head");
                    return FetchSupplies(headChest.Pos, want.IsPiece, 1);
                }
            }

            if (m.PackCount(citizen, want.IsStone) > 0)
            {
                kind = want;
                strikes = 0;
                return Go(Work.Knap, hut, "craftsman-knapping");
            }
            int batch = System.Math.Clamp(m.CraftWant(colony, hut, want.Key) - m.KnapStock(colony, hut, want, citizen), 1, m.Config.CraftsmanBatch);
            var chest = storage.FirstOrDefault(c => c.Inventory.Any(s => !s.Empty && want.IsStone(s.Itemstack)));
            if (chest != null && !m.PackFull(citizen))
            {
                kind = want;
                SetStatus("craftsman-fetching-stone");
                return FetchSupplies(chest.Pos, want.IsStone, batch);
            }
            if (m.CodeInWarehouse(colony, want.IsStone) is { } code)
            {
                m.EnsureCraftRequest(colony, hut, code, batch);
                SetStatus("craftsman-waiting-stone");
                return false;
            }
        }
        SetStatus(needsHandle ? "craftsman-needs-handle" : "craftsman-needs-stone");
        return false;
    }

    /// <summary>
    /// Gets what the hafting takes that the craftsman lacks: from the hut storage, or by asking couriers.
    /// True when something is missing (fetching says whether it walks to get it).
    /// </summary>
    bool FetchHandles(Colony colony, CitizenRecord citizen, Building hut, List<BlockEntityContainer> storage, ColonyManager.KnapKind want, out bool fetching)
    {
        var m = manager!;
        fetching = false;
        bool missing = false;
        foreach (var (part, quantity) in want.Handles)
        {
            ItemMatch fits = s => part.SatisfiesAsIngredient(s, false);
            int short_ = quantity - m.PackCount(citizen, fits);
            if (short_ <= 0) continue;
            missing = true;
            var chest = storage.FirstOrDefault(c => c.Inventory.Any(s => !s.Empty && fits(s.Itemstack)));
            if (chest != null && !m.PackFull(citizen))
            {
                SetStatus("craftsman-fetching-handle");
                fetching = FetchSupplies(chest.Pos, fits, quantity * m.Config.CraftsmanBatch);
                return true;
            }
            string? code = m.CodeInWarehouse(colony, fits) ?? (part.Code is { } c && !c.Path.Contains('*') ? c.ToString() : null);
            if (code != null) m.EnsureCraftRequest(colony, hut, code, quantity * m.Config.CraftsmanBatch);
        }
        return missing;
    }

    bool Go(Work next, Building hut, string status)
    {
        work = next;
        SetStatus(status);
        return GoWork(hut.HutPos);
    }

    protected override void DoWork(Colony colony, CitizenRecord citizen, Building hut)
    {
        var m = manager!;
        if (kind == null) return;
        switch (work)
        {
            case Work.Knap:
                entity.World.PlaySoundAt(new AssetLocation("sounds/player/knap" + (entity.World.Rand.Next(2) + 1)), target.X + 0.5, target.Y + 0.5, target.Z + 0.5, null, true, 16);
                if (++strikes < m.Config.CraftsmanStrikes) break;
                strikes = 0;
                m.FinishKnapping(colony, hut, citizen, kind);
                break;
            case Work.Haft:
                entity.World.PlaySoundAt(new AssetLocation("sounds/player/build"), target.X + 0.5, target.Y + 0.5, target.Z + 0.5, null, true, 16);
                m.Haft(colony, hut, citizen, kind);
                break;
        }
    }

    protected override string DescribeWork() => kind == null ? Lang.Get("createacolony:activity-craftsman-idle")
        : work == Work.Haft ? Lang.Get("createacolony:activity-craftsman-haft", kind.Sample.GetName())
        : Lang.Get("createacolony:activity-craftsman-knap", kind.Piece.GetName(), strikes, manager?.Config.CraftsmanStrikes ?? 0);
}
