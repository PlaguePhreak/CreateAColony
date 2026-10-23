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
        float hunger = (float)(config.HungerPerDay * manager.TraitMult(citizen, "hunger") * manager.HungerPerkFor(settler.ColonyId, citizen) / 24.0 * hours); // Light Eater, Glutton, Fitness perk
        citizen.Saturation = System.Math.Max(0, citizen.Saturation - hunger);
        entity.WatchedAttributes.SetFloat("createacolony:saturation", citizen.Saturation);
        // The food groups drain with hunger too, as a player's nutrition bars do.
        for (int i = 0; i < citizen.Nutrition.Count; i++) citizen.Nutrition[i] = System.Math.Max(0, citizen.Nutrition[i] - hunger);
        UpdateWetness(hours);

        // Vanilla health regen ignores hunger for non-players, so slow it down ourselves the same way
        // players work: full speed above 75% food, tapering to nothing when starving.
        float regen = (float)System.Math.Clamp(citizen.Saturation / config.SaturationMax / 0.75, 0, 1) * manager.TraitMult(citizen, "regen"); // Fast Healer
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

    /// <summary>The settler's wetness, 0 (dry) to 1 (soaked), shown on the Stats tab like a player's.</summary>
    public const string WetnessAttr = "createacolony:wetness";

    /// <summary>Rain on a settler with nothing over its head soaks it; under a roof it dries off within a couple of hours.</summary>
    void UpdateWetness(double hours)
    {
        if (hours <= 0) return;
        var pos = entity.Pos.AsBlockPos;
        var ba = entity.World.BlockAccessor;
        float wet = entity.WatchedAttributes.GetFloat(WetnessAttr);
        bool exposed = ba.GetRainMapHeightAt(pos.X, pos.Z) <= pos.Y;
        float rain = exposed ? ba.GetClimateAt(pos, EnumGetClimateMode.NowValues)?.Rainfall ?? 0 : 0;
        float next = rain > 0.05f ? wet + rain * (float)hours * 2 : wet - (float)hours * 0.5f;
        next = System.Math.Clamp(next, 0, 1);
        if (System.Math.Abs(next - wet) > 0.001f) entity.WatchedAttributes.SetFloat(WetnessAttr, next);
    }
}
