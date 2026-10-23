using System.Collections.Generic;
using Vintagestory.API.Common;

namespace CreateAColony.Blocks;

/// <summary>Loaded from assets/createacolony/config/supplycamp.json so packs can change what the camp hands out.</summary>
public class SupplyCampLoot
{
    public const string AssetPath = "config/supplycamp.json";

    /// <summary>Handed straight to the player who places their first camp: what they need to found a colony.</summary>
    public List<SupplyCampEntry> PlayerItems = new();
    /// <summary>Put once into the first camp's own storage.</summary>
    public List<SupplyCampEntry> Items = new();

    public static SupplyCampLoot Load(ICoreAPI api)
    {
        var asset = api.Assets.TryGet(new AssetLocation(CreateAColonyModSystem.ModId, AssetPath));
        return asset?.ToObject<SupplyCampLoot>() ?? new SupplyCampLoot();
    }

    /// <summary>Turns entries into stacks, skipping (with a warning) codes that are not in the game.</summary>
    public static List<ItemStack> Resolve(ICoreAPI api, IEnumerable<SupplyCampEntry> entries)
    {
        var stacks = new List<ItemStack>();
        foreach (var entry in entries)
        {
            var code = new AssetLocation(entry.Code);
            CollectibleObject? collectible = (CollectibleObject?)api.World.GetItem(code) ?? api.World.GetBlock(code);
            if (collectible == null)
            {
                api.Logger.Warning("[createacolony] Supply camp item {0} not found, skipping", entry.Code);
                continue;
            }
            stacks.Add(new ItemStack(collectible, entry.Quantity));
        }
        return stacks;
    }
}

public class SupplyCampEntry
{
    public string Code = "";
    public int Quantity = 1;
}
