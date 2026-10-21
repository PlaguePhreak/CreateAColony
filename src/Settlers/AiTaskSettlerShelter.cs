using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace CreateAColony.Settlers;

/// <summary>
/// Before and during a temporal storm, settlers go to their bed (or the Town Hall if they have none)
/// and stay there until the storm is over.
/// </summary>
public class AiTaskSettlerShelter : SettlerTaskBase
{
    public AiTaskSettlerShelter(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig) { }

    public override bool ShouldExecute()
    {
        return manager != null && Citizen != null && manager.IsStormNear();
    }

    public override void StartExecute()
    {
        base.StartExecute();
        var colony = Colony!;
        BlockPos home = manager!.EnsureBed(colony, Citizen!) ?? colony.TownHallPos;
        WalkTo(home.ToVec3d().Add(0.5, 0, 0.5), 1.5f);
    }

    public override bool ContinueExecute(float dt)
    {
        if (!manager!.IsStormNear()) return false;

        if (Arrived || PathFailed)
        {
            // Wait it out where we are; huddle on the floor.
            StopWalking();
            PlayAnimation("shelter");
            return true;
        }

        KeepWalking();
        return true;
    }

    public override void FinishExecute(bool cancelled)
    {
        StopAnimation("shelter");
        base.FinishExecute(cancelled);
    }
}
