using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace CreateAColony.Settlers;

/// <summary>
/// When hungry, a settler walks to the nearest container in the colony that holds food, takes one
/// item, and eats it. Food comes from the same chests the players use, so feeding the colony is part
/// of the game. With no food anywhere the settler tries again later and eventually starves.
/// </summary>
public class AiTaskSettlerEat : SettlerTaskBase
{
    const float EatSeconds = 3f;
    const long NoFoodRetryMs = 30_000;

    BlockPos? containerPos;
    float eatTime;
    bool eating;
    long nextSearchMs;

    public AiTaskSettlerEat(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig) { }

    public override bool ShouldExecute()
    {
        if (manager == null || entity.World.ElapsedMilliseconds < nextSearchMs) return false;
        var citizen = Citizen;
        var needs = entity.GetBehavior<EntityBehaviorSettlerNeeds>();
        if (citizen == null || needs == null || !needs.IsHungry(manager.Config, citizen)) return false;

        var container = manager.FindFoodContainer(Colony!, entity);
        if (container == null)
        {
            nextSearchMs = entity.World.ElapsedMilliseconds + NoFoodRetryMs;
            return false;
        }
        containerPos = container.Pos.Copy();
        return true;
    }

    public override void StartExecute()
    {
        base.StartExecute();
        eating = false;
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

        var slot = manager!.FindFoodSlot(container, entity);
        if (slot?.Itemstack == null) return false;

        var props = slot.Itemstack.Collectible.GetNutritionProperties(entity.World, slot.Itemstack, entity);
        if (props == null) return false;
        slot.TakeOut(1);
        slot.MarkDirty();
        container.MarkDirty(true);

        citizen.Saturation = System.Math.Min(manager.Config.SaturationMax, citizen.Saturation + props.Satiety);
        return true;
    }

    public override void FinishExecute(bool cancelled)
    {
        StopAnimation("eat");
        eating = false;
        base.FinishExecute(cancelled);
    }
}
