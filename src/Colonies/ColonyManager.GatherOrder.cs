using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using ItemMatch = System.Func<Vintagestory.API.Common.ItemStack, bool>;

namespace CreateAColony.Colonies;

/// <summary>
/// The Gatherer's Hut's collecting order (Aaron, 0.145.0): what its gatherer goes for first, by kind of thing.
/// Things the colony has asked for come first, then ore bits, food, herbs, clay, peat, reeds and the rest, until an
/// officer moves them on the Priorities tab (Building.GatherOrder). Within one kind it takes the nearest.
/// </summary>
public partial class ColonyManager
{
    public const string GatherRequests = "requests";
    /// <summary>The kinds of thing on the collecting order, in the order a new hut has them.</summary>
    public static readonly string[] GatherCategories = { GatherRequests, "ore", "food", "herbs", "clay", "peat", "reeds", "stones" };
    /// <summary>Keys of collecting order rows on the Priorities tab start with this, so MovePriority can tell them from crafting rows.</summary>
    public const string GatherKeyPrefix = "gather:";

    /// <summary>The hut's collecting order: the kinds an officer placed, then the rest in their usual order.</summary>
    public static List<string> GatherOrder(Building hut) =>
        hut.GatherOrder.Where(GatherCategories.Contains).Distinct().Concat(GatherCategories.Where(c => !hut.GatherOrder.Contains(c))).ToList();

    /// <summary>Moves one kind up, down or to the top of the hut's collecting order. Returns a lang key on failure.</summary>
    public string? MoveGatherPriority(Colony colony, Building hut, string category, int move)
    {
        var order = GatherOrder(hut);
        if (!Move(order, category, move)) return "createacolony:bad-building";
        hut.GatherOrder = order;
        MarkDirty(colony);
        return null;
    }

    /// <summary>Which row of the collecting order a thing the gatherer can take (Forage's kind) belongs to.</summary>
    string GatherCategory(Colony colony, Block block, string kind)
    {
        if (kind != "fruit" && IsRequested(colony, block)) return GatherRequests;
        string path = block.Code?.Path ?? "";
        return kind switch
        {
            "fruit" or "mushroom" or "crop" or "root" => "food",
            "herb" => "herbs",
            "reed" => "reeds",
            "dig" => path.StartsWith("peat-") ? "peat" : "clay",
            _ => path.StartsWith("looseores-") ? "ore" : "stones"
        };
    }

    /// <summary>True when the block, or what it drops, is something a hut is waiting on a delivery of.</summary>
    bool IsRequested(Colony colony, Block block)
    {
        var wanted = OpenRequestMatchers(colony);
        return wanted.Count > 0 && RequestSamples(block).Any(sample => wanted.Any(m => m(sample)));
    }

    /// <summary>What the colony's open delivery requests ask for, worked out again every 5 seconds.</summary>
    List<ItemMatch> OpenRequestMatchers(Colony colony)
    {
        long now = sapi.World.ElapsedMilliseconds;
        if (requestMatchers.TryGetValue(colony.ColonyId, out var cached) && now - cached.Ms < 5000) return cached.Matchers;
        var matchers = colony.Requests.Where(r => r.Remaining > 0).Select(r => r.ItemCode).Distinct().Select(Matcher).ToList();
        requestMatchers[colony.ColonyId] = (now, matchers);
        return matchers;
    }

    readonly Dictionary<string, (long Ms, List<ItemMatch> Matchers)> requestMatchers = new();

    /// <summary>The block and what it drops, by block id.</summary>
    List<ItemStack> RequestSamples(Block block)
    {
        if (requestSamples.TryGetValue(block.Id, out var known)) return known;
        var samples = new List<ItemStack> { new ItemStack(block) };
        try
        {
            foreach (var drop in block.Drops ?? System.Array.Empty<BlockDropItemStack>())
            {
                if (drop.ResolvedItemstack == null) drop.Resolve(sapi.World, "gather order", block.Code);
                if (drop.ResolvedItemstack != null) samples.Add(drop.ResolvedItemstack);
            }
        }
        catch (System.Exception)
        {
            // Unusual drops: only the block itself counts.
        }
        return requestSamples[block.Id] = samples;
    }

    readonly Dictionary<int, List<ItemStack>> requestSamples = new();
}
