using CreateAColony.Colonies;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace CreateAColony.Settlers;

/// <summary>
/// On a service day, during the service's hours, a settler with Faith enough leaves what it is doing, walks to the
/// Church and stands there facing the hut block until the service is over (ColonyManager.Church.cs). Guards stay on
/// watch and the clergy lead it (AiTaskSettlerPriest). Eating, sleeping and running from danger still come first.
/// </summary>
public class AiTaskSettlerWorship : SettlerTaskBase
{
    const long RetryMs = 10_000;
    const float ArriveRange = 2f;
    /// <summary>Close enough to the Church to count as there when the way to the chosen spot fails.</summary>
    const float NearEnough = 10f;

    long nextTryMs;
    int churchId;
    BlockPos altar = new(0);
    Vec3d goal = new();
    bool there;

    public AiTaskSettlerWorship(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig) { }

    protected override bool CountsForWillpower => false;

    public override bool ShouldExecute()
    {
        if (manager == null || entity.World.ElapsedMilliseconds < nextTryMs) return false;
        nextTryMs = entity.World.ElapsedMilliseconds + RetryMs;
        if (Colony is not { } colony || Citizen is not { } citizen || manager.ServiceFor(colony, citizen) is not { } church) return false;
        churchId = church.BuildingId;
        altar = church.HutPos.Copy();
        // A spot among the others, a few blocks from the hut block.
        var rand = entity.World.Rand;
        var around = altar.AddCopy(rand.Next(-4, 5), 0, rand.Next(-4, 5));
        goal = manager.FreeSpotNear(entity, around, 3) ?? manager.FreeSpotNear(entity, altar, 5) ?? altar.ToVec3d().Add(0.5, 1, 0.5);
        return true;
    }

    public override void StartExecute()
    {
        base.StartExecute();
        there = false;
        WalkTo(goal, ArriveRange);
    }

    public override bool ContinueExecute(float dt)
    {
        if (manager == null || Colony is not { } colony || Citizen is not { } citizen) return false;
        if (!manager.IsServiceTime(out _)) return false;
        if (there)
        {
            // Facing the hut block, where the priest stands.
            entity.Pos.Yaw = (float)System.Math.Atan2(altar.X + 0.5 - entity.Pos.X, altar.Z + 0.5 - entity.Pos.Z);
            return true;
        }
        if (Arrived || (PathFailed && entity.Pos.SquareDistanceTo(altar.ToVec3d().Add(0.5, 1, 0.5)) <= NearEnough * NearEnough))
        {
            StopWalking();
            there = true;
            manager.AtService(colony, citizen, clergy: false);
            return true;
        }
        if (PathFailed)
        {
            StopWalking();
            return false;
        }
        KeepWalking();
        return true;
    }

    public override string Describe(Colony colony, CitizenRecord citizen) =>
        Lang.Get(there ? "createacolony:activity-worship-at" : "createacolony:activity-worship-walking");
}
