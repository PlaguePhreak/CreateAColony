using System.Collections.Generic;
using System.Linq;
using CreateAColony.Colonies;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using ItemMatch = System.Func<Vintagestory.API.Common.ItemStack, bool>;

namespace CreateAColony.Settlers;

/// <summary>
/// A hunter works from a Hunter's Hut (ColonyManager.Hunting.cs). It chases animals around the hut and strikes
/// them with its spear or other weapon until they fall, then skins them with its knife and takes the meat, hide
/// and the rest to the hut. An animal it cannot catch in time it lets go for a while. Without a knife it leaves
/// its kills for settlers without a job to skin.
/// </summary>
public class AiTaskSettlerHunter : AiTaskSettlerWorker
{
    const long LetGoMs = 300_000;

    long preyId;
    bool skinning;
    long chaseStartMs;
    readonly Dictionary<long, long> letGo = new();

    public AiTaskSettlerHunter(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig) { }

    protected override string Job => Jobs.Hunter;
    protected override string HutType => BuildingTypes.Hunter;
    protected override EnumTool Tool => EnumTool.Spear;
    protected override ItemMatch Stock => _ => false;
    protected override bool IsToolStack(ItemStack? stack) => stack != null && ColonyManager.IsWeapon(stack);
    protected override bool AlsoKeeps(ItemStack stack) => ColonyManager.IsKnife(stack);
    protected override bool WantsInHand(ItemStack? stack) => skinning ? ColonyManager.IsKnife(stack) : IsToolStack(stack);

    protected override float WorkSeconds => skinning
        ? (Hand is { } hand && entity.World.GetEntityById(preyId) is { } carcass ? ColonyManager.SkinSeconds(carcass, hand, entity) : 1f)
        : manager!.Config.HunterSecondsPerHit / (Colony is { } c && Citizen is { } me ? manager.SwingFactor(c, me) : 1f);

    bool LetGo(long id)
    {
        if (!letGo.TryGetValue(id, out long until)) return false;
        if (entity.World.ElapsedMilliseconds < until) return true;
        letGo.Remove(id);
        return false;
    }

    bool HasKnife(CitizenRecord citizen) => ColonyManager.IsKnife(Hand?.Itemstack) || manager!.PackCount(citizen, s => ColonyManager.IsKnife(s)) > 0;

    protected override bool PlanWork(Colony colony, CitizenRecord citizen, Building hut, List<BlockEntityContainer> storage)
    {
        var m = manager!;
        long now = entity.World.ElapsedMilliseconds;
        bool knife = HasKnife(citizen);
        if (!knife && storage.FirstOrDefault(c => c.Inventory.Any(s => !s.Empty && ColonyManager.IsKnife(s.Itemstack))) is { } knifeChest)
        {
            return FetchSupplies(knifeChest.Pos, s => ColonyManager.IsKnife(s), 1);
        }
        // Its own kills first, before they rot.
        if (knife && !m.PackFull(citizen) && m.HunterCarcass(colony, hut, entity.Pos.XYZ, LetGo) is { } carcass)
        {
            skinning = true;
            preyId = carcass.EntityId;
            m.ClaimCarcass(carcass, entity);
            workRange = 2f;
            SetStatus("hunter-skinning");
            return GoWork(carcass.Pos.AsBlockPos);
        }
        if (m.PackFull(citizen)) return false;

        skinning = false;
        var prey = entity.World.GetEntityById(preyId);
        if (prey == null || !m.IsPrey(colony, hut, prey) || LetGo(preyId))
        {
            prey = m.FindPrey(colony, hut, entity.Pos.XYZ, LetGo);
            if (prey == null)
            {
                preyId = 0;
                SetStatus("hunter-nothing");
                return false;
            }
            preyId = prey.EntityId;
            chaseStartMs = now;
        }
        if (now - chaseStartMs > m.Config.HunterChaseSeconds * 1000)
        {
            // It got away: leave it be for a while.
            letGo[preyId] = now + LetGoMs;
            preyId = 0;
            SetStatus("hunter-lost");
            return false;
        }
        workRange = 2.2f;
        SetStatus("hunting");
        return GoWork(prey.Pos.AsBlockPos);
    }

    /// <summary>The animal moves: walk to where it is now.</summary>
    protected override Vec3d WalkGoal() => entity.World.GetEntityById(preyId)?.Pos.XYZ ?? target.ToVec3d().Add(0.5, 0, 0.5);

    protected override bool CloseEnough() => entity.World.GetEntityById(preyId) is { } e
        ? entity.Pos.DistanceTo(e.Pos.XYZ) <= workRange + 0.5
        : base.CloseEnough();

    protected override string WorkAnimation(ItemSlot? hand) => skinning ? "knifecut" : base.WorkAnimation(hand);

    protected override void DoWork(Colony colony, CitizenRecord citizen, Building hut)
    {
        var m = manager!;
        var e = entity.World.GetEntityById(preyId);
        if (e == null) return;
        if (skinning)
        {
            if (!ColonyManager.Skinnable(e) || Hand is not { } hand) return;
            bool done = e.GetBehavior<EntityBehaviorHarvestable>()?.IsHarvested == true;
            if (!done && !ColonyManager.IsKnife(hand.Itemstack)) return;
            m.Skin(colony, citizen, entity, e, hand);
            SyncHand();
            return;
        }
        // Too far: it ran off while the hunter swung. Plan walks after it again.
        if (!e.Alive || entity.Pos.DistanceTo(e.Pos.XYZ) > workRange + 1) return;
        entity.Pos.Yaw = (float)System.Math.Atan2(e.Pos.X - entity.Pos.X, e.Pos.Z - entity.Pos.Z);
        if (m.HunterStrike(colony, citizen, hut, entity, e)) chaseStartMs = entity.World.ElapsedMilliseconds;
        SyncHand();
    }

    protected override string DescribeWork() => Lang.Get(skinning ? "createacolony:activity-hunter-skin" : "createacolony:activity-hunter-hunt");
}
