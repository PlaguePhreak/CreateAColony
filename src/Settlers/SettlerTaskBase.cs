using CreateAColony.Colonies;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
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
    /// <summary>A walking settler that has not moved this far in NoProgressMs plans a new path.</summary>
    const double ProgressDistance = 0.5;
    const long NoProgressMs = 4000;

    protected readonly float moveSpeed;
    protected readonly ColonyManager? manager;

    /// <summary>Further than this, a settler walks in stretches: the pathfinder only plans so far ahead.</summary>
    const double LegLength = 28;
    /// <summary>Turns, in degrees, tried for a stretch that cannot be walked straight on: around water or a cliff.</summary>
    static readonly double[] LegTurns = { 0, 35, -35, 70, -70, 110, -110 };

    Vec3d? finalGoal;
    float finalRange;
    System.Func<Vec3d>? refreshGoal;
    Vec3d? goal;
    float goalRange;
    bool onLeg;
    bool legDone;
    int legTurn;
    bool arrived;
    bool failed;
    int attempts;
    long retryAtMs;
    Vec3d lastProgressPos = new();
    long lastProgressMs;

    protected SettlerTaskBase(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig)
    {
        moveSpeed = taskConfig["movespeed"].AsFloat(0.02f);
        if (entity.Api.Side == EnumAppSide.Server) manager = entity.Api.ModLoader.GetModSystem<ColonyManager>();
    }

    protected Colony? Colony => entity is EntitySettler s && s.ColonyId != null ? manager?.GetById(s.ColonyId) : null;

    protected CitizenRecord? Citizen => entity is EntitySettler s ? manager?.FindCitizen(s.ColonyId, s.CitizenId) : null;

    /// <summary>
    /// Starts walking to a position. Check Arrived / PathFailed from ContinueExecute and call KeepWalking.
    /// A far goal is walked in stretches; refresh, if given, works the goal out again for the last
    /// stretch, once the land there is loaded.
    /// </summary>
    protected void WalkTo(Vec3d target, float range, System.Func<Vec3d>? refresh = null)
    {
        finalGoal = target;
        finalRange = range;
        refreshGoal = refresh;
        arrived = false;
        legTurn = 0;
        // StopWalking ends the walk animation, so every new leg starts it again; otherwise settlers glide.
        if (animMeta != null) entity.AnimManager.StartAnimation(animMeta);
        StartLeg();
    }

    /// <summary>Plans the next stretch: straight to the goal when it is near, else a point on the way there.</summary>
    void StartLeg()
    {
        attempts = 0;
        failed = false;
        legDone = false;
        var here = entity.Pos.XYZ;
        double dx = finalGoal!.X - here.X, dz = finalGoal.Z - here.Z;
        double dist = System.Math.Sqrt(dx * dx + dz * dz);
        onLeg = dist > LegLength + 4;
        if (!onLeg)
        {
            if (refreshGoal != null) finalGoal = refreshGoal();
            goal = finalGoal;
            goalRange = finalRange;
        }
        else
        {
            double angle = System.Math.Atan2(dz, dx) + LegTurns[legTurn] * GameMath.DEG2RAD;
            int x = (int)System.Math.Floor(here.X + System.Math.Cos(angle) * LegLength);
            int z = (int)System.Math.Floor(here.Z + System.Math.Sin(angle) * LegLength);
            var ba = entity.World.BlockAccessor;
            int y = ba.GetTerrainMapheightAt(new BlockPos(x, 0, z)) + 1;
            goal = new Vec3d(x + 0.5, y, z + 0.5);
            goalRange = 3;
        }
        StartPath();
    }

    protected bool Arrived => arrived || (finalGoal != null && entity.Pos.SquareDistanceTo(finalGoal) <= (finalRange + 0.6f) * (finalRange + 0.6f));

    /// <summary>True once the target could not be reached after several attempts.</summary>
    protected bool PathFailed => failed && attempts >= MaxPathAttempts && (!onLeg || legTurn >= LegTurns.Length - 1);

    /// <summary>Retries a stuck path after a short pause. Call every ContinueExecute while walking.</summary>
    protected void KeepWalking()
    {
        if (goal == null || arrived) return;
        if (legDone)
        {
            legTurn = 0;
            StartLeg();
            return;
        }
        long now = entity.World.ElapsedMilliseconds;
        if (failed)
        {
            if (attempts < MaxPathAttempts)
            {
                if (now >= retryAtMs) StartPath();
            }
            else if (onLeg && legTurn < LegTurns.Length - 1)
            {
                // This stretch cannot be walked: try one turned a little to the side.
                legTurn++;
                StartLeg();
            }
            return;
        }
        // A block placed in the way after the path was planned leaves the settler walking into it: plan again.
        if (entity.Pos.SquareDistanceTo(lastProgressPos) > ProgressDistance * ProgressDistance)
        {
            lastProgressPos = entity.Pos.XYZ;
            lastProgressMs = now;
        }
        else if (now - lastProgressMs > NoProgressMs)
        {
            pathTraverser.Stop();
            if (attempts < MaxPathAttempts) StartPath();
            else failed = true;
        }
    }

    void StartPath()
    {
        attempts++;
        failed = false;
        lastProgressPos = entity.Pos.XYZ;
        lastProgressMs = entity.World.ElapsedMilliseconds;
        pathTraverser.NavigateTo_Async(goal!, moveSpeed, goalRange, OnArrived, OnPathFailed, OnPathFailed, 2000, 1);
    }

    void OnArrived()
    {
        if (onLeg) legDone = true;
        else arrived = true;
    }

    void OnPathFailed()
    {
        failed = true;
        retryAtMs = entity.World.ElapsedMilliseconds + RetryDelayMs * (attempts - 1);
    }

    public override bool CanContinueExecute() => pathTraverser.Ready;

    public override void FinishExecute(bool cancelled)
    {
        pathTraverser.Stop();
        goal = null;
        finalGoal = null;
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

    /// <summary>The settler's right hand; what is in it shows in the game and is the tool it works with.</summary>
    protected ItemSlot? Hand => entity.RightHandItemSlot;

    /// <summary>
    /// Pushes the hand slot to players' screens. The settler's inventory reaches clients through its
    /// watched attributes, which are only rewritten when stored, so an emptied hand would otherwise
    /// keep showing the old tool.
    /// </summary>
    protected void SyncHand()
    {
        Hand?.MarkDirty();
        entity.GetBehavior<EntityBehaviorContainer>()?.storeInv();
    }

    protected void PutAwayHand(Colony colony, CitizenRecord citizen)
    {
        var hand = Hand;
        if (hand == null || hand.Empty) return;
        var stack = hand.Itemstack;
        manager!.AddToPack(colony, citizen, stack);
        // Inventory full: keep holding it rather than dropping a tool on the ground.
        hand.Itemstack = stack.StackSize > 0 ? stack : null;
        SyncHand();
    }

    /// <summary>What the settler is doing right now, in words, for the settler window.</summary>
    public virtual string Describe(Colony colony, CitizenRecord citizen) => Lang.Get("createacolony:activity-busy");

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
