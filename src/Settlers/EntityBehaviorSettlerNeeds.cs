using CreateAColony.Colonies;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

namespace CreateAColony.Settlers;

/// <summary>
/// Hunger for settlers. Saturation lives on the CitizenRecord (so it survives respawns) and drains with
/// in-game time while the settler is loaded. At zero the settler starves slowly; the eat task refills it.
/// </summary>
public class EntityBehaviorSettlerNeeds : EntityBehavior
{
    const float UpdateIntervalSeconds = 2f;
    const string LastUpdateAttr = "createacolony:needsLastTotalHours";

    const float StarvationTickDamage = 0.5f;

    ColonyManager? manager;
    float sinceUpdate;
    float pendingStarvationDamage;

    public EntityBehaviorSettlerNeeds(Entity entity) : base(entity) { }

    public override string PropertyName() => "settlerneeds";

    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
        base.Initialize(properties, attributes);
        if (entity.Api.Side == EnumAppSide.Server) manager = entity.Api.ModLoader.GetModSystem<ColonyManager>();
    }

    public override void OnGameTick(float deltaTime)
    {
        if (manager == null || !entity.Alive) return;
        sinceUpdate += deltaTime;
        if (sinceUpdate < UpdateIntervalSeconds) return;
        sinceUpdate = 0;

        if (entity is not EntitySettler settler) return;
        var citizen = manager.FindCitizen(settler.ColonyId, settler.CitizenId);
        if (citizen == null) return;

        var config = manager.Config;
        double nowHours = entity.World.Calendar.TotalHours;
        double lastHours = entity.WatchedAttributes.GetDouble(LastUpdateAttr, nowHours);
        entity.WatchedAttributes.SetDouble(LastUpdateAttr, nowHours);
        // Cap the step so a long unload or a /time jump does not starve a settler instantly.
        double hours = System.Math.Clamp(nowHours - lastHours, 0, 6);

        if (citizen.Saturation < 0) citizen.Saturation = config.SaturationMax;
        citizen.Saturation = System.Math.Max(0, citizen.Saturation - (float)(config.HungerPerDay / 24.0 * hours));
        entity.WatchedAttributes.SetFloat("createacolony:saturation", citizen.Saturation);

        // Vanilla health regen ignores hunger for non-players, so slow it down ourselves the same way
        // players work: full speed above 75% food, tapering to nothing when starving.
        float regen = (float)System.Math.Clamp(citizen.Saturation / config.SaturationMax / 0.75, 0, 1);
        if (System.Math.Abs(entity.WatchedAttributes.GetFloat("regenSpeed", 1) - regen) > 0.01f)
        {
            entity.WatchedAttributes.SetFloat("regenSpeed", regen);
        }

        if (citizen.Saturation <= 0 && hours > 0)
        {
            // Apply starvation in half-heart steps so the hurt animation does not play every few seconds.
            pendingStarvationDamage += (float)(config.StarvationDamagePerHour * hours);
            if (pendingStarvationDamage >= StarvationTickDamage)
            {
                entity.ReceiveDamage(new DamageSource { Source = EnumDamageSource.Internal, Type = EnumDamageType.Hunger }, pendingStarvationDamage);
                pendingStarvationDamage = 0;
            }
        }
        else
        {
            pendingStarvationDamage = 0;
        }
    }

    public bool IsHungry(ColonyConfig config, CitizenRecord citizen)
    {
        return citizen.Saturation >= 0 && citizen.Saturation < config.SaturationMax * config.EatBelowFraction;
    }
}
