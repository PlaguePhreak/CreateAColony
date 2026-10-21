using CreateAColony.Colonies;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace CreateAColony.Settlers;

/// <summary>
/// Shared plumbing for settler AI tasks: finding the settler's colony record and walking to a target
/// with the vanilla async pathfinder, giving up after a few failed attempts instead of re-pathing forever.
/// </summary>
public abstract class SettlerTaskBase : AiTaskBase
{
    const int MaxPathAttempts = 3;
    const long RetryDelayMs = 4000;

    protected readonly float moveSpeed;
    protected readonly ColonyManager? manager;

    Vec3d? goal;
    float goalRange;
    bool arrived;
    bool failed;
    int attempts;
    long retryAtMs;

    protected SettlerTaskBase(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig)
    {
        moveSpeed = taskConfig["movespeed"].AsFloat(0.02f);
        if (entity.Api.Side == EnumAppSide.Server) manager = entity.Api.ModLoader.GetModSystem<ColonyManager>();
    }

    protected Colony? Colony => entity is EntitySettler s && s.ColonyId != null ? manager?.GetById(s.ColonyId) : null;

    protected CitizenRecord? Citizen => entity is EntitySettler s ? manager?.FindCitizen(s.ColonyId, s.CitizenId) : null;

    /// <summary>Starts walking to a position. Check Arrived / PathFailed from ContinueExecute and call KeepWalking.</summary>
    protected void WalkTo(Vec3d target, float range)
    {
        goal = target;
        goalRange = range;
        arrived = false;
        failed = false;
        attempts = 0;
        // StopWalking ends the walk animation, so every new leg starts it again; otherwise settlers glide.
        if (animMeta != null) entity.AnimManager.StartAnimation(animMeta);
        StartPath();
    }

    protected bool Arrived => arrived || (goal != null && entity.Pos.SquareDistanceTo(goal) <= (goalRange + 0.6f) * (goalRange + 0.6f));

    /// <summary>True once the target could not be reached after several attempts.</summary>
    protected bool PathFailed => failed && attempts >= MaxPathAttempts;

    /// <summary>Retries a stuck path after a short pause. Call every ContinueExecute while walking.</summary>
    protected void KeepWalking()
    {
        if (goal == null || arrived || attempts >= MaxPathAttempts) return;
        if (failed && entity.World.ElapsedMilliseconds >= retryAtMs) StartPath();
    }

    void StartPath()
    {
        attempts++;
        failed = false;
        pathTraverser.NavigateTo_Async(goal!, moveSpeed, goalRange, OnArrived, OnPathFailed, OnPathFailed, 2000, 1);
    }

    void OnArrived() => arrived = true;

    void OnPathFailed()
    {
        failed = true;
        retryAtMs = entity.World.ElapsedMilliseconds + RetryDelayMs * attempts;
    }

    public override bool CanContinueExecute() => pathTraverser.Ready;

    public override void FinishExecute(bool cancelled)
    {
        pathTraverser.Stop();
        goal = null;
        base.FinishExecute(cancelled);
    }

    /// <summary>Stops moving and stops the walk animation set by the task's "animation" config.</summary>
    protected void StopWalking()
    {
        pathTraverser.Stop();
        if (animMeta != null) entity.AnimManager.StopAnimation(animMeta.Code);
    }

    protected void PlayAnimation(string code)
    {
        if (!entity.AnimManager.IsAnimationActive(code)) entity.AnimManager.StartAnimation(code);
    }

    protected void StopAnimation(string code) => entity.AnimManager.StopAnimation(code);

    /// <summary>
    /// Plays an animation by its shape code, such as a player tool animation copied in by
    /// SettlerAnimations, falling back to an animation from the entity config when the shape lacks it.
    /// </summary>
    protected string PlayShapeAnimation(string code, string fallback)
    {
        if (!SettlerAnimations.PlayerAnimations.Contains(code))
        {
            PlayAnimation(fallback);
            return fallback;
        }
        if (!entity.AnimManager.IsAnimationActive(code))
        {
            entity.AnimManager.StartAnimation(new AnimationMetaData
            {
                Code = code,
                Animation = code,
                Weight = 5,
                BlendMode = EnumAnimationBlendMode.Average
            }.Init());
        }
        return code;
    }
}
