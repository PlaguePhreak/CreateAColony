namespace CreateAColony.Colonies;

/// <summary>The tabs of the Builder's Wand building list, in order, and the building types on each.</summary>
public static class BuildingCategories
{
    public record Category(string Code, string[] Types);

    /// <summary>Tabs with no types yet still show, as placeholders for buildings to come.</summary>
    public static readonly Category[] All =
    {
        new("fundamentals", new[] { BuildingTypes.TownHall, BuildingTypes.Builder, BuildingTypes.Tavern, BuildingTypes.Residence }),
        new("agriculture", new[] { BuildingTypes.Farmer, BuildingTypes.Forester, BuildingTypes.Gatherer, BuildingTypes.Hunter }),
        new("craftsmanship", new[] { BuildingTypes.Potter, BuildingTypes.Cook, BuildingTypes.Smelter, BuildingTypes.Blacksmith, BuildingTypes.Sawmill }),
        new("mining", new[] { BuildingTypes.Prospector, BuildingTypes.Miner, BuildingTypes.MineshaftEntrance, BuildingTypes.Quarry }),
        new("education", System.Array.Empty<string>()),
        new("infrastructure", new[] { BuildingTypes.Warehouse, BuildingTypes.Explorer }),
        new("military", new[] { BuildingTypes.GuardTower, BuildingTypes.Barracks }),
        new("walls", System.Array.Empty<string>()),
        new("decoration", System.Array.Empty<string>()),
    };
}

/// <summary>
/// Blueprint styles, the asset packs of the Builder's Wand. Each pack has its own set of blueprints under
/// blueprints/&lt;style&gt;/; a player picks the pack they build with, and a building keeps the pack it was
/// placed in for all its levels.
/// </summary>
public static class BlueprintStyles
{
    /// <summary>MineColonies' Medieval Oak style, converted (0.48.0).</summary>
    public const string MedievalOak = "medievaloak";
    /// <summary>
    /// The old Medieval Oak (Alt) pack, MineColonies' alternative buildings. Since 0.66.0 they are the "alt" type
    /// of each building in Medieval Oak; buildings and players saved with it move there.
    /// </summary>
    public const string OldMedievalOakAlt = "medievaloakalt";
    /// <summary>The type (variant) Medieval Oak's alternative buildings are under.</summary>
    public const string AltVariant = "alt";
    public const string DevsBuilds = "devsbuilds";
    /// <summary>The old Testing style (blueprints/&lt;type&gt;/), removed in 0.46.0. Buildings saved with it move to a pack that still exists.</summary>
    public const string OldTesting = "testing";

    public static readonly string[] All = { MedievalOak, DevsBuilds };

    /// <summary>The pack for buildings and players with none (or one that no longer exists).</summary>
    public const string Default = MedievalOak;

    public static bool Exists(string? style) => style != null && System.Array.IndexOf(All, style) >= 0;

    public static string Normalize(string? style) => Exists(style) ? style! : Default;

    /// <summary>The style's folder under blueprints/.</summary>
    public static string Folder(string style) => Normalize(style);
}
