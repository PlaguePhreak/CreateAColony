namespace CreateAColony.Colonies;

/// <summary>The tabs of the Builder's Wand building list, in order, and the building types on each.</summary>
public static class BuildingCategories
{
    public record Category(string Code, string[] Types);

    /// <summary>Tabs with no types yet still show, as placeholders for buildings to come.</summary>
    public static readonly Category[] All =
    {
        new("fundamentals", new[] { BuildingTypes.TownHall, BuildingTypes.Builder, BuildingTypes.Tavern }),
        new("agriculture", new[] { BuildingTypes.Farmer, BuildingTypes.Forester }),
        new("craftsmanship", new[] { BuildingTypes.Potter, BuildingTypes.Cook }),
        new("education", System.Array.Empty<string>()),
        new("infrastructure", new[] { BuildingTypes.Warehouse }),
        new("military", System.Array.Empty<string>()),
        new("walls", System.Array.Empty<string>()),
        new("decoration", System.Array.Empty<string>()),
    };
}
