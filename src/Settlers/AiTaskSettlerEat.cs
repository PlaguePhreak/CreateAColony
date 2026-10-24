using System.Collections.Generic;
using CreateAColony.Colonies;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace CreateAColony.Settlers;

/// <summary>
/// When hungry, a settler walks to a container in the colony that holds food and eats one item, one
/// serving of a meal or one slice of pie (see ColonyManager.Food.cs). Food comes from the same chests,
/// shelves and crocks the players use, so feeding the colony is part of the game. Settlers prefer food
/// groups they have not eaten lately. With no food anywhere they try again later and eventually starve.
/// Settlers carrying packed food eat that on the go instead (ColonyManager.Lunch.cs) and only come here once
/// it is gone and they are hungry; here they pack food for later.
/// To eat, a settler sits down as a player does: on a chair, stool or bench near the food if there is a free
/// one, else on the floor (config SitToEat).
/// </summary>
public class AiTaskSettlerEat : SettlerTaskBase
{
    const float EatSeconds = 3f;
    /// <summary>Sitting down, a settler takes its time over the meal.</summary>
    const float SitEatSeconds = 10f;
    /// <summary>How far from the food a settler looks for a seat.</summary>
    const int SeatRange = 6;
    const long NoFoodRetryMs = 30_000;
    /// <summary>After failing to reach food, the settler waits this long before looking again.</summary>
    const long UnreachableRetryMs = 5_000;
    /// <summary>Food that could not be reached is left alone for this long, so the settler tries other food.</summary>
    const long UnreachableForgetMs = 120_000;

    BlockPos? containerPos;
    float eatTime;
    bool eating;
    BlockPos? seat;
    bool sitting;
    long nextSearchMs;
    string? eatingName;
    readonly Dictionary<BlockPos, long> unreachable = new();

    public AiTaskSettlerEat(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig) { }

    public override bool ShouldExecute()
    {
        if (manager == null || entity.World.ElapsedMilliseconds < nextSearchMs) return false;
        var citizen = Citizen;
        var needs = entity.GetBehavior<EntityBehaviorSettlerNeeds>();
        if (citizen == null || needs == null || !needs.IsHungry(manager.Config, citizen) || CalledToArms) return false;
        // With packed food left the settler eats on the go (EntityBehaviorColonyCitizen) and keeps working.
        if (manager.HasLunch(citizen, entity)) return false;

        var food = manager.FindFood(Colony!, citizen, entity, IsUnreachable);
        if (food == null)
        {
            nextSearchMs = entity.World.ElapsedMilliseconds + NoFoodRetryMs;
            return false;
        }
        containerPos = food.Container.Pos.Copy();
        return true;
    }

    public override void StartExecute()
    {
        base.StartExecute();
        eating = false;
        eatingName = null;
        eatTime = 0;
        seat = null;
        sitting = false;
        WalkTo(containerPos!.ToVec3d().Add(0.5, 0, 0.5), 1.5f);
    }

    public override bool ContinueExecute(float dt)
    {
        if (seat != null)
        {
            // Food in hand, on the way to a seat. If it cannot be reached, the settler sits where it is.
            if (!Arrived && !PathFailed)
            {
                KeepWalking();
                return true;
            }
            StopWalking();
            bool onSeat = Arrived && SettlerSeats.SitOn(entity, seat);
            seat = null;
            SitDown(onSeat);
            return true;
        }
        if (PathFailed)
        {
            // A hungry settler that cannot reach the nearest food used to try the same food again straight away,
            // forever, and never got back to work. Try other food (or work) first.
            long now = entity.World.ElapsedMilliseconds;
            unreachable[containerPos!.Copy()] = now + UnreachableForgetMs;
            nextSearchMs = now + UnreachableRetryMs;
            return false;
        }
        if (!eating && CalledToArms) return false;

        if (!eating)
        {
            if (!Arrived)
            {
                KeepWalking();
                return true;
            }
            StopWalking();
            if (!TakeFood()) return false;
            eating = true;
            if (manager!.Config.SitToEat && (seat = SettlerSeats.FindSeat(entity, containerPos!, SeatRange)) != null)
            {
                WalkTo(SettlerSeats.SeatSpot(seat), 1.2f);
                return true;
            }
            SitDown(false);
        }

        eatTime += dt;
        return eatTime < (sitting ? SitEatSeconds : EatSeconds);
    }

    /// <summary>Starts eating: on the seat it was just put on, else on the floor if it can sit there, else standing.</summary>
    void SitDown(bool onSeat)
    {
        sitting = onSeat;
        if (!onSeat && manager!.Config.SitToEat && SettlerSeats.CanSitHere(entity))
        {
            SettlerSeats.SitOnFloor(entity);
            sitting = true;
        }
        PlayAnimation("eat");
    }

    bool IsUnreachable(BlockPos pos)
    {
        if (!unreachable.TryGetValue(pos, out long until)) return false;
        if (entity.World.ElapsedMilliseconds < until) return true;
        unreachable.Remove(pos);
        return false;
    }

    bool TakeFood()
    {
        var citizen = Citizen;
        if (citizen == null || entity.World.BlockAccessor.GetBlockEntity(containerPos!) is not BlockEntityContainer container) return false;
        eatingName = manager!.EatFood(Colony!, citizen, entity, container);
        // Packs food for later while at the store, so the next meals are eaten on the go.
        if (eatingName != null) manager.PackLunch(Colony!, citizen, entity, container);
        return eatingName != null;
    }

    public override void FinishExecute(bool cancelled)
    {
        StopAnimation("eat");
        if (sitting) SettlerSeats.StandUp(entity);
        sitting = false;
        seat = null;
        eating = false;
        eatingName = null;
        base.FinishExecute(cancelled);
    }

    public override string Describe(Colony colony, CitizenRecord citizen) => eatingName != null
        ? Vintagestory.API.Config.Lang.Get("createacolony:activity-eating-food", eatingName)
        : Vintagestory.API.Config.Lang.Get("createacolony:activity-eating");
}
