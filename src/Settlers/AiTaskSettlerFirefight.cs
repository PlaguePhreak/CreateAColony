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
/// A fire on the colony's land (ColonyManager.Fires.cs): settlers near it drop what they are doing, even sleep,
/// and put it out with a watering can, as a player would. With no can on it, a settler takes one from a chest at
/// home, the warehouse or a hut (the Farmer's Hut keeps them); with an empty can, it fills it at the nearest
/// water first. A few settlers go to each fire (config FirefightersPerFire). Running from an attacker, being hurt
/// badly and a guard's call to arms come first. The can stays in the settler's inventory for the next fire.
/// </summary>
public class AiTaskSettlerFirefight : SettlerTaskBase
{
    const long CheckMs = 1_500;
    const long UnreachableForgetMs = 60_000;
    const float FireReach = 2.5f;
    const float WaterReach = 4.5f;
    /// <summary>How far a player can reach: a settler that cannot walk closer pours from here.</summary>
    const float ReachFar = 5f;

    enum Step { Fetch, Refill, Pour }

    Step step;
    BlockPos target = new(0);
    BlockPos? fire;
    long nextCheckMs;
    bool walking;
    float workTime;
    string? workAnim;
    readonly Dictionary<BlockPos, long> unreachable = new();

    public AiTaskSettlerFirefight(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig) { }

    public override bool ShouldExecute()
    {
        if (manager == null || !manager.Config.Firefighting || entity.World.ElapsedMilliseconds < nextCheckMs) return false;
        nextCheckMs = entity.World.ElapsedMilliseconds + CheckMs;
        if (Colony is not { } colony || !manager.HasFire(colony) || Citizen is not { } citizen) return false;
        if (citizen.Visitor || citizen.IsDowned || Interrupted()) return false;
        return Plan(colony, citizen);
    }

    /// <summary>Running from an attacker or called to fight: those come first.</summary>
    bool Interrupted() => manager!.FleeingFrom(entity) != null || CalledToArms;

    bool Plan(Colony colony, CitizenRecord citizen)
    {
        var m = manager!;
        fire = m.FireToFight(colony, entity, IsUnreachable);
        if (fire == null) return false;
        var (hasCan, water) = m.FireWater(citizen, Hand);
        if (water > 0) return Go(Step.Pour, fire);
        if (hasCan)
        {
            // Fill up at the water nearest the fire, else nearest the settler.
            int r = System.Math.Max(4, m.Config.FireWaterSearchRadius);
            var pond = m.FindWaterNear(colony, fire, r) ?? m.FindWaterNear(colony, entity.Pos.AsBlockPos, r);
            return pond != null && !IsUnreachable(pond) && Go(Step.Refill, pond);
        }
        if (m.WateringCanSupply(colony, citizen, entity.Pos.XYZ) is { } chest && !IsUnreachable(chest.Pos)) return Go(Step.Fetch, chest.Pos);
        m.NoWateringCanAlarm(colony);
        return false;
    }

    bool Go(Step next, BlockPos pos)
    {
        step = next;
        target = pos.Copy();
        manager!.JoinFire(fire!, entity);
        return true;
    }

    bool IsUnreachable(BlockPos pos)
    {
        if (!unreachable.TryGetValue(pos, out long until)) return false;
        if (entity.World.ElapsedMilliseconds < until) return true;
        unreachable.Remove(pos);
        return false;
    }

    float Reach => step switch
    {
        Step.Pour => FireReach,
        Step.Refill => WaterReach,
        _ => 1.5f
    };

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
        if (Colony is not { } colony || Citizen is not { } citizen || citizen.IsDowned || Interrupted()) return false;
        // Put out by someone else, by rain, or burnt out on the way: on to the next fire.
        if (fire != null && !manager!.IsLooseFire(fire))
        {
            StopWorkAnimation();
            if (!Plan(colony, citizen)) return false;
            BeginStep();
            return true;
        }
        if (walking)
        {
            // A fire up in a tree or on a roof cannot be walked to: pour from as close as the settler got, if a player could reach.
            if (PathFailed && entity.Pos.XYZ.DistanceTo(target.ToVec3d().Add(0.5, 0.5, 0.5)) <= ReachFar)
            {
                StopWalking();
                walking = false;
            }
            else if (PathFailed)
            {
                // Try another fire, chest or pond, if there is one.
                unreachable[target.Copy()] = entity.World.ElapsedMilliseconds + UnreachableForgetMs;
                if (!Plan(colony, citizen)) return false;
                BeginStep();
                return true;
            }
            else if (!Arrived)
            {
                KeepWalking();
                return true;
            }
            else
            {
                StopWalking();
                walking = false;
            }
        }

        if (workAnim == null)
        {
            if (step == Step.Pour) HoldCan(colony, citizen);
            workAnim = PlayShapeAnimation(WorkAnimation(), "build");
            entity.Pos.Yaw = (float)System.Math.Atan2(target.X + 0.5 - entity.Pos.X, target.Z + 0.5 - entity.Pos.Z);
        }
        workTime += dt;
        if (workTime < System.Math.Max(0.3f, manager!.Config.FirePourSeconds)) return true;
        StopWorkAnimation();

        switch (step)
        {
            case Step.Fetch:
                if (!TakeCan(colony, citizen))
                {
                    unreachable[target.Copy()] = entity.World.ElapsedMilliseconds + UnreachableForgetMs;
                    return false;
                }
                break;
            case Step.Refill:
                manager.RefillCansAndHand(colony, citizen, Hand, target);
                break;
            case Step.Pour:
                if (Hand is { } hand) manager.PourOnFire(colony, hand, target);
                break;
        }

        if (!Plan(colony, citizen)) return false;
        BeginStep();
        return true;
    }

    /// <summary>Takes one watering can from the chest at the target. False if there was none, or no room to carry it.</summary>
    bool TakeCan(Colony colony, CitizenRecord citizen)
    {
        if (entity.World.BlockAccessor.GetBlockEntity(target) is not BlockEntityContainer chest) return false;
        // A can with water in it first.
        var can = manager!.TakeItems(new[] { chest }, s => ColonyManager.IsWateringCan(s) && ((BlockWateringCan)s.Block).GetRemainingWateringSeconds(s) > 0, 1)
            ?? manager.TakeItems(new[] { chest }, ColonyManager.IsWateringCan, 1);
        if (can == null) return false;
        manager.AddToPack(colony, citizen, can);
        if (can.StackSize <= 0) return true;
        manager.PutItems(chest, can);
        return false;
    }

    /// <summary>Holds a watering can with water in it, putting away whatever was in the hand.</summary>
    void HoldCan(Colony colony, CitizenRecord citizen)
    {
        var hand = Hand;
        if (hand == null || (hand.Itemstack is { } held && ColonyManager.IsWateringCan(held) && ((BlockWateringCan)held.Block).GetRemainingWateringSeconds(held) > 0)) return;
        var can = manager!.TakeFromPack(colony, citizen, s => ColonyManager.IsWateringCan(s) && ((BlockWateringCan)s.Block).GetRemainingWateringSeconds(s) > 0, 1);
        if (can == null) return;
        PutAwayHand(colony, citizen);
        hand.Itemstack = can;
        SyncHand();
    }

    string WorkAnimation()
    {
        if (step == Step.Pour && Hand is { Itemstack: not null } hand) return hand.Itemstack.Collectible.GetHeldTpUseAnimation(hand, entity) ?? "interactstatic";
        return SettlerAnimations.PlayerAnimations.Contains("placeblock") ? "placeblock" : "interactstatic";
    }

    void StopWorkAnimation()
    {
        if (workAnim != null) StopAnimation(workAnim);
        workAnim = null;
    }

    public override void FinishExecute(bool cancelled)
    {
        StopWorkAnimation();
        manager?.LeaveFire(entity);
        fire = null;
        if (Colony is { } colony && Citizen is { } citizen) PutAwayHand(colony, citizen);
        base.FinishExecute(cancelled);
    }

    protected override bool CountsForWillpower => false;

    public override string Describe(Colony colony, CitizenRecord citizen) => Lang.Get("createacolony:activity-fire-" + step.ToString().ToLowerInvariant());
}
