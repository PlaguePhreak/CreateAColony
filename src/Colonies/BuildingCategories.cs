namespace CreateAColony.Colonies;

/// <summary>The tabs of the Builder's Wand building list, in order, and the building types on each.</summary>
public static class BuildingCategories
{
    public record Category(string Code, string[] Types);

    /// <summary>Tabs with no types yet still show, as placeholders for buildings to come.</summary>
    public static readonly Category[] All =
    {
        new("fundamentals", new[] { BuildingTypes.TownHall, BuildingTypes.Builder, BuildingTypes.Tavern, BuildingTypes.Residence }),
        new("agriculture", new[] { BuildingTypes.Farmer, BuildingTypes.Forester }),
        new("craftsmanship", new[] { BuildingTypes.Potter, BuildingTypes.Cook }),
        new("mining", new[] { BuildingTypes.Prospector, BuildingTypes.Miner, BuildingTypes.Quarry }),
        new("education", System.Array.Empty<string>()),
        new("infrastructure", new[] { BuildingTypes.Warehouse }),
        new("military", new[] { BuildingTypes.GuardTower }),
        new("walls", System.Array.Empty<string>()),
        new("decoration", System.Array.Empty<string>()),
    };
}

/// <summary>
/// Blueprint styles, the tabs along the top of the Builder's Wand list. Each style has its own set of
/// blueprints; a building keeps the style it was placed in for all its levels.
/// </summary>
public static class BlueprintStyles
{
    public const string Testing = "testing";
    public const string DevsBuilds = "devsbuilds";

    public static readonly string[] All = { Testing, DevsBuilds };

    public static string Normalize(string? style) => string.IsNullOrEmpty(style) || System.Array.IndexOf(All, style) < 0 ? Testing : style;

    /// <summary>The style's folder under blueprints/; Testing uses the blueprints/&lt;type&gt; folders themselves.</summary>
    public static string Folder(string style) => Normalize(style) == Testing ? "" : Normalize(style);
}
