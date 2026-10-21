namespace CreateAColony;

/// <summary>Server config, stored in ModConfig/createacolony.json.</summary>
public class ColonyConfig
{
    public const string FileName = "createacolony.json";

    /// <summary>The mod is designed around permadeath. When false, a killed settler is
    /// knocked out instead and returns to the Town Hall after DownedRecoveryDays.</summary>
    public bool Permadeath = true;
    public double DownedRecoveryDays = 1.0;

    /// <summary>Settlers that arrive when a colony is founded (rolled between min and max).</summary>
    public int StartingSettlersMin = 3;
    public int StartingSettlersMax = 4;

    public int MaxConcurrentPathRequestsPerColony = 4;

    // Needs
    /// <summary>Full stomach. A settler starts here and eats when below EatBelowFraction of it.</summary>
    public float SaturationMax = 1200f;
    /// <summary>Saturation lost per in-game day; 600 means a full settler gets hungry in about a day and a half.</summary>
    public float HungerPerDay = 600f;
    public float EatBelowFraction = 0.4f;
    /// <summary>Health lost per in-game hour while saturation is zero. Settlers have 20 health.</summary>
    public float StarvationDamagePerHour = 0.5f;

    // Sleep
    public float SleepStartHour = 21f;
    public float SleepEndHour = 6f;

    // Storms
    /// <summary>Settlers head home this many in-game hours before a temporal storm starts.</summary>
    public float StormWarningHours = 2f;

    // Respawn
    /// <summary>A settler not seen for this long while the Town Hall area is loaded is treated as lost and respawned.</summary>
    public double RespawnMissingAfterDays = 0.5;

    // Construction
    /// <summary>When false, builders conjure blocks without materials. Handy for creative testing.</summary>
    public bool BuildersNeedMaterials = true;
    /// <summary>Seconds a builder takes per block placed or cleared.</summary>
    public float BuilderSecondsPerBlock = 0.75f;
    /// <summary>Builders need a shovel, axe or pickaxe of the right tier to clear blocks, and wear it out.</summary>
    public bool BuildersNeedTools = true;
}
