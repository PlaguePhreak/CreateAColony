using CreateAColony.Colonies;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace CreateAColony.Settlers;

/// <summary>
/// A wanderer walks from the colony border to the Town Hall to ask to join, and waits around there.
/// One that cannot find the way after a few tries is shown in: it appears beside the Town Hall.
/// </summary>
public class AiTaskSettlerVisitor : SettlerTaskBase
{
    /// <summary>Close enough to the Town Hall to count as there.</summary>
    const float ArriveRange = 4;
    const int TriesBeforeShownIn = 3;
    const long RetryAfterMs = 15000;

    int failures;
    long nextTryMs;

    public AiTaskSettlerVisitor(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig) { }

    Vec3d Target(Colony colony) => colony.TownHallPos.ToVec3d().Add(0.5, 1, 0.5);

    public override bool ShouldExecute()
    {
        if (entity.World.ElapsedMilliseconds < nextTryMs) return false;
        if (Citizen is not { Visitor: true } || Colony is not { } colony) return false;
        float far = ArriveRange + 4;
        return entity.Pos.SquareDistanceTo(Target(colony)) > far * far;
    }

    public override void StartExecute()
    {
        base.StartExecute();
        WalkTo(Target(Colony!), ArriveRange);
    }

    public override bool ContinueExecute(float dt)
    {
        var colony = Colony;
        if (colony == null || Citizen is not { Visitor: true }) return false;

        if (Arrived)
        {
            StopWalking();
            failures = 0;
            ColonyManager.SetWanderHome(entity, entity.Pos.XYZ);
            return false;
        }
        if (PathFailed)
        {
            StopWalking();
            if (++failures >= TriesBeforeShownIn)
            {
                failures = 0;
                var spot = manager!.Beside(colony.TownHallPos);
                entity.TeleportToDouble(spot.X, spot.Y, spot.Z);
                ColonyManager.SetWanderHome(entity, spot);
            }
            nextTryMs = entity.World.ElapsedMilliseconds + RetryAfterMs;
            return false;
        }
        KeepWalking();
        return true;
    }

    public override string Describe(Colony colony, CitizenRecord citizen) => Lang.Get("createacolony:activity-visitor-walking");
}
