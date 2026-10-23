using CreateAColony.Colonies;
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

    /// <summary>Night for most settlers; for a guard on night watch, the day hours it sleeps.</summary>
    bool IsSleepTime()
    {
        var colony = Colony;
        var citizen = Citizen;
        return colony != null && citizen != null && manager!.IsSleepTime(colony, citizen, entity.World.Calendar.HourOfDay);
    }

    public override bool ShouldExecute()
    {
        return manager != null && IsSleepTime() && !CalledToArms;
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
        if (!IsSleepTime() || CalledToArms) return false;

        if (!lyingDown && (Arrived || PathFailed))
        {
            StopWalking();
            if (bedPos != null && Arrived)
            {
                // Lie on top of the bed block (on its own half of a furniture double bed).
                var spot = manager!.BedSpot(Colony!, Citizen!, bedPos);
                entity.TeleportToDouble(spot.X, spot.Y, spot.Z);
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

    public override string Describe(Colony colony, CitizenRecord citizen) => Vintagestory.API.Config.Lang.Get("createacolony:activity-sleeping");
}
