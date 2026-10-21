using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace CreateAColony.Settlers;

/// <summary>
/// At night settlers walk to their bed and lie on it until morning. A settler without a free bed
/// sleeps on the floor next to the Town Hall, which is the player's cue to build more beds.
/// </summary>
public class AiTaskSettlerSleep : SettlerTaskBase
{
    BlockPos? bedPos;
    bool lyingDown;

    public AiTaskSettlerSleep(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig) { }

    bool IsNight()
    {
        var config = manager!.Config;
        float hour = entity.World.Calendar.HourOfDay;
        return config.SleepStartHour > config.SleepEndHour
            ? hour >= config.SleepStartHour || hour < config.SleepEndHour
            : hour >= config.SleepStartHour && hour < config.SleepEndHour;
    }

    public override bool ShouldExecute()
    {
        return manager != null && Citizen != null && IsNight();
    }

    public override void StartExecute()
    {
        base.StartExecute();
        lyingDown = false;
        var colony = Colony!;
        bedPos = manager!.EnsureBed(colony, Citizen!);
        BlockPos target = bedPos ?? colony.TownHallPos;
        WalkTo(target.ToVec3d().Add(0.5, 0, 0.5), bedPos != null ? 1.5f : 2.5f);
    }

    public override bool ContinueExecute(float dt)
    {
        if (!IsNight()) return false;

        if (!lyingDown && (Arrived || PathFailed))
        {
            StopWalking();
            if (bedPos != null && Arrived)
            {
                // Lie on top of the bed block.
                entity.TeleportToDouble(bedPos.X + 0.5, bedPos.Y + 0.6, bedPos.Z + 0.5);
            }
            PlayAnimation("sleep");
            lyingDown = true;
        }

        if (!lyingDown) KeepWalking();
        return true;
    }

    public override void FinishExecute(bool cancelled)
    {
        StopAnimation("sleep");
        if (lyingDown && bedPos != null)
        {
            // Step off the bed so the settler does not start the day standing on it.
            entity.TeleportToDouble(bedPos.X + 0.5, bedPos.Y + 1, bedPos.Z + 0.5);
        }
        lyingDown = false;
        base.FinishExecute(cancelled);
    }
}
