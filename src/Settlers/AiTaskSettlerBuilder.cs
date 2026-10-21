using System.Collections.Generic;
using System.Linq;
using CreateAColony.Colonies;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace CreateAColony.Settlers;

/// <summary>
/// A builder works from a Builder's Hut. It takes the oldest work order in the colony and goes
/// through the blueprint one block at a time: clearing what is in the way, then placing blocks.
/// Materials come from the chests at its own hut; whatever is missing it asks couriers to bring from
/// the warehouse, and waits. Blocks it clears go into the same chests.
/// </summary>
public class AiTaskSettlerBuilder : SettlerTaskBase
{
    const long IdleRetryMs = 5_000;
    const long WaitRetryMs = 15_000;
    /// <summary>How close a builder must stand to place or clear a block.</summary>
    const float WorkRange = 4.5f;
    /// <summary>If no path gets closer, a builder still works on blocks this close, so it never gets stuck on its own walls.</summary>
    const float StretchRange = 8f;

    enum Step { Fetch, Stash, FetchTool, Work }

    Step step;
    BlockPos target = new BlockPos(0);
    BuildStep buildStep;
    int orderId;
    ColonyManager.ToolNeed neededTool;

    /// <summary>The settler's right hand; what is in it shows in the game and is the tool it works with.</summary>
    ItemSlot? Hand => entity.RightHandItemSlot;
    bool walking;
    float workTime;
    /// <summary>The animation playing while working on the current block.</summary>
    string? workAnim;
    long nextSearchMs;

    public AiTaskSettlerBuilder(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig) { }

    public override bool ShouldExecute()
    {
        if (manager == null || entity.World.ElapsedMilliseconds < nextSearchMs) return false;
        nextSearchMs = entity.World.ElapsedMilliseconds + IdleRetryMs;
        return Plan();
    }

    /// <summary>Decides the next thing to do and sets step and target. False when there is nothing to do now.</summary>
    bool Plan()
    {
        var colony = Colony;
        var citizen = Citizen;
        if (colony == null || citizen == null || citizen.JobCode != Jobs.Builder) return false;

        var hut = manager!.EnsureBuilderHut(colony, citizen);
        if (hut == null)
        {
            foreach (var b in colony.Buildings.Where(b => b.HasWorkOrder)) manager.BuildStatus[b.BuildingId] = "no-builder-hut";
            return false;
        }

        bool carrying = !manager.PackEmpty(citizen);
        var order = manager.TakeWorkOrder(colony, citizen);
        if (order == null) return carrying && StashAt(colony, hut);
        orderId = order.BuildingId;

        var blueprint = manager.Blueprints.Get(order.Type, order.TargetLevel, order.Rotation);
        if (blueprint == null)
        {
            manager.BuildStatus[order.BuildingId] = "no-blueprint";
            return false;
        }
        if (!manager.FindNextStep(colony, order, blueprint, out buildStep, out var pos))
        {
            manager.CompleteWorkOrder(colony, order);
            return carrying && StashAt(colony, hut);
        }

        // The right tool first: a builder digs with a shovel and fells with an axe, like a player.
        if (manager.ToolNeededAt(colony, buildStep, pos) is { } toolNeed && !ColonyManager.ToolFits(Hand?.Itemstack, toolNeed.Type, toolNeed.Tier))
        {
            neededTool = toolNeed;
            var toolChest = manager.HutStorage(colony, hut)
                .FirstOrDefault(c => c.Inventory.Any(s => ColonyManager.ToolFits(s.Itemstack, toolNeed.Type, toolNeed.Tier)));
            if (toolChest != null) return Go(Step.FetchTool, toolChest.Pos);

            bool canAsk = manager.EnsureToolRequest(colony, order, hut, toolNeed);
            manager.BuildStatus[order.BuildingId] = canAsk ? "needs-tool-" + toolNeed.Type.ToString().ToLowerInvariant() : "no-hut-chest";
            nextSearchMs = entity.World.ElapsedMilliseconds + WaitRetryMs;
            return false;
        }

        var cost = manager.NextPlacement(colony, buildStep, pos).Cost;
        var fits = cost == null ? null : ColonyManager.Matcher(cost.Value.Code);
        if (fits == null || manager.PackCount(citizen, fits) > 0)
        {
            manager.BuildStatus[order.BuildingId] = "building";
            return Go(Step.Work, pos);
        }

        var source = manager.HutStorage(colony, hut).FirstOrDefault(c => c.Inventory.Any(s => !s.Empty && fits(s.Itemstack)));
        if (source != null)
        {
            manager.BuildStatus[order.BuildingId] = "building";
            return Go(Step.Fetch, source.Pos);
        }

        // Nothing to build with: ask couriers and wait.
        bool hasStorage = manager.EnsureMaterialRequests(colony, order, hut, citizen, blueprint);
        manager.BuildStatus[order.BuildingId] = hasStorage ? "waiting-materials" : "no-hut-chest";
        nextSearchMs = entity.World.ElapsedMilliseconds + WaitRetryMs;
        return false;
    }

    bool StashAt(Colony colony, Building hut)
    {
        var chest = manager!.HutStorage(colony, hut).FirstOrDefault();
        return chest != null && Go(Step.Stash, chest.Pos);
    }

    bool Go(Step next, BlockPos pos)
    {
        step = next;
        target = pos.Copy();
        return true;
    }

    public override void StartExecute()
    {
        base.StartExecute();
        BeginStep();
    }

    void BeginStep()
    {
        workTime = 0;
        var center = target.ToVec3d().Add(0.5, 0.5, 0.5);
        walking = !(step == Step.Work && entity.Pos.XYZ.DistanceTo(center) <= WorkRange);
        if (walking) WalkTo(target.ToVec3d().Add(0.5, 0, 0.5), step == Step.Work ? WorkRange - 1 : 1.5f);
    }

    public override bool ContinueExecute(float dt)
    {
        if (walking)
        {
            if (PathFailed)
            {
                if (step != Step.Work || entity.Pos.XYZ.DistanceTo(target.ToVec3d().Add(0.5, 0.5, 0.5)) > StretchRange)
                {
                    var o = Colony?.Buildings.Find(b => b.BuildingId == orderId);
                    if (o != null) manager!.BuildStatus[o.BuildingId] = "unreachable";
                    nextSearchMs = entity.World.ElapsedMilliseconds + WaitRetryMs;
                    return false;
                }
            }
            else if (!Arrived)
            {
                KeepWalking();
                return true;
            }
            StopWalking();
            walking = false;
        }

        var colony = Colony;
        var citizen = Citizen;
        if (colony == null || citizen == null) return false;

        switch (step)
        {
            case Step.Fetch:
                Fetch(colony, citizen);
                break;
            case Step.FetchTool:
                FetchTool();
                break;
            case Step.Stash:
                Stash(colony, citizen);
                if (!manager!.PackEmpty(citizen)) return false;
                break;
            case Step.Work:
                workAnim ??= PlayShapeAnimation(WorkAnimationCode(colony), "build");
                workTime += dt;
                if (workTime < manager!.Config.BuilderSecondsPerBlock) return true;
                Work(colony, citizen);
                StopWorkAnimation();
                break;
        }

        // Straight on to the next block; only walk if it is out of reach.
        if (!Plan()) return false;
        BeginStep();
        return true;
    }

    /// <summary>
    /// Loads up at the hut: puts back what the order no longer needs, then takes the material for the
    /// next block and as much of the rest of the order as the inventory holds, so one trip goes a long way.
    /// </summary>
    void Fetch(Colony colony, CitizenRecord citizen)
    {
        var order = colony.Buildings.Find(b => b.BuildingId == orderId);
        var hut = manager!.EnsureBuilderHut(colony, citizen);
        if (order == null || hut == null) return;
        var blueprint = manager.Blueprints.Get(order.Type, order.TargetLevel, order.Rotation);
        var cost = manager.NextPlacement(colony, buildStep, manager.StepPos(order, buildStep)).Cost;
        if (blueprint == null || cost == null) return;

        var storage = manager.HutStorage(colony, hut).Cast<BlockEntityContainer>().ToList();
        var needs = manager.MaterialsNeeded(colony, order, blueprint);
        var matchers = needs.Keys.Select(ColonyManager.Matcher).ToList();
        manager.UnloadPack(colony, citizen, storage, s => !matchers.Any(m => m(s)));

        var current = ColonyManager.Matcher(cost.Value.Code);
        int currentNeed = needs.TryGetValue(cost.Value.Code, out var n) ? n.Count : cost.Value.Count;
        if (Load(colony, citizen, storage, current, currentNeed) == 0 && manager.PackCount(citizen, current) == 0)
        {
            // Full of other materials: make room for the one needed right now.
            manager.UnloadPack(colony, citizen, storage, s => !current(s));
            Load(colony, citizen, storage, current, currentNeed);
        }
        foreach (var (code, (_, count)) in needs)
        {
            if (code == cost.Value.Code) continue;
            Load(colony, citizen, storage, ColonyManager.Matcher(code), count);
        }
    }

    /// <summary>Takes items from the chests into the inventory until it holds want of them or is full. Returns how many moved.</summary>
    int Load(Colony colony, CitizenRecord citizen, List<BlockEntityContainer> storage, System.Func<ItemStack, bool> match, int want)
    {
        int moved = 0;
        int missing = want - manager!.PackCount(citizen, match);
        while (missing > 0)
        {
            var part = manager.TakeItems(storage, match, missing);
            if (part == null) break;
            int n = manager.AddToPack(colony, citizen, part);
            moved += n;
            missing -= n;
            if (part.StackSize > 0)
            {
                foreach (var chest in storage)
                {
                    if (part.StackSize <= 0) break;
                    manager.PutItems(chest, part);
                }
                break;
            }
        }
        return moved;
    }

    /// <summary>Swaps whatever tool is in hand for one that fits the job, from the hut chest.</summary>
    void FetchTool()
    {
        var hand = Hand;
        if (hand == null || entity.World.BlockAccessor.GetBlockEntity(target) is not BlockEntityContainer chest) return;
        var slot = chest.Inventory.FirstOrDefault(s => ColonyManager.ToolFits(s.Itemstack, neededTool.Type, neededTool.Tier));
        if (slot == null) return;

        if (!hand.Empty)
        {
            var old = hand.TakeOutWhole();
            manager!.PutItems(chest, old);
            if (old.StackSize > 0) entity.World.SpawnItemEntity(old, entity.Pos.XYZ);
        }
        hand.Itemstack = slot.TakeOut(1);
        slot.MarkDirty();
        hand.MarkDirty();
        chest.MarkDirty(true);
    }

    void Stash(Colony colony, CitizenRecord citizen)
    {
        var hut = manager!.EnsureBuilderHut(colony, citizen);
        if (hut == null) return;
        // Hut chests full: wait instead of walking back and forth.
        if (!manager.UnloadPack(colony, citizen, manager.HutStorage(colony, hut))) nextSearchMs = entity.World.ElapsedMilliseconds + WaitRetryMs;
    }

    void Work(Colony colony, CitizenRecord citizen)
    {
        var order = colony.Buildings.Find(b => b.BuildingId == orderId);
        var hut = manager!.EnsureBuilderHut(colony, citizen);
        if (order == null || hut == null || manager.IsStepDone(colony, buildStep, target)) return;

        var ba = entity.World.BlockAccessor;
        var current = ba.GetBlock(target);
        if (current.Id != 0 && !manager.KeepsBlock(colony, current, buildStep.BlockId))
        {
            var toolNeed = manager.ToolNeededAt(colony, buildStep, target);
            var hand = Hand;
            if (toolNeed != null && !ColonyManager.ToolFits(hand?.Itemstack, toolNeed.Value.Type, toolNeed.Value.Tier)) return;
            manager.BreakForBuild(colony, hut, target);
            // Every block cleared wears the tool down; a broken tool sends the builder back for another.
            if (toolNeed != null && hand?.Itemstack != null) hand.Itemstack.Collectible.DamageItem(entity.World, entity, hand);
        }
        if (buildStep.BlockId == 0) return;

        var (place, cost) = manager.NextPlacement(colony, buildStep, target);
        ItemStack? used = null;
        if (cost != null)
        {
            used = manager.TakeFromPack(colony, citizen, ColonyManager.Matcher(cost.Value.Code), 1);
            if (used == null) return;
        }
        place = manager.SwapToMaterial(colony, place, used);
        manager.PlaceForBuild(place.Id, target);
        manager.FinishPlacement(place, target);
    }

    /// <summary>What a player would play: the held tool's swing when clearing a block, the place motion when building.</summary>
    string WorkAnimationCode(Colony colony)
    {
        var current = entity.World.BlockAccessor.GetBlock(target);
        bool clearing = current.Id != 0 && !manager!.KeepsBlock(colony, current, buildStep.BlockId);
        if (!clearing) return SettlerAnimations.PlayerAnimations.Contains("placeblock") ? "placeblock" : "interactstatic";
        var hand = Hand;
        return hand?.Itemstack?.Collectible.GetHeldTpHitAnimation(hand, entity) ?? "breakhand";
    }

    void StopWorkAnimation()
    {
        if (workAnim != null) StopAnimation(workAnim);
        workAnim = null;
    }

    public override void FinishExecute(bool cancelled)
    {
        StopWorkAnimation();
        base.FinishExecute(cancelled);
    }
}
