using CreateAColony.Colonies;
using Vintagestory.API.Common;
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

    ColonyManager? manager;
    float sinceSync;

    public EntityBehaviorColonyCitizen(Entity entity) : base(entity) { }

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
        sinceSync += deltaTime;
        if (sinceSync < SyncIntervalSeconds) return;
        sinceSync = 0;

        var record = FindRecord();
        if (record == null) return;

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
            if (manager.Config.Permadeath)
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
        if (record == null) return;
        player.SendMessage(0, $"{record.Name}: {record.JobCode ?? "unemployed"}", EnumChatType.Notification);
        handled = EnumHandling.PreventDefault;
    }

    CitizenRecord? FindRecord()
    {
        if (manager == null || entity is not EntitySettler settler || settler.ColonyId == null) return null;
        return manager.GetById(settler.ColonyId)?.Citizens.Find(c => c.CitizenId == settler.CitizenId);
    }
}
