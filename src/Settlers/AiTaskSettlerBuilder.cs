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
    /// <summary>How far below a block a builder looks for the ground to stand on while working on it.</summary>
    const int GroundSearchDepth = 12;

    enum Step { Fetch, Stash, FetchTool, Work, StepAside }

    Step step;
    BlockPos target = new BlockPos(0);
    BuildStep buildStep;
    int orderId;
    ColonyManager.ToolNeed neededTool;

    bool walking;
    float workTime;
    /// <summary>Set by Work when the builder stands where its next block goes: the spot to move to.</summary>
    Vec3d? stepAsideTo;
    /// <summary>While stepping aside: the block the builder must get its body out of.</summary>
    BlockPos? clearOf;
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

        var blueprint = manager.Blueprints.Get(order, order.TargetLevel);
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
        if (manager.ToolNeededAt(colony, buildStep, pos) is { } toolNeed && !HasTool(citizen, toolNeed))
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

        // Everything the next block costs must be in hand; a roof block takes its frame and materials together.
        var costs = manager.NextPlacement(colony, buildStep, pos).Costs;
        var missing = costs.Where(c => manager.PackCount(citizen, ColonyManager.Matcher(c.Code)) < c.Count).ToList();
        if (missing.Count == 0)
        {
            manager.BuildStatus[order.BuildingId] = "building";
            return Go(Step.Work, pos);
        }

        var fits = missing.Select(c => ColonyManager.Matcher(c.Code)).ToList();
        var source = manager.HutStorage(colony, hut).FirstOrDefault(c => c.Inventory.Any(s => !s.Empty && fits.Any(m => m(s.Itemstack))));
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
        stepAsideTo = null;
        clearOf = null;
        BeginStep();
    }

    void BeginStep()
    {
        workTime = 0;
        if (step != Step.Work && Colony is { } colony && Citizen is { } citizen) PutAwayHand(colony, citizen);
        var center = target.ToVec3d().Add(0.5, 0.5, 0.5);
        walking = !(step == Step.Work && entity.Pos.XYZ.DistanceTo(center) <= WorkRange);
        if (!walking) return;
        if (step == Step.StepAside && stepAsideTo != null)
        {
            WalkTo(stepAsideTo, 0.1f);
            return;
        }
        if (step == Step.Work)
        {
            WalkTo(WorkSpot(), 1f);
            return;
        }
        WalkTo(target.ToVec3d().Add(0.5, 0, 0.5), 1.5f);
    }

    /// <summary>
    /// Where to stand to work on the target: on the ground below it, beside it. A block high up (a roof,
    /// the top of a wall) is worked on from the ground underneath, within the builder's reach.
    /// </summary>
    Vec3d WorkSpot()
    {
        var ba = entity.World.BlockAccessor;
        var ground = target.Copy();
        for (int i = 0; i < GroundSearchDepth && ground.Y > 1; i++)
        {
            var below = ground.DownCopy();
            var boxes = ba.GetBlock(below).GetCollisionBoxes(ba, below);
            if (boxes != null && boxes.Length > 0) break;
            ground = below;
        }
        return manager!.FreeSpotNear(entity, ground, 3, target) ?? target.ToVec3d().Add(0.5, 0, 0.5);
    }

    bool InReach() => entity.Pos.XYZ.DistanceTo(target.ToVec3d().Add(0.5, 0.5, 0.5)) <= manager!.Config.BuilderReach;

    void MarkUnreachable()
    {
        var o = Colony?.Buildings.Find(b => b.BuildingId == orderId);
        if (o != null) manager!.BuildStatus[o.BuildingId] = "unreachable";
        nextSearchMs = entity.World.ElapsedMilliseconds + WaitRetryMs;
    }

    public override bool ContinueExecute(float dt)
    {
        if (walking)
        {
            if (PathFailed)
            {
                // A failed step aside falls through: the StepAside step shuffles the builder over itself.
                // If no path gets closer, a builder still works on blocks within reach, so it never gets stuck on its own walls.
                if (step != Step.StepAside && (step != Step.Work || !InReach()))
                {
                    MarkUnreachable();
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
            // Standing on the ground below a block can still leave it out of reach (too high up).
            if (step == Step.Work && !InReach())
            {
                MarkUnreachable();
                return false;
            }
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
                FetchTool(colony, citizen);
                break;
            case Step.Stash:
                Stash(colony, citizen);
                if (!manager!.PackEmpty(citizen)) return false;
                break;
            case Step.StepAside:
                // The pathfinder counts "close enough" as arrived, which can leave the builder half inside
                // the block it wants to place. Shuffle the last bit over to the free spot.
                if (clearOf != null && stepAsideTo != null && manager!.Overlaps(entity, clearOf)
                    && entity.Pos.XYZ.DistanceTo(stepAsideTo) < 2.5)
                {
                    entity.TeleportToDouble(stepAsideTo.X, stepAsideTo.Y, stepAsideTo.Z);
                }
                stepAsideTo = null;
                clearOf = null;
                break;
            case Step.Work:
                if (workAnim == null)
                {
                    PrepareHand(colony, citizen);
                    workAnim = PlayShapeAnimation(WorkAnimationCode(colony), "build");
                }
                workTime += dt;
                if (workTime < manager!.Config.BuilderSecondsPerBlock) return true;
                Work(colony, citizen);
                StopWorkAnimation();
                if (stepAsideTo != null)
                {
                    clearOf = target.Copy();
                    Go(Step.StepAside, stepAsideTo.AsBlockPos);
                    BeginStep();
                    return true;
                }
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
        var blueprint = manager.Blueprints.Get(order, order.TargetLevel);
        var costs = manager.NextPlacement(colony, buildStep, manager.StepPos(order, buildStep)).Costs;
        if (blueprint == null || costs.Count == 0) return;

        var storage = manager.HutStorage(colony, hut).Cast<BlockEntityContainer>().ToList();
        var needs = manager.MaterialsNeeded(colony, order, blueprint);
        var matchers = needs.Keys.Select(ColonyManager.Matcher).ToList();
        manager.UnloadPack(colony, citizen, storage, s => !matchers.Any(m => m(s)) && !ColonyManager.IsBuilderTool(s));

        var nowMatchers = costs.Select(c => ColonyManager.Matcher(c.Code)).ToList();
        foreach (var cost in costs)
        {
            var current = ColonyManager.Matcher(cost.Code);
            int currentNeed = needs.TryGetValue(cost.Code, out var n) ? n.Count : cost.Count;
            if (Load(colony, citizen, storage, current, currentNeed) == 0 && manager.PackCount(citizen, current) < cost.Count)
            {
                // Full of other materials: make room for what the next block needs right now.
                manager.UnloadPack(colony, citizen, storage, s => !nowMatchers.Any(m => m(s)) && !ColonyManager.IsBuilderTool(s));
                Load(colony, citizen, storage, current, currentNeed);
            }
        }
        foreach (var (code, (_, count)) in needs)
        {
            if (costs.Any(c => c.Code == code)) continue;
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

    bool HasTool(CitizenRecord citizen, ColonyManager.ToolNeed need) =>
        ColonyManager.ToolFits(Hand?.Itemstack, need.Type, need.Tier)
        || manager!.PackCount(citizen, s => ColonyManager.ToolFits(s, need.Type, need.Tier)) > 0;

    /// <summary>Takes a tool that fits the job from the hut chest into the inventory.</summary>
    void FetchTool(Colony colony, CitizenRecord citizen)
    {
        if (entity.World.BlockAccessor.GetBlockEntity(target) is not BlockEntityContainer chest) return;
        var tool = manager!.TakeItems(new[] { chest }, s => ColonyManager.ToolFits(s, neededTool.Type, neededTool.Tier), 1);
        if (tool == null) return;
        manager.AddToPack(colony, citizen, tool);
        if (tool.StackSize > 0) manager.PutItems(chest, tool); // inventory full: the tool waits in the chest
    }

    /// <summary>Holds the right tool while clearing a block, and puts tools away otherwise, like a player switching hotbar slots.</summary>
    void PrepareHand(Colony colony, CitizenRecord citizen)
    {
        var hand = Hand;
        if (hand == null) return;
        var current = entity.World.BlockAccessor.GetBlock(target);
        bool clearing = current.Id != 0 && !manager!.KeepsBlock(colony, current, buildStep.BlockId);
        var need = clearing ? manager!.ToolNeededAt(colony, buildStep, target) : null;
        if (need == null)
        {
            PutAwayHand(colony, citizen);
            return;
        }
        if (ColonyManager.ToolFits(hand.Itemstack, need.Value.Type, need.Value.Tier)) return;
        var tool = manager!.TakeFromPack(colony, citizen, s => ColonyManager.ToolFits(s, need.Value.Type, need.Value.Tier), 1);
        if (tool == null) return;
        PutAwayHand(colony, citizen);
        hand.Itemstack = tool;
        SyncHand();
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
            if (toolNeed != null && hand?.Itemstack != null)
            {
                hand.Itemstack.Collectible.DamageItem(entity.World, entity, hand);
                SyncHand(); // a tool that just broke must vanish from the model too
            }
        }
        if (buildStep.BlockId == 0) return;

        var (place, costs) = manager.NextPlacement(colony, buildStep, target);
        var paid = new List<ItemStack>();
        foreach (var cost in costs)
        {
            var taken = manager.TakeFromPack(colony, citizen, ColonyManager.Matcher(cost.Code), cost.Count);
            if (taken != null) paid.Add(taken);
            if (taken == null || taken.StackSize < cost.Count)
            {
                foreach (var stack in paid) manager.AddToPack(colony, citizen, stack);
                return;
            }
        }
        var used = paid.FirstOrDefault();
        place = manager.SwapToMaterial(colony, place, used);
        if (manager.EntityInTheWay(place, target) is { } inTheWay)
        {
            foreach (var stack in paid) manager.AddToPack(colony, citizen, stack);
            if (inTheWay.EntityId == entity.EntityId && manager.FreeSpotNear(entity, target, 3, target) is { } spot)
            {
                // Standing where the block goes: step out of the way first.
                stepAsideTo = spot;
            }
            else
            {
                // Someone else is standing there: build elsewhere and come back to this block later.
                order.Cursor++;
                manager.MarkDirty(colony);
            }
            return;
        }
        manager.PlaceForBuild(place.Id, target);
        manager.FinishPlacement(place, target);
        if (place.Id == buildStep.BlockId) manager.RestoreSavedDetails(buildStep, target);
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
        var colony = Colony;
        var citizen = Citizen;
        if (colony != null && citizen != null) PutAwayHand(colony, citizen);
        base.FinishExecute(cancelled);
    }

    public override string Describe(Colony colony, CitizenRecord citizen)
    {
        var order = colony.Buildings.Find(b => b.BuildingId == orderId);
        string building = order == null ? "" : Lang.Get("createacolony:building-" + order.Type) + " " + order.TargetLevel;
        string key = step switch
        {
            Step.Fetch => "activity-builder-fetch",
            Step.FetchTool => "activity-builder-tool",
            Step.Stash => "activity-builder-stash",
            Step.StepAside => "activity-builder-aside",
            _ => "activity-builder-work"
        };
        return Lang.Get("createacolony:" + key, building);
    }
}
