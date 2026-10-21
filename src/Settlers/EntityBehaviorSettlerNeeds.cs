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

    ColonyManager? manager;
    float sinceUpdate;

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

        if (citizen.Saturation <= 0 && hours > 0)
        {
            entity.ReceiveDamage(new DamageSource { Source = EnumDamageSource.Internal, Type = EnumDamageType.Hunger }, (float)(config.StarvationDamagePerHour * hours));
        }
    }

    public bool IsHungry(ColonyConfig config, CitizenRecord citizen)
    {
        return citizen.Saturation >= 0 && citizen.Saturation < config.SaturationMax * config.EatBelowFraction;
    }
}
