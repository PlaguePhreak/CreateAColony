using System.Collections.Generic;
using System.Linq;
using CreateAColony.Colonies;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using ItemMatch = System.Func<Vintagestory.API.Common.ItemStack, bool>;

namespace CreateAColony.Settlers;

/// <summary>
/// The working day of a settler who gathers from the land around its hut (forester, farmer). It needs
/// a chest at its hut and its tool; without a tool it asks couriers for one and waits. Then it walks
/// from job to job, carrying what it gathers, and takes it to the hut chest when its inventory is full
/// or there is nothing left to do. It keeps its tool and a stock of seeds; couriers take the rest.
/// </summary>
public abstract class AiTaskSettlerWorker : SettlerTaskBase
{
    const long IdleRetryMs = 5_000;
    protected const long WaitRetryMs = 15_000;
    /// <summary>A spot that could not be reached is left alone for this long.</summary>
    const long UnreachableForgetMs = 300_000;
    /// <summary>If no path gets closer, a worker still works on things this close.</summary>
    const float StretchRange = 5f;

    protected enum Step { FetchTool, FetchSupplies, Deposit, Work }

    protected Step step;
    protected BlockPos target = new BlockPos(0);
    /// <summary>How close the worker must stand to work on the target.</summary>
    protected float workRange = 2.5f;
    protected long nextSearchMs;
    int hutId;
    bool walking;
    float workTime;
    string? workAnim;
    ItemMatch supplyMatch = _ => false;
    int supplyWant;
    readonly Dictionary<BlockPos, long> unreachable = new();

    protected AiTaskSettlerWorker(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig) { }

    protected abstract string Job { get; }
    protected abstract string HutType { get; }
    protected abstract EnumTool Tool { get; }
    /// <summary>The seeds this job keeps a stock of in its inventory.</summary>
    protected abstract ItemMatch Stock { get; }
    protected abstract float WorkSeconds { get; }

    /// <summary>Picks the next thing to work on (calling GoWork or FetchSupplies) and sets the hut status. False when there is nothing.</summary>
    protected abstract bool PlanWork(Colony colony, CitizenRecord citizen, Building hut, List<BlockEntityContainer> storage);

    /// <summary>Does the work at the target once the work time is up.</summary>
    protected abstract void DoWork(Colony colony, CitizenRecord citizen, Building hut);

    /// <summary>What the settler is working on, for the settler window.</summary>
    protected abstract string DescribeWork();

    /// <summary>True when the current work is done with the tool in hand, false when it is done bare-handed.</summary>
    protected virtual bool WorkNeedsTool => true;

    /// <summary>What a player would play for the current work.</summary>
    protected virtual string WorkAnimation(ItemSlot? hand) =>
        hand?.Itemstack?.Collectible.GetHeldTpHitAnimation(hand, entity) ?? "breakhand";

    public override bool ShouldExecute()
    {
        if (manager == null || entity.World.ElapsedMilliseconds < nextSearchMs) return false;
        nextSearchMs = entity.World.ElapsedMilliseconds + IdleRetryMs;
        return Plan();
    }

    bool Plan()
    {
        var colony = Colony;
        var citizen = Citizen;
        if (colony == null || citizen == null || citizen.JobCode != Job) return false;

        var hut = manager!.EnsureWorkplace(colony, citizen, HutType);
        if (hut == null) return false;
        hutId = hut.BuildingId;

        var storage = manager.HutStorage(colony, hut).Cast<BlockEntityContainer>().ToList();
        if (storage.Count == 0)
        {
            SetStatus("no-hut-chest");
            return Wait();
        }

        // Hands full: take the harvest home first.
        if (manager.PackFull(citizen) && HasSurplus(citizen)) return Go(Step.Deposit, storage[0].Pos);

        if (!HasTool(citizen))
        {
            var toolChest = storage.FirstOrDefault(c => c.Inventory.Any(s => ColonyManager.ToolFits(s.Itemstack, Tool, 0)));
            if (toolChest != null) return Go(Step.FetchTool, toolChest.Pos);
            if (HasSurplus(citizen)) return Go(Step.Deposit, storage[0].Pos);
            manager.EnsureToolRequest(colony, hut, hut, new ColonyManager.ToolNeed(Tool, 0));
            SetStatus("needs-tool-" + Tool.ToString().ToLowerInvariant());
            return Wait();
        }

        if (PlanWork(colony, citizen, hut, storage)) return true;

        // Nothing left to do: bring the harvest home and wait for things to grow.
        if (HasSurplus(citizen)) return Go(Step.Deposit, storage[0].Pos);
        return Wait();
    }

    bool Wait()
    {
        nextSearchMs = entity.World.ElapsedMilliseconds + WaitRetryMs;
        return false;
    }

    protected void SetStatus(string status)
    {
        if (hutId != 0) manager!.WorkStatus[hutId] = status;
    }

    bool Go(Step next, BlockPos pos)
    {
        step = next;
        target = pos.Copy();
        return true;
    }

    protected bool GoWork(BlockPos pos) => Go(Step.Work, pos);

    /// <summary>Walks to the chest to take up to want items the work needs.</summary>
    protected bool FetchSupplies(BlockPos chest, ItemMatch match, int want)
    {
        supplyMatch = match;
        supplyWant = want;
        return Go(Step.FetchSupplies, chest);
    }

    /// <summary>True when a spot could not be reached a little while ago, so the worker tries something else first.</summary>
    protected bool IsUnreachable(BlockPos pos)
    {
        if (!unreachable.TryGetValue(pos, out long until)) return false;
        if (entity.World.ElapsedMilliseconds < until) return true;
        unreachable.Remove(pos);
        return false;
    }

    bool HasTool(CitizenRecord citizen) =>
        ColonyManager.ToolFits(Hand?.Itemstack, Tool, 0) || manager!.PackCount(citizen, s => ColonyManager.ToolFits(s, Tool, 0)) > 0;

    /// <summary>True when the settler carries anything it does not keep: a harvest, or seeds beyond its stock.</summary>
    bool HasSurplus(CitizenRecord citizen)
    {
        var items = manager!.PackItems(citizen);
        return items.Any(s => s.Collectible.Tool != Tool && !Stock(s))
            || items.Where(Stock).Sum(s => s.StackSize) > manager.SeedReserve(Job);
    }

    public override void StartExecute()
    {
        base.StartExecute();
        BeginStep();
    }

    void BeginStep()
    {
        workTime = 0;
        if (step != Step.Work && Colony is { } colony && Citizen is { } citizen) PutAwayHand(colony, citizen);
        var center = target.ToVec3d().Add(0.5, 0.5, 0.5);
        walking = !(step == Step.Work && entity.Pos.XYZ.DistanceTo(center) <= workRange + 0.5);
        if (walking) WalkTo(target.ToVec3d().Add(0.5, 0, 0.5), step == Step.Work ? workRange : 1.5f);
    }

    public override bool ContinueExecute(float dt)
    {
        if (walking)
        {
            if (PathFailed)
            {
                if (step != Step.Work || entity.Pos.XYZ.DistanceTo(target.ToVec3d().Add(0.5, 0.5, 0.5)) > StretchRange)
                {
                    unreachable[target.Copy()] = entity.World.ElapsedMilliseconds + UnreachableForgetMs;
                    SetStatus("unreachable");
                    nextSearchMs = entity.World.ElapsedMilliseconds + IdleRetryMs;
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
        var hut = colony?.Buildings.Find(b => b.BuildingId == hutId);
        if (colony == null || citizen == null || hut == null) return false;

        switch (step)
        {
            case Step.FetchTool:
                if (!Take(colony, citizen, s => ColonyManager.ToolFits(s, Tool, 0), 1)) return Wait();
                break;
            case Step.FetchSupplies:
                if (!Take(colony, citizen, supplyMatch, supplyWant)) return Wait();
                break;
            case Step.Deposit:
                if (!Deposit(colony, citizen, hut))
                {
                    SetStatus("hut-chest-full");
                    return Wait();
                }
                break;
            case Step.Work:
                if (workAnim == null)
                {
                    PrepareHand(colony, citizen);
                    workAnim = PlayShapeAnimation(WorkAnimation(Hand), "build");
                }
                workTime += dt;
                if (workTime < WorkSeconds) return true;
                DoWork(colony, citizen, hut);
                StopWorkAnimation();
                break;
        }

        // Straight on to the next job; only walk if it is out of reach.
        if (!Plan()) return false;
        BeginStep();
        return true;
    }

    /// <summary>Takes up to want matching items from the chest at the target into the inventory. False if nothing moved.</summary>
    bool Take(Colony colony, CitizenRecord citizen, ItemMatch match, int want)
    {
        if (entity.World.BlockAccessor.GetBlockEntity(target) is not BlockEntityContainer chest) return false;
        int moved = 0;
        while (moved < want)
        {
            var part = manager!.TakeItems(new[] { chest }, match, want - moved);
            if (part == null) break;
            int n = manager.AddToPack(colony, citizen, part);
            moved += n;
            if (part.StackSize > 0)
            {
                manager.PutItems(chest, part); // inventory full: the rest stays in the chest
                break;
            }
        }
        return moved > 0;
    }

    /// <summary>Puts everything but the tool and the seed stock into the hut chests. False if they are too full to take it all.</summary>
    bool Deposit(Colony colony, CitizenRecord citizen, Building hut)
    {
        var storage = manager!.HutStorage(colony, hut).Cast<BlockEntityContainer>().ToList();
        manager.UnloadPack(colony, citizen, storage, s => s.Collectible.Tool != Tool && !Stock(s));

        int extra = manager.PackCount(citizen, Stock) - manager.SeedReserve(Job);
        if (extra > 0 && manager.TakeFromPack(colony, citizen, Stock, extra) is { } seeds)
        {
            foreach (var chest in storage)
            {
                if (seeds.StackSize <= 0) break;
                manager.PutItems(chest, seeds);
            }
            if (seeds.StackSize > 0) manager.AddToPack(colony, citizen, seeds);
        }
        return !HasSurplus(citizen);
    }

    /// <summary>Holds the tool for work done with it and puts it away for work done by hand.</summary>
    void PrepareHand(Colony colony, CitizenRecord citizen)
    {
        var hand = Hand;
        if (hand == null) return;
        if (!WorkNeedsTool)
        {
            PutAwayHand(colony, citizen);
            return;
        }
        if (ColonyManager.ToolFits(hand.Itemstack, Tool, 0)) return;
        var tool = manager!.TakeFromPack(colony, citizen, s => ColonyManager.ToolFits(s, Tool, 0), 1);
        if (tool == null) return;
        PutAwayHand(colony, citizen);
        hand.Itemstack = tool;
        SyncHand();
    }

    /// <summary>True when the tool is in hand for work that needs it.</summary>
    protected bool HoldsTool => ColonyManager.ToolFits(Hand?.Itemstack, Tool, 0);

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
        return step switch
        {
            Step.FetchTool => Lang.Get("createacolony:activity-worker-tool", Lang.Get("createacolony:tool-" + Tool.ToString().ToLowerInvariant())),
            Step.FetchSupplies => Lang.Get("createacolony:activity-worker-seeds"),
            Step.Deposit => Lang.Get("createacolony:activity-worker-deposit"),
            _ => DescribeWork()
        };
    }
}
