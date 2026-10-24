using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using ItemMatch = System.Func<Vintagestory.API.Common.ItemStack, bool>;

namespace CreateAColony.Colonies;

/// <summary>
/// What the crafter huts share (Smelter, Blacksmith, Sawmill, Primitive Craftsman, and the ones to come): a "keep in stock" list on the
/// hut window, saved on the building as CraftKeep, and asking couriers for what the work needs. Each
/// crafter hut has its own list of things it can make, by key (CraftRows).
/// </summary>
public partial class ColonyManager
{
    /// <summary>Huts with a crafter's "keep in stock" tab, and the tab's name (also the start of its lang keys).</summary>
    public static string? CraftTabOf(string type) => type switch
    {
        BuildingTypes.Smelter => "smelting",
        BuildingTypes.Blacksmith => "smithing",
        BuildingTypes.Sawmill => "sawmill",
        BuildingTypes.Craftsman => "knapping",
        _ => null
    };

    /// <summary>How many of a thing a crafter hut keeps in stock: the hut's own choice, or the config defaults until an officer changes one.</summary>
    public int CraftKeep(Building hut, string key)
    {
        if (hut.CraftChosen) return hut.CraftKeep.GetValueOrDefault(key);
        var defaults = hut.Type switch
        {
            BuildingTypes.Smelter => Config.SmelterDefaultKeep,
            BuildingTypes.Sawmill => Config.SawmillDefaultKeep,
            BuildingTypes.Craftsman => Config.CraftsmanDefaultKeep,
            _ => Config.BlacksmithDefaultKeep
        };
        foreach (var (pattern, keep) in defaults)
        {
            if (WildcardUtil.Match(pattern, key)) return keep;
        }
        return 0;
    }

    /// <summary>The keys a crafter hut can make, so a changed amount can be checked.</summary>
    public IEnumerable<string> CraftKeys(Building hut) => hut.Type switch
    {
        BuildingTypes.Smelter => SmeltKinds().Select(k => k.Key),
        BuildingTypes.Blacksmith => SmithKinds().Select(k => k.Key),
        BuildingTypes.Sawmill => SawKinds().Select(k => k.Key),
        BuildingTypes.Craftsman => KnapKinds().Select(k => k.Key),
        _ => Enumerable.Empty<string>()
    };

    public void SetCraftKeep(Colony colony, Building hut, string key, int keep)
    {
        if (!hut.CraftChosen)
        {
            // The defaults become the hut's own list, so changing one thing keeps the others.
            hut.CraftKeep = CraftKeys(hut).Select(k => (Key: k, Keep: CraftKeep(hut, k))).Where(k => k.Keep > 0).ToDictionary(k => k.Key, k => k.Keep);
            hut.CraftChosen = true;
        }
        if (keep > 0) hut.CraftKeep[key] = keep;
        else hut.CraftKeep.Remove(key);
        MarkDirty(colony);
    }

    /// <summary>Asks couriers to bring something to a crafter hut, once at a time.</summary>
    public void EnsureCraftRequest(Colony colony, Building hut, string code, int quantity)
    {
        var chest = HutStorage(colony, hut).FirstOrDefault();
        if (chest == null) return;
        string tag = RequestTag(hut);
        if (!colony.Requests.Any(r => r.RequestedBy == tag && r.ItemCode == code)) AddRequest(colony, code, quantity, chest.Pos, tag);
    }

    /// <summary>The code of something matching in the warehouse, so a worker can ask couriers for it; null when there is none.</summary>
    public string? CodeInWarehouse(Colony colony, ItemMatch match)
    {
        foreach (var chest in WarehouseContainers(colony))
        {
            foreach (var slot in chest.Inventory)
            {
                if (!slot.Empty && match(slot.Itemstack)) return slot.Itemstack.Collectible.Code.ToString();
            }
        }
        return null;
    }

    /// <summary>The nearest block entity of a kind within radius blocks of the hut (4 up or down), for the work blocks of a crafter hut.</summary>
    public T? NearHut<T>(Building hut, int radius, System.Func<T, bool>? test = null) where T : BlockEntity
    {
        var h = hut.HutPos;
        var area = new Cuboidi(h.X - radius, h.Y - 4, h.Z - radius, h.X + radius, h.Y + 4, h.Z + radius);
        return FindBlockEntities<T>(area)
            .Where(b => test == null || test(b))
            .OrderBy(b => b.Pos.DistanceSqTo(h.X, h.Y, h.Z))
            .FirstOrDefault();
    }

    /// <summary>All block entities of a kind within radius blocks of a spot (2 up or down).</summary>
    public List<T> AllNear<T>(BlockPos at, int radius) where T : BlockEntity
    {
        var area = new Cuboidi(at.X - radius, at.Y - 2, at.Z - radius, at.X + radius, at.Y + 2, at.Z + radius);
        return FindBlockEntities<T>(area).OrderBy(b => b.Pos.DistanceSqTo(at.X, at.Y, at.Z)).ToList();
    }
}
