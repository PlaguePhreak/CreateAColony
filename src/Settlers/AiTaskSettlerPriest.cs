using System.Linq;
using CreateAColony.Colonies;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using ItemMatch = System.Func<Vintagestory.API.Common.ItemStack, bool>;

namespace CreateAColony.Settlers;

/// <summary>
/// The working day of the clergy (job Priest, the bishop too; ColonyManager.Church.cs). On a service day they stand
/// at the Church's hut block through the service's hours, which is what makes it a service. Otherwise they hold a
/// funeral at the grave of anyone newly dead, and when enough has happened in the colony they fetch a blank book
/// and an ink and quill from the Church's storage and write a chronicle at the hut block. The Church's paintings
/// they hang through AiTaskSettlerDecorate. With nothing to do they stay near the Church.
/// </summary>
public class AiTaskSettlerPriest : SettlerTaskBase
{
    const long RetryMs = 5_000;
    const long IdleRetryMs = 15_000;
    const float WorkRange = 2.5f;
    /// <summary>Further than this from the Church with nothing to do, they walk back to it.</summary>
    const float HomeRange = 8f;

    enum Mode { Service, Funeral, Fetch, Write, Return }

    Mode mode;
    long nextTryMs;
    int churchId;
    int deadId;
    BlockPos target = new(0);
    ItemMatch fetch = _ => false;
    bool walking;
    float workTime;
    string? workAnim;

    public AiTaskSettlerPriest(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig) { }

    public override bool ShouldExecute()
    {
        if (manager == null || entity.World.ElapsedMilliseconds < nextTryMs) return false;
        nextTryMs = entity.World.ElapsedMilliseconds + RetryMs;
        return Plan();
    }

    bool Plan()
    {
        var m = manager!;
        if (Colony is not { } colony || Citizen is not { JobCode: Jobs.Priest, IsDowned: false } citizen || m.IsBrokenDown(citizen)) return false;
        if (m.ChurchOf(colony, citizen) is not { } church) return false;
        churchId = church.BuildingId;

        if (m.IsServiceTime(out int day) && colony.LastServiceDay < day)
        {
            SetStatus("priest-service");
            return Go(Mode.Service, church.HutPos);
        }
        if (m.NextFuneral(colony) is { } dead)
        {
            deadId = dead.CitizenId;
            m.ClaimFuneral(dead, true);
            SetStatus("priest-funeral");
            return Go(Mode.Funeral, m.FuneralSpot(colony, dead, church));
        }
        if (m.ChronicleDue(colony))
        {
            bool hasBook = m.PackCount(citizen, ColonyManager.IsBlankBook) > 0;
            bool hasPen = m.PackCount(citizen, ColonyManager.IsWritingTool) > 0 || ColonyManager.IsWritingTool(Hand?.Itemstack);
            if (hasBook && hasPen)
            {
                SetStatus("priest-writing");
                return Go(Mode.Write, church.HutPos);
            }
            ItemMatch want = !hasBook ? ColonyManager.IsBlankBook : ColonyManager.IsWritingTool;
            var chest = m.HutStorage(colony, church).FirstOrDefault(c => c.Inventory.Any(s => !s.Empty && want(s.Itemstack)));
            if (chest != null && !m.PackFull(citizen))
            {
                fetch = want;
                SetStatus("priest-fetching");
                return Go(Mode.Fetch, chest.Pos);
            }
            SetStatus(!hasBook ? "priest-needs-book" : "priest-needs-pen");
        }
        else SetStatus(m.ServiceUnderway(colony) ? "priest-service" : "priest-idle");

        nextTryMs = entity.World.ElapsedMilliseconds + IdleRetryMs;
        var h = church.HutPos;
        if (entity.Pos.SquareDistanceTo(h.X + 0.5, h.Y + 1, h.Z + 0.5) > HomeRange * HomeRange) return Go(Mode.Return, h);
        return false;
    }

    bool Go(Mode next, BlockPos pos)
    {
        mode = next;
        target = pos.Copy();
        return true;
    }

    void SetStatus(string status)
    {
        if (Citizen is not { } citizen) return;
        manager!.WorkerStatus[citizen.CitizenId] = status;
        if (churchId != 0) manager.WorkStatus[churchId] = status;
    }

    public override void StartExecute()
    {
        base.StartExecute();
        BeginStep();
    }

    void BeginStep()
    {
        workTime = 0;
        StopWorkAnimation();
        float range = mode == Mode.Fetch ? 1.5f : mode == Mode.Return ? 4f : WorkRange;
        walking = entity.Pos.XYZ.DistanceTo(target.ToVec3d().Add(0.5, 0.5, 0.5)) > range + 0.5;
        if (walking) WalkTo(target.ToVec3d().Add(0.5, 0, 0.5), range);
    }

    public override bool ContinueExecute(float dt)
    {
        var m = manager!;
        if (walking)
        {
            if (PathFailed)
            {
                StopWalking();
                // A funeral far off can be held from where the way ends; anything else is tried again later.
                if (mode != Mode.Funeral || entity.Pos.SquareDistanceTo(target.X + 0.5, target.Y, target.Z + 0.5) > 12 * 12) return false;
            }
            else if (!Arrived)
            {
                KeepWalking();
                return true;
            }
            StopWalking();
            walking = false;
        }

        if (Colony is not { } colony || Citizen is not { } citizen) return false;
        var church = colony.Buildings.Find(b => b.BuildingId == churchId);
        if (church == null) return false;

        switch (mode)
        {
            case Mode.Return:
                return false;
            case Mode.Service:
                if (!m.IsServiceTime(out _)) return false;
                m.AtService(colony, citizen, clergy: true);
                Face(church.HutPos.AddCopy(0, 0, 0), true);
                if (workAnim == null) workAnim = PlayShapeAnimation("interactstatic", "idle");
                return true;
            case Mode.Funeral:
                if (workAnim == null) workAnim = PlayShapeAnimation("interactstatic", "idle");
                workTime += dt;
                if (workTime < m.Config.FuneralSeconds) return true;
                StopWorkAnimation();
                if (colony.Citizens.Find(c => c.CitizenId == deadId) is { } dead) m.HoldFuneral(colony, citizen, dead);
                deadId = 0;
                break;
            case Mode.Fetch:
                if (entity.World.BlockAccessor.GetBlockEntity(target) is BlockEntityContainer chest && m.TakeItems(new[] { chest }, fetch, 1) is { } got)
                {
                    m.AddToPack(colony, citizen, got, false);
                    if (got.StackSize > 0) m.PutItems(chest, got);
                }
                break;
            case Mode.Write:
                if (workAnim == null) workAnim = PlayShapeAnimation("interactstatic", "build");
                workTime += dt;
                if (workTime < m.Config.ChronicleSeconds * m.WorkTimeFactor(colony, citizen)) return true;
                StopWorkAnimation();
                m.WriteChronicle(colony, church, citizen);
                break;
        }

        if (!Plan()) return false;
        BeginStep();
        return true;
    }

    /// <summary>Turns to face a spot; the clergy at the service face away from the hut block, towards the people.</summary>
    void Face(BlockPos pos, bool away)
    {
        float yaw = (float)System.Math.Atan2(pos.X + 0.5 - entity.Pos.X, pos.Z + 0.5 - entity.Pos.Z);
        entity.Pos.Yaw = away ? yaw + GameMath.PI : yaw;
    }

    void StopWorkAnimation()
    {
        if (workAnim != null) StopAnimation(workAnim);
        workAnim = null;
    }

    public override void FinishExecute(bool cancelled)
    {
        StopWorkAnimation();
        if (deadId != 0 && Colony?.Citizens.Find(c => c.CitizenId == deadId) is { } dead) manager?.ClaimFuneral(dead, false);
        deadId = 0;
        base.FinishExecute(cancelled);
    }

    public override string Describe(Colony colony, CitizenRecord citizen) => mode switch
    {
        Mode.Service => Lang.Get("createacolony:activity-priest-service"),
        Mode.Funeral => Lang.Get("createacolony:activity-priest-funeral", colony.Citizens.Find(c => c.CitizenId == deadId)?.Name ?? ""),
        Mode.Fetch => Lang.Get("createacolony:activity-priest-fetch"),
        Mode.Write => Lang.Get("createacolony:activity-priest-write"),
        _ => Lang.Get("createacolony:activity-priest-idle")
    };
}
