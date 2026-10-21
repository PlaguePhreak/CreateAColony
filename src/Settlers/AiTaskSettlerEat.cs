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
/// </summary>
public class AiTaskSettlerEat : SettlerTaskBase
{
    const float EatSeconds = 3f;
    const long NoFoodRetryMs = 30_000;

    BlockPos? containerPos;
    float eatTime;
    bool eating;
    long nextSearchMs;
    string? eatingName;

    public AiTaskSettlerEat(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig) { }

    public override bool ShouldExecute()
    {
        if (manager == null || entity.World.ElapsedMilliseconds < nextSearchMs) return false;
        var citizen = Citizen;
        var needs = entity.GetBehavior<EntityBehaviorSettlerNeeds>();
        if (citizen == null || needs == null || !needs.IsHungry(manager.Config, citizen)) return false;

        var food = manager.FindFood(Colony!, citizen, entity);
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
        WalkTo(containerPos!.ToVec3d().Add(0.5, 0, 0.5), 1.5f);
    }

    public override bool ContinueExecute(float dt)
    {
        if (PathFailed) return false;

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
            PlayAnimation("eat");
        }

        eatTime += dt;
        return eatTime < EatSeconds;
    }

    bool TakeFood()
    {
        var citizen = Citizen;
        if (citizen == null || entity.World.BlockAccessor.GetBlockEntity(containerPos!) is not BlockEntityContainer container) return false;
        eatingName = manager!.EatFood(Colony!, citizen, entity, container);
        return eatingName != null;
    }

    public override void FinishExecute(bool cancelled)
    {
        StopAnimation("eat");
        eating = false;
        eatingName = null;
        base.FinishExecute(cancelled);
    }

    public override string Describe(Colony colony, CitizenRecord citizen) => eatingName != null
        ? Vintagestory.API.Config.Lang.Get("createacolony:activity-eating-food", eatingName)
        : Vintagestory.API.Config.Lang.Get("createacolony:activity-eating");
}
