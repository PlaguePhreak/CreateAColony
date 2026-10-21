using System.Linq;
using CreateAColony.Colonies;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace CreateAColony.Settlers;

/// <summary>
/// A guard follows the worker it escorts, or stays by the Town Hall, and fights the monsters that
/// come near: it walks up to them and strikes with its weapon (or its fists) until they fall or flee.
/// Without a weapon it fetches the best one it can find in the warehouse first.
/// </summary>
public class AiTaskSettlerGuard : SettlerTaskBase
{
    const float ThinkSeconds = 0.5f;
    const float Reach = 2.4f;
    /// <summary>An escorting guard keeps within this distance of its worker.</summary>
    const float FollowDistance = 4f;
    /// <summary>A guard at the Town Hall wanders no further than this from it.</summary>
    const float PostDistance = 10f;
    const float SwingSeconds = 0.6f;

    enum Mode { Idle, Fight, Follow, Post, FetchWeapon }

    Mode mode;
    Entity? threat;
    Entity? protectee;
    BlockPos? weaponChest;
    Vec3d? walkingTo;
    float thinkIn;
    float cooldown;
    float swingLeft;
    string? swingAnim;

    public AiTaskSettlerGuard(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig) { }

    public override bool ShouldExecute() => manager != null && Citizen?.JobCode == Jobs.Guard && Colony != null;

    public override void StartExecute()
    {
        base.StartExecute();
        thinkIn = 0;
        walkingTo = null;
        mode = Mode.Idle;
    }

    public override bool CanContinueExecute() => true;

    public override bool ContinueExecute(float dt)
    {
        var colony = Colony;
        var citizen = Citizen;
        if (colony == null || citizen == null || citizen.JobCode != Jobs.Guard || !citizen.Alive) return false;

        cooldown -= dt;
        if (swingAnim != null && (swingLeft -= dt) <= 0)
        {
            StopAnimation(swingAnim);
            swingAnim = null;
        }
        if ((thinkIn -= dt) <= 0)
        {
            thinkIn = ThinkSeconds;
            Think(colony, citizen);
        }

        if (walkingTo != null)
        {
            if (Arrived || PathFailed)
            {
                StopWalking();
                walkingTo = null;
                if (mode == Mode.FetchWeapon) TakeWeapon(colony, citizen);
            }
            else
            {
                KeepWalking();
            }
        }

        if (mode == Mode.Fight && threat != null && cooldown <= 0 && entity.Pos.DistanceTo(threat.Pos.XYZ) <= Reach) Strike(threat);
        return true;
    }

    /// <summary>Picks what to do: fight, follow, fetch a weapon, or go back to the post.</summary>
    void Think(Colony colony, CitizenRecord citizen)
    {
        var m = manager!;
        HoldBestWeapon(colony, citizen);

        var escorted = m.EscortedBy(colony, citizen);
        protectee = escorted == null ? null : entity.World.GetEntityById(escorted.EntityId);
        var guarding = protectee?.Pos.XYZ ?? colony.TownHallPos.ToVec3d().Add(0.5, 0, 0.5);

        // Fight what threatens whoever the guard looks after, or the guard itself.
        threat = m.FindThreat(guarding) ?? m.FindThreat(entity.Pos.XYZ);
        if (threat != null)
        {
            mode = Mode.Fight;
            if (entity.Pos.DistanceTo(threat.Pos.XYZ) > Reach) Walk(threat.Pos.XYZ, Reach - 0.6f);
            return;
        }

        if (protectee == null && !HasWeapon(citizen) && m.WarehouseWeapon(colony) is { } chest)
        {
            mode = Mode.FetchWeapon;
            weaponChest = chest;
            Walk(chest.ToVec3d().Add(0.5, 0, 0.5), 1.5f);
            return;
        }

        if (protectee != null)
        {
            mode = Mode.Follow;
            if (entity.Pos.DistanceTo(guarding) > FollowDistance) Walk(guarding, FollowDistance - 1.5f);
            return;
        }

        mode = Mode.Post;
        if (entity.Pos.DistanceTo(guarding) > PostDistance) Walk(guarding, PostDistance / 2);
    }

    /// <summary>Walks somewhere, planning a new path only when the goal has moved since the last one.</summary>
    void Walk(Vec3d to, float range)
    {
        if (walkingTo != null && walkingTo.SquareDistanceTo(to) < 9) return;
        walkingTo = to.Clone();
        WalkTo(to, range);
    }

    void Strike(Entity target)
    {
        StopWalking();
        walkingTo = null;
        entity.Pos.Yaw = (float)System.Math.Atan2(target.Pos.X - entity.Pos.X, target.Pos.Z - entity.Pos.Z);

        var hand = Hand;
        var weapon = hand?.Itemstack is { } held && ColonyManager.IsWeapon(held) ? held : null;
        float damage = weapon != null ? ColonyManager.WeaponDamage(weapon) : manager!.Config.GuardFistDamage;
        target.ReceiveDamage(new DamageSource
        {
            Source = EnumDamageSource.Entity,
            SourceEntity = entity,
            CauseEntity = entity,
            Type = weapon != null ? EnumDamageType.SlashingAttack : EnumDamageType.BluntAttack,
            DamageTier = weapon?.Collectible.ToolTier ?? 0
        }, damage);
        if (weapon != null && hand != null)
        {
            ColonyManager.WearTool(entity, hand);
            SyncHand();
        }

        if (swingAnim != null) StopAnimation(swingAnim);
        swingAnim = PlayShapeAnimation(hand?.Itemstack?.Collectible.GetHeldTpHitAnimation(hand, entity) ?? "breakhand", "breakhand");
        swingLeft = SwingSeconds;
        cooldown = manager!.Config.GuardSecondsPerHit;
    }

    bool HasWeapon(CitizenRecord citizen) =>
        (Hand?.Itemstack is { } held && ColonyManager.IsWeapon(held)) || manager!.PackCount(citizen, ColonyManager.IsWeapon) > 0;

    /// <summary>Holds the hardest-hitting weapon it carries.</summary>
    void HoldBestWeapon(Colony colony, CitizenRecord citizen)
    {
        var hand = Hand;
        if (hand == null) return;
        var best = manager!.PackItems(citizen).Where(ColonyManager.IsWeapon).OrderByDescending(ColonyManager.WeaponDamage).FirstOrDefault();
        if (best == null) return;
        if (hand.Itemstack is { } held && ColonyManager.IsWeapon(held) && ColonyManager.WeaponDamage(held) >= ColonyManager.WeaponDamage(best)) return;
        var weapon = manager.TakeFromPack(colony, citizen, s => s.Collectible.Code.Equals(best.Collectible.Code), 1);
        if (weapon == null) return;
        PutAwayHand(colony, citizen);
        hand.Itemstack = weapon;
        SyncHand();
    }

    /// <summary>Takes the hardest-hitting weapon from the warehouse chest it walked to.</summary>
    void TakeWeapon(Colony colony, CitizenRecord citizen)
    {
        mode = Mode.Idle;
        if (weaponChest == null || entity.World.BlockAccessor.GetBlockEntity(weaponChest) is not Vintagestory.GameContent.BlockEntityContainer chest) return;
        var best = chest.Inventory.Where(s => !s.Empty && ColonyManager.IsWeapon(s.Itemstack))
            .OrderByDescending(s => ColonyManager.WeaponDamage(s.Itemstack!)).FirstOrDefault();
        if (best == null) return;
        var weapon = best.TakeOut(1);
        best.MarkDirty();
        manager!.AddToPack(colony, citizen, weapon);
        if (weapon.StackSize > 0) manager.PutItems(chest, weapon); // inventory full
        HoldBestWeapon(colony, citizen);
    }

    public override void FinishExecute(bool cancelled)
    {
        if (swingAnim != null) StopAnimation(swingAnim);
        swingAnim = null;
        walkingTo = null;
        base.FinishExecute(cancelled);
    }

    public override string Describe(Colony colony, CitizenRecord citizen)
    {
        return mode switch
        {
            Mode.Fight => Lang.Get("createacolony:activity-guard-fight", threat?.GetName() ?? ""),
            Mode.Follow => Lang.Get("createacolony:activity-guard-escort", manager!.EscortedBy(colony, citizen)?.Name ?? ""),
            Mode.FetchWeapon => Lang.Get("createacolony:activity-guard-weapon"),
            _ => Lang.Get("createacolony:activity-guard-post")
        };
    }
}
