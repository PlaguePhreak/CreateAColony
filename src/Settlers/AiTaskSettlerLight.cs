using System.Collections.Generic;
using CreateAColony.Colonies;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace CreateAColony.Settlers;

/// <summary>
/// Every grown settler gets a light to carry (ColonyManager.HandLights.cs): in the dark, one without a light walks
/// to its hut storage or the warehouse for one, a free lantern before a torch. At any time it swaps its torch for a
/// lantern that has come free, and brings a lantern back for a torch when a build needs it. It drops its work for
/// the walk and goes back to it after; running from an attacker, a fire and sleep come first.
/// </summary>
public class AiTaskSettlerLight : SettlerTaskBase
{
    const long CheckMs = 5_000;
    const long UnreachableForgetMs = 120_000;

    ColonyManager.LightErrand? errand;
    BlockPos target = new(0);
    long nextCheckMs;
    bool walking;
    readonly Dictionary<BlockPos, long> unreachable = new();

    public AiTaskSettlerLight(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig) { }

    public override bool ShouldExecute()
    {
        if (manager == null || !manager.Config.CarryTorches || entity.World.ElapsedMilliseconds < nextCheckMs) return false;
        nextCheckMs = entity.World.ElapsedMilliseconds + CheckMs;
        if (Colony is not { } colony || Citizen is not { } citizen || citizen.IsDowned || CalledToArms || manager.FleeingFrom(entity) != null) return false;
        errand = manager.PlanLightErrand(colony, citizen, entity);
        if (errand is not { } e || IsUnreachable(e.Chest.Pos)) return false;
        target = e.Chest.Pos.Copy();
        return true;
    }

    bool IsUnreachable(BlockPos pos)
    {
        if (!unreachable.TryGetValue(pos, out long until)) return false;
        if (entity.World.ElapsedMilliseconds < until) return true;
        unreachable.Remove(pos);
        return false;
    }

    public override void StartExecute()
    {
        base.StartExecute();
        walking = entity.Pos.XYZ.DistanceTo(target.ToVec3d().Add(0.5, 0.5, 0.5)) > 2f;
        if (walking) WalkTo(target.ToVec3d().Add(0.5, 0, 0.5), 1.5f);
    }

    public override bool ContinueExecute(float dt)
    {
        if (Colony is not { } colony || Citizen is not { } citizen || errand is not { } e) return false;
        if (walking)
        {
            if (PathFailed)
            {
                unreachable[target.Copy()] = entity.World.ElapsedMilliseconds + UnreachableForgetMs;
                return false;
            }
            if (!Arrived)
            {
                KeepWalking();
                return true;
            }
            StopWalking();
            walking = false;
        }
        // The chest may have gone or been emptied on the way; the next check finds another.
        if (entity.World.BlockAccessor.GetBlockEntity(target) is BlockEntityContainer chest)
        {
            if (!manager!.DoLightErrand(colony, citizen, e with { Chest = chest })) unreachable[target.Copy()] = entity.World.ElapsedMilliseconds + UnreachableForgetMs;
            manager.DressEntity(citizen);
        }
        nextCheckMs = 0;
        return false;
    }

    protected override bool CountsForWillpower => false;

    public override string Describe(Colony colony, CitizenRecord citizen) => Lang.Get(errand?.Action switch
    {
        ColonyManager.LightAction.GiveBack => "createacolony:activity-light-giveback",
        ColonyManager.LightAction.Upgrade => "createacolony:activity-light-upgrade",
        _ => errand?.Lantern == true ? "createacolony:activity-light-lantern" : "createacolony:activity-fetching-torch"
    });
}
