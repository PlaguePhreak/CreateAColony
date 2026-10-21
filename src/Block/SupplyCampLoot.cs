using System.Collections.Generic;
using Vintagestory.API.Common;

namespace CreateAColony.Blocks;

/// <summary>Loaded from assets/createacolony/config/supplycamp.json so packs can change what the camp hands out.</summary>
public class SupplyCampLoot
{
    public const string AssetPath = "config/supplycamp.json";

    /// <summary>How many daily deliveries a colony gets in total.</summary>
    public int Deliveries = 5;
    public List<SupplyCampEntry> Items = new();

    public static SupplyCampLoot Load(ICoreAPI api)
    {
        var asset = api.Assets.TryGet(new AssetLocation(CreateAColonyModSystem.ModId, AssetPath));
        return asset?.ToObject<SupplyCampLoot>() ?? new SupplyCampLoot();
    }
}

public class SupplyCampEntry
{
    public string Code = "";
    public int Quantity = 1;
}
