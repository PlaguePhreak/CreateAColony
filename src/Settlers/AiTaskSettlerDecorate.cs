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
/// Now and then a settler makes its home nicer with things the household owns (ColonyManager.Decor.cs): it
/// fetches a painting, or a rack, shelf, mount or pole and what goes on it, from the chests at home (the rack
/// may come from the warehouse), puts it up inside the home and takes anything left over back to a chest at
/// home. Any grown-up settler does it, jobless or not, when it is near home; it looks again every
/// DecorCheckSeconds, so it is a short break from work, not a job.
/// </summary>
public class AiTaskSettlerDecorate : SettlerTaskBase
{
    const long UnreachableForgetMs = 300_000;
    const float HangSeconds = 2.5f;

    enum Step { FetchMount, FetchContent, Hang, Return }

    Step step;
    DecorPlan? plan;
    BlockPos target = new(0);
    int homeId;
    long nextSearchMs;
    bool walking;
    float workTime;
    string? workAnim;
    readonly Dictionary<BlockPos, long> unreachable = new();

    public AiTaskSettlerDecorate(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig) { }

    // Putting up a painting is a pleasure, not work.
    protected override bool CountsForWillpower => false;

    public override bool ShouldExecute()
    {
        if (manager == null || entity.World.ElapsedMilliseconds < nextSearchMs) return false;
        // Settlers check at different moments, so a whole colony does not stop work at once.
        nextSearchMs = entity.World.ElapsedMilliseconds + (long)(manager.Config.DecorCheckSeconds * 1000 * (0.75 + entity.World.Rand.NextDouble() * 0.5));
        return Plan();
    }

    bool Plan()
    {
        if (Colony is not { } colony || Citizen is not { } citizen || !manager!.Config.SettlersDecorate) return false;
        if (!citizen.Alive || citizen.Visitor || citizen.IsChild || citizen.IsDowned || manager.IsBrokenDown(citizen)) return false;
        // The clergy lead the service through its hours.
        if (citizen.JobCode == Jobs.Priest && manager.IsServiceTime(out _)) return false;
        // Halfway through a delivery it is helping with: that comes first.
        if (colony.Requests.Exists(r => r.LeasedByCitizenId == citizen.CitizenId)) return false;
        // The clergy decorate their Church instead of their home: they hang its paintings.
        if (manager.DecorPlaceOf(colony, citizen) is not { } home || home.HasWorkOrder || home.Removing) return false;
        var h = home.HutPos;
        if (entity.Pos.XYZ.SquareDistanceTo(h.X, h.Y, h.Z) > manager.Config.DecorRange * manager.Config.DecorRange) return false;
        if (manager.PackFull(citizen)) return false;
        plan = manager.PlanDecor(colony, citizen, home, IsUnreachable);
        if (plan == null) return false;
        homeId = home.BuildingId;
        return Next(Step.FetchMount);
    }

    /// <summary>Goes on with the first step from this one that has something to do.</summary>
    bool Next(Step from)
    {
        var p = plan!;
        step = from;
        if (step == Step.FetchMount)
        {
            if (p.MountFrom != null) return At(p.MountFrom);
            step = Step.FetchContent;
        }
        if (step == Step.FetchContent)
        {
            if (p.ContentFrom != null && p.Want > 0) return At(p.ContentFrom);
            step = Step.Hang;
        }
        if (step == Step.Hang) return At(p.Spot);
        // Return: only when something is left over.
        if (Citizen is not { } citizen || !manager!.PackItems(citizen).Any(s => ColonyManager.IsDecorItem(p.Kind, s))) return false;
        var home = Colony?.Buildings.Find(b => b.BuildingId == homeId);
        var chest = home == null ? null : manager.HomeChests(home).FirstOrDefault();
        return chest != null && At(chest.Pos);
    }

    bool At(BlockPos pos)
    {
        target = pos.Copy();
        return true;
    }

    bool IsUnreachable(BlockPos pos)
    {
        if (!unreachable.TryGetValue(pos, out long until)) return false;
        if (entity.World.ElapsedMilliseconds < until) return true;
        unreachable.Remove(pos);
        return false;
    }

    float Reach => step == Step.Hang ? 2.5f : 1.5f;

    public override void StartExecute()
    {
        base.StartExecute();
        BeginStep();
    }

    void BeginStep()
    {
        workTime = 0;
        walking = entity.Pos.XYZ.DistanceTo(target.ToVec3d().Add(0.5, 0.5, 0.5)) > Reach + 0.5;
        if (walking) WalkTo(target.ToVec3d().Add(0.5, 0, 0.5), Reach);
    }

    public override bool ContinueExecute(float dt)
    {
        if (walking)
        {
            if (PathFailed)
            {
                unreachable[target.Copy()] = entity.World.ElapsedMilliseconds + UnreachableForgetMs;
                return Leave();
            }
            if (!Arrived)
            {
                KeepWalking();
                return true;
            }
            StopWalking();
            walking = false;
        }

        if (Colony is not { } colony || Citizen is not { } citizen || plan is not { } p) return false;
        var home = colony.Buildings.Find(b => b.BuildingId == homeId);
        if (home == null) return false;
        var m = manager!;

        switch (step)
        {
            case Step.FetchMount:
                if (!Take(colony, citizen, s => ColonyManager.IsDecorMountFor(home, p.Kind, s), 1)) return Leave();
                break;
            case Step.FetchContent:
                // Without what goes on it, a new rack still goes up empty; filling one that is up needs it.
                if (!Take(colony, citizen, s => ColonyManager.IsDecorContentFor(home, p.Kind, s), p.Want) && p.Fill) return Leave();
                break;
            case Step.Hang:
                if (workAnim == null)
                {
                    entity.Pos.Yaw = (float)System.Math.Atan2(target.X + 0.5 - entity.Pos.X, target.Z + 0.5 - entity.Pos.Z);
                    workAnim = PlayShapeAnimation(SettlerAnimations.PlayerAnimations.Contains("placeblock") ? "placeblock" : "interactstatic", "build");
                }
                workTime += dt;
                if (workTime < HangSeconds) return true;
                StopWorkAnimation();
                if (!m.PutUpDecor(colony, citizen, home, p)) unreachable[target.Copy()] = entity.World.ElapsedMilliseconds + UnreachableForgetMs;
                break;
            case Step.Return:
                m.UnloadPack(colony, citizen, m.HomeChests(home), s => ColonyManager.IsDecorItem(p.Kind, s));
                return false;
        }

        if (!Next(step + 1)) return false;
        BeginStep();
        return true;
    }

    /// <summary>Something went wrong on the way: anything already taken goes back to a chest at home.</summary>
    bool Leave()
    {
        if (step is Step.FetchMount or Step.Return || plan == null) return false;
        if (!Next(Step.Return)) return false;
        BeginStep();
        return true;
    }

    /// <summary>Takes up to want matching items from the chest at the target into the inventory (not the tool bar). False if nothing moved.</summary>
    bool Take(Colony colony, CitizenRecord citizen, System.Func<ItemStack, bool> match, int want)
    {
        if (entity.World.BlockAccessor.GetBlockEntity(target) is not BlockEntityContainer chest) return false;
        var m = manager!;
        int moved = 0;
        while (moved < want)
        {
            var part = m.TakeItems(new[] { chest }, match, want - moved);
            if (part == null) break;
            moved += m.AddToPack(colony, citizen, part, false);
            if (part.StackSize > 0)
            {
                m.PutItems(chest, part);
                break;
            }
        }
        return moved > 0;
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

    public override string Describe(Colony colony, CitizenRecord citizen) => step switch
    {
        Step.Hang => Lang.Get("createacolony:activity-decorate-" + (plan?.Kind.ToString().ToLowerInvariant() ?? "painting")),
        Step.Return => Lang.Get("createacolony:activity-chores-store"),
        _ => Lang.Get("createacolony:activity-decorate-fetch")
    };
}
