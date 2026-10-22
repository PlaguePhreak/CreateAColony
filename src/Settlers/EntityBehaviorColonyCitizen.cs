using System.Linq;
using CreateAColony.Colonies;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace CreateAColony.Settlers;

/// <summary>
/// Links a settler entity to its CitizenRecord. Keeps the record's last-seen position fresh
/// and marks the citizen dead when the entity dies, so the colony knows to replace it.
/// </summary>
public class EntityBehaviorColonyCitizen : EntityBehavior
{
    const float SyncIntervalSeconds = 5f;
    const float StuckCheckSeconds = 1f;
    const float DoorCheckSeconds = 0.2f;

    ColonyManager? manager;
    /// <summary>
    /// Holding right-click repeats the interaction. After a bag changes hands, further clicks from that
    /// player are ignored briefly, so the same click does not also open the window or give a second bag.
    /// Kept per settler, not shared: the game clock restarts at zero when a world is loaded again, and a
    /// time left over from the last session would block clicks for as long as that session had run.
    /// </summary>
    const long BagCooldownMs = 700;
    readonly System.Collections.Generic.Dictionary<string, long> lastBagChangeMs = new();
    float sinceSync;
    float sinceStuckCheck;
    float sinceDoorCheck;
    int stuckChecks;
    int willpowerLogTicks;

    /// <summary>How long this settler has been stuck, and when it relocates itself (see Willpower.cs). Server only.</summary>
    public Willpower Willpower { get; }

    public EntityBehaviorColonyCitizen(Entity entity) : base(entity)
    {
        Willpower = new Willpower(entity);
    }

    public override string PropertyName() => "colonycitizen";

    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
        base.Initialize(properties, attributes);
        if (entity.Api.Side == EnumAppSide.Server)
        {
            manager = entity.Api.ModLoader.GetModSystem<ColonyManager>();
        }
    }

    public override void OnGameTick(float deltaTime)
    {
        if (manager == null || !entity.Alive) return;
        sinceDoorCheck += deltaTime;
        if (sinceDoorCheck >= DoorCheckSeconds)
        {
            sinceDoorCheck = 0;
            manager.OpenDoorsAhead(entity);
        }
        sinceStuckCheck += deltaTime;
        if (sinceStuckCheck >= StuckCheckSeconds)
        {
            sinceStuckCheck = 0;
            FreeIfInsideBlock();
            TickWillpower(StuckCheckSeconds);
        }
        sinceSync += deltaTime;
        if (sinceSync < SyncIntervalSeconds) return;
        sinceSync = 0;

        var record = FindRecord();
        if (record == null)
        {
            // A visitor who left or was turned away while this entity was unloaded.
            if (entity is EntitySettler { ColonyId: { } colonyId } && manager.GetById(colonyId) != null) entity.Die(EnumDespawnReason.Removed);
            return;
        }

        if (record.EntityId != entity.EntityId)
        {
            // The colony respawned this settler while we were unloaded. The newer entity wins.
            var other = entity.World.GetEntityById(record.EntityId);
            if (other != null && other.Alive)
            {
                entity.Die(EnumDespawnReason.Removed);
                return;
            }
            record.EntityId = entity.EntityId;
        }

        record.LastSeenPos = entity.Pos.XYZ.Clone();
        record.LastSeenTotalDays = entity.World.Calendar.TotalDays;
    }

    public override void OnEntityDeath(DamageSource damageSourceForDeath)
    {
        var record = FindRecord();
        if (record != null && manager != null)
        {
            // What they carried, bags included, falls where they died instead of vanishing with them.
            var colony = manager.GetById(((EntitySettler)entity).ColonyId!);
            if (colony != null)
            {
                foreach (var stack in manager.EmptyPackAndBags(colony, record).Concat(manager.TakeOffArmor(colony, record)))
                {
                    entity.World.SpawnItemEntity(stack, entity.Pos.XYZ.Add(0, 0.5, 0));
                }
            }

            if (record.Visitor)
            {
                // A wanderer who dies before joining was never one of ours: no grave in the settler list.
                if (colony != null) manager.ForgetCitizen(colony, record);
            }
            else if (colony != null && manager.Permadeath(colony))
            {
                record.Alive = false;
            }
            else
            {
                // Knocked out: the respawn pass (milestone 2) brings them back to the Town Hall after this time.
                record.DownedUntilTotalDays = entity.World.Calendar.TotalDays + manager.Config.DownedRecoveryDays;
            }
        }
        base.OnEntityDeath(damageSourceForDeath);
    }

    public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode, ref EnumHandling handled)
    {
        // MVP: show who this is and their job. The citizen dialog replaces this later.
        if (mode != EnumInteractMode.Interact || manager == null) return;
        if (byEntity is not EntityPlayer { Player: IServerPlayer player }) return;

        var record = FindRecord();
        var colony = record == null ? null : manager.GetById(((EntitySettler)entity).ColonyId!);
        if (record == null || colony == null) return;
        handled = EnumHandling.PreventDefault;
        long now = entity.World.ElapsedMilliseconds;
        if (lastBagChangeMs.TryGetValue(player.PlayerUID, out long last) && now >= last && now - last < BagCooldownMs) return;

        // Members hand a settler a bag by right-clicking with it, and take one back with sneak and an empty hand.
        bool member = colony.RoleOf(player.PlayerUID) >= ColonyRole.Member;
        if (member && itemslot?.Itemstack is { } held && ColonyManager.BagSlots(held) > 0)
        {
            if (manager.AddBag(colony, record, held))
            {
                itemslot.TakeOut(1);
                itemslot.MarkDirty();
                lastBagChangeMs[player.PlayerUID] = now;
                player.SendMessage(0, Lang.Get("createacolony:bag-given", record.Name, manager.PackSlots(record)), EnumChatType.Notification);
            }
            else
            {
                player.SendMessage(0, Lang.Get("createacolony:bag-full", record.Name, ColonyManager.MaxBags), EnumChatType.Notification);
            }
            return;
        }
        if (member && itemslot?.Empty != false && byEntity.Controls.Sneak && manager.RemoveBag(colony, record) is { } removed)
        {
            lastBagChangeMs[player.PlayerUID] = now;
            if (!player.InventoryManager.TryGiveItemstack(removed.Bag, true)) entity.World.SpawnItemEntity(removed.Bag, entity.Pos.XYZ);
            foreach (var stack in removed.Overflow) entity.World.SpawnItemEntity(stack, entity.Pos.XYZ.Add(0, 0.5, 0));
            player.SendMessage(0, Lang.Get("createacolony:bag-taken", record.Name, manager.PackSlots(record)), EnumChatType.Notification);
            return;
        }

        entity.Api.ModLoader.GetModSystem<Network.ColonyNetwork>().OpenCitizenWindow(player, colony, record);
    }

    /// <summary>A settler inside a block (someone built over it) for two checks in a row is moved to the nearest free spot.</summary>
    void FreeIfInsideBlock()
    {
        if (!entity.World.CollisionTester.IsColliding(entity.World.BlockAccessor, entity.CollisionBox, entity.Pos.XYZ, false))
        {
            stuckChecks = 0;
            return;
        }
        if (++stuckChecks < 2) return;
        stuckChecks = 0;
        var spot = manager!.FreeSpotNear(entity, entity.Pos.AsBlockPos, 4);
        if (spot != null) entity.TeleportToDouble(spot.X, spot.Y, spot.Z);
    }

    /// <summary>Once a second: build or lose Willpower, show it when debugging, relocate when it is full.</summary>
    void TickWillpower(float seconds)
    {
        if (!manager!.Config.Willpower || entity is not EntityAgent agent) return;
        bool full = Willpower.Tick(seconds, manager);
        manager.ShowWillpower(entity, Willpower);
        if (manager.PathDebug && Willpower.Reason != StuckReason.None && ++willpowerLogTicks >= 5)
        {
            willpowerLogTicks = 0;
            entity.Api.Logger.Notification("[willpower] {0} at {1}: stuck ({2}{3}), willpower {4:0}/{5:0}", FindRecord()?.Name ?? entity.EntityId.ToString(),
                entity.Pos.AsBlockPos, Willpower.Reason, Willpower.Trapped ? ", trapped" : "", Willpower.Value, Willpower.Threshold);
        }
        if (!full || agent.MountedOn != null) return;
        manager.Relocate(agent, Willpower, FindRecord()?.JobCode);
    }

    CitizenRecord? FindRecord()
    {
        if (manager == null || entity is not EntitySettler settler || settler.ColonyId == null) return null;
        return manager.GetById(settler.ColonyId)?.Citizens.Find(c => c.CitizenId == settler.CitizenId);
    }
}
