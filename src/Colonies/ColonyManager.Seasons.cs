using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using ItemMatch = System.Func<Vintagestory.API.Common.ItemStack, bool>;

namespace CreateAColony.Colonies;

/// <summary>
/// Farmers only sow a crop that will live until it is ripe (Aaron, 0.147.0). The game kills an unripe crop after
/// about 48 hours below its cold limit or above its heat limit (BlockEntityFarmland.updateCropDamage), and slows its
/// growth when it is cool. Before sowing, the farmer works the crop's growing time out ahead, hour by hour, with the
/// same climate the game will use for that spot on those days (seasons, the region's warmth, day and night, +5 in a
/// greenhouse). A crop that would take too much cold or heat damage on the way is not sown. Nothing is sown in winter.
/// Jobless settlers' home plots follow the same rule.
/// </summary>
public partial class ColonyManager
{
    const double SurviveStepHours = 3;
    /// <summary>Hours of cold or heat damage that kill an unripe crop in the game.</summary>
    const float CropDeathHours = 48f;
    /// <summary>A greenhouse makes a crop this much warmer, as in the game.</summary>
    const float GreenhouseWarmth = 5f;

    /// <summary>Whether a crop lives until ripe if sown in this area today, by crop, area, greenhouse and soil.</summary>
    readonly Dictionary<(int Crop, int X, int Y, int Z, bool Glass, int Rate), (double Day, bool Ok)> cropSurvives = new();
    List<ItemStack>? allSeeds;
    ModSystemFarming? farming;

    /// <summary>Winter where this spot is (the game knows the hemisphere).</summary>
    public bool IsWinterAt(BlockPos pos) => sapi.World.Calendar.GetSeason(pos) == EnumSeason.Winter;

    /// <summary>The seeds of match that may be sown on this ground today: never in winter, and only crops that live until ripe.</summary>
    public ItemMatch SowableNow(BlockPos ground, ItemMatch match) => s => match(s) && CropFor(s) is { } crop && CropSurvives(crop, ground);

    /// <summary>Why nothing that match allows may be sown on this ground today: "winter", "weather", or null when some crop would do.</summary>
    public string? NoSowingReason(BlockPos ground, ItemMatch match)
    {
        if (!Config.FarmerChecksSeasons) return null;
        if (Config.FarmerSkipsWinter && IsWinterAt(ground)) return "winter";
        return AllSeeds().Any(SowableNow(ground, match)) ? null : "weather";
    }

    /// <summary>One of every seed in the game that a farmer can sow.</summary>
    List<ItemStack> AllSeeds() => allSeeds ??= sapi.World.Items
        .Where(i => i?.Code != null && i is ItemPlantableSeed)
        .Select(i => new ItemStack(i))
        .Where(IsSowable)
        .ToList();

    /// <summary>True when a crop sown on this ground today would live until it is ripe.</summary>
    public bool CropSurvives(Block crop, BlockPos ground)
    {
        if (!Config.FarmerChecksSeasons) return true;
        if (Config.FarmerSkipsWinter && IsWinterAt(ground)) return false;
        var props = crop.CropProps;
        if (props == null) return false;
        var farmland = sapi.World.BlockAccessor.GetBlockEntity(ground) as BlockEntityFarmland;
        bool glass = farmland?.Roomness > 0;
        // The farmer waters dry soil, so only the nutrients count, and never below half speed.
        float rate = Math.Clamp(farmland?.GetGrowthRate(props.RequiredNutrient) ?? 1f, 0.5f, 1.5f);
        var key = (crop.Id, ground.X >> 4, ground.Y >> 4, ground.Z >> 4, glass, (int)(rate * 4));
        double today = Math.Floor(sapi.World.Calendar.TotalDays);
        if (cropSurvives.TryGetValue(key, out var known) && known.Day == today) return known.Ok;
        bool ok = LivesUntilRipe(props, ground, glass, rate);
        cropSurvives[key] = (today, ok);
        return ok;
    }

    /// <summary>
    /// Walks a crop's growing time ahead in 3 hour steps with the game's own climate for each date, growing as the
    /// game grows it (slower when cool) and adding up cold and heat damage as the game does. False when the damage
    /// would pass the safe share of what kills a crop, or the crop would not ripen within a year.
    /// </summary>
    bool LivesUntilRipe(BlockCropProperties props, BlockPos pos, bool glass, float rate)
    {
        var cal = sapi.World.Calendar;
        float days = props.TotalGrowthDays > 0 ? props.TotalGrowthDays / 12f * cal.DaysPerMonth : props.TotalGrowthMonths * cal.DaysPerMonth;
        float mul = (float)sapi.World.Config.GetDecimal("cropGrowthRateMul", 1);
        // Each stage takes 0.9 to 1.1 times its share in the game; plan for the slow end.
        double needHours = cal.HoursPerDay * days * 1.1 / rate / Math.Max(0.01f, mul);
        var fc = (farming ??= sapi.ModLoader.GetModSystem<ModSystemFarming>())?.Config;
        float slowBelow = fc?.DelayGrowthBelowTemperature ?? 10f;
        float lossPerDegree = fc?.LossPerDegree ?? 0.1f;
        float allowed = CropDeathHours * Math.Clamp(Config.FarmerCropDamageAllowed, 0f, 1f);
        float margin = Config.FarmerSeasonMarginDegrees;
        double limit = cal.DaysPerYear * cal.HoursPerDay;

        var ba = sapi.World.BlockAccessor;
        double start = cal.TotalHours;
        double hours = 0, grown = 0;
        float cold = 0, heat = 0;
        ClimateCondition? climate = null;
        while (grown < needHours)
        {
            if (hours > limit) return false;
            hours += SurviveStepHours;
            double day = (start + hours) / cal.HoursPerDay;
            climate = climate == null
                ? ba.GetClimateAt(pos, EnumGetClimateMode.ForSuppliedDate_TemperatureRainfallOnly, day)
                : ba.GetClimateAt(pos, climate, EnumGetClimateMode.ForSuppliedDate_TemperatureRainfallOnly, day);
            if (climate == null) return true; // no climate here: the game cannot hurt it either
            float t = climate.Temperature + (glass ? GreenhouseWarmth : 0);
            grown += SurviveStepHours * Math.Clamp(1 + (t - slowBelow) * lossPerDegree, 0, 1);
            cold = t < props.ColdDamageBelow + margin ? cold + (float)SurviveStepHours : Math.Max(0, cold - (float)SurviveStepHours / 10);
            heat = t > props.HeatDamageAbove - margin ? heat + (float)SurviveStepHours : Math.Max(0, heat - (float)SurviveStepHours / 10);
            if (cold > allowed || heat > allowed) return false;
        }
        return true;
    }
}
