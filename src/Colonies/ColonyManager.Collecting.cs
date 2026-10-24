using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace CreateAColony.Colonies;

/// <summary>
/// The "Allowed to collect" tab of the gathering huts: which trees a forester fells, what a gatherer
/// or a farmer picks, and which ore and stone a miner, prospector or quarrier keeps. Everything is on
/// until an officer turns it off (Building.CollectOff). Each thing has a key: "wood-oak",
/// "fruit-game:fruit-blueberry", "mushroom-bolete", "loosestones", "ore-nativecopper", "rock-granite", "soil".
/// </summary>
public partial class ColonyManager
{
    /// <summary>One row of the tab: its key and a block or item to show for it.</summary>
    public record CollectKind(string Key, ItemStack Sample);

    readonly Dictionary<string, List<CollectKind>> collectKinds = new();

    /// <summary>Huts whose window has the Allowed to collect tab.</summary>
    public static bool HasCollectTab(string type) => type is BuildingTypes.Forester or BuildingTypes.Gatherer or BuildingTypes.Farmer
        or BuildingTypes.Miner or BuildingTypes.Prospector or BuildingTypes.Quarry or BuildingTypes.Hunter;

    /// <summary>False when the hut is set not to collect this; a thing with no key is always allowed.</summary>
    public static bool MayCollect(Building hut, string? key) => key == null || !hut.CollectOff.Contains(key);

    /// <summary>Everything the hut's worker can collect, for the tab, worked out once per hut type.</summary>
    public List<CollectKind> CollectKinds(string type)
    {
        if (collectKinds.TryGetValue(type, out var kinds)) return kinds;
        kinds = type switch
        {
            BuildingTypes.Forester => WoodKinds(),
            BuildingTypes.Gatherer => FruitKinds().Concat(MushroomKinds()).Concat(LooseKinds()).ToList(),
            BuildingTypes.Farmer => FruitKinds(),
            BuildingTypes.Hunter => AnimalKinds(),
            BuildingTypes.Miner or BuildingTypes.Prospector or BuildingTypes.Quarry => DigKinds(),
            _ => new List<CollectKind>()
        };
        collectKinds[type] = kinds;
        return kinds;
    }

    /// <summary>Turns one thing on or off at a hut; "*" turns everything back on, "-" turns everything off.</summary>
    public void SetCollect(Colony colony, Building hut, string key, bool on)
    {
        if (key == "*") hut.CollectOff.Clear();
        else if (key == "-") hut.CollectOff = CollectKinds(hut.Type).Select(k => k.Key).ToList();
        else
        {
            hut.CollectOff.Remove(key);
            if (!on && CollectKinds(hut.Type).Any(k => k.Key == key)) hut.CollectOff.Add(key);
        }
        MarkDirty(colony);
    }

    // ---- Keys -----------------------------------------------------------------------------

    /// <summary>A tree's key, from one of its logs.</summary>
    public static string? WoodKey(Block block) => VariantOf(block, "wood") is { } wood ? "wood-" + wood : null;

    /// <summary>The deposit code of an ore block, such as "nativecopper" for ore-poor-nativecopper-granite.</summary>
    public static string? OreCode(Block block)
    {
        if (!IsOre(block)) return null;
        if (VariantOf(block, "type") is { } type) return type;
        var parts = block.Code.Path.Split('-');
        return parts.Length >= 3 ? parts[^2] : null;
    }

    /// <summary>A dug block's key: its ore, its rock, or a kind of ground. Null for anything else, which is always kept.</summary>
    public static string? DigKey(Block block)
    {
        if (block.Code == null) return null;
        if (OreCode(block) is { } ore) return "ore-" + ore;
        string first = block.FirstCodePart();
        if (first == "rock" && VariantOf(block, "rock") is { } rock) return "rock-" + rock;
        return first switch
        {
            "soil" or "forestfloor" => "soil",
            "gravel" => "gravel",
            "sand" => "sand",
            "rawclay" => "clay",
            "peat" => "peat",
            _ => null
        };
    }

    /// <summary>True when the hut keeps what this block drops (and, for ore, goes after it).</summary>
    public static bool MayDig(Building hut, Block block) => MayCollect(hut, DigKey(block));

    /// <summary>The key of what a gatherer or farmer would take at a spot: a fruit, a mushroom, or a loose thing, reed or surface clay.</summary>
    public string? ForageKey(BlockPos pos)
    {
        var ba = sapi.World.BlockAccessor;
        var be = ba.GetBlockEntity(pos);
        if (IsFruitBearer(be)) return FruitKey(be!);
        var block = ba.GetBlock(pos);
        string path = block.Code?.Path ?? "";
        if (path.StartsWith("mushroom-")) return "mushroom-" + block.FirstCodePart(1);
        return WildKey(block);
    }

    /// <summary>The key of a loose thing, reed, surface clay or herb a gatherer takes: "loosestones", "seashell", "reed-papyrus", "clay-blue", "peat", "herb-horsetail".</summary>
    string? WildKey(Block block)
    {
        string path = block.Code?.Path ?? "";
        if (path.StartsWith("loosestones-")) return "loosestones";
        if (path.StartsWith("looseflints-")) return "looseflints";
        if (path.StartsWith("loosestick")) return "loosestick";
        if (path.StartsWith("looseores-")) return "looseores";
        if (block is BlockSeashell) return "seashell";
        if (block is BlockReeds) return "reed-" + block.FirstCodePart(1);
        if (path.StartsWith("rawclay-")) return "clay-" + (block.Variant["type"] ?? block.FirstCodePart(1));
        if (path.StartsWith("peat-")) return "peat";
        if (IsHerb(block)) return HerbKey(block);
        return null;
    }

    /// <summary>The key of the fruit a berry bush or fruit tree branch gives: "fruit-" and the fruit's item code.</summary>
    string? FruitKey(BlockEntity be)
    {
        if (be is BlockEntityFruitTreePart part)
        {
            var branchCode = AssetLocation.Create(part.Block.Attributes?["branchBlock"].AsString() ?? "", part.Block.Code.Domain);
            if (sapi.World.GetBlock(branchCode) is not BlockFruitTreeBranch branch || part.TreeType == null
                || !branch.TypeProps.TryGetValue(part.TreeType, out var props)) return null;
            return FirstFruit(props.FruitStacks) is { } fruit ? "fruit-" + fruit.Collectible.Code : null;
        }
        var bush = be.Block.GetBehavior<BlockBehaviorFruitingBush>();
        return FirstFruit(bush?.harvestedStacks) is { } berry ? "fruit-" + berry.Collectible.Code : null;
    }

    ItemStack? FirstFruit(BlockDropItemStack[]? drops)
    {
        foreach (var drop in drops ?? System.Array.Empty<BlockDropItemStack>())
        {
            if (drop.ResolvedItemstack == null) drop.Resolve(sapi.World, "allowed to collect", new AssetLocation("fruit"));
            if (drop.ResolvedItemstack != null) return drop.ResolvedItemstack;
        }
        return null;
    }

    // ---- The lists ------------------------------------------------------------------------

    /// <summary>One row per key, the first block or item found standing for it, sorted by key.</summary>
    static List<CollectKind> ByKey(IEnumerable<(string? Key, ItemStack Sample)> found) =>
        found.Where(f => f.Key != null).GroupBy(f => f.Key!).Select(g => new CollectKind(g.Key, g.First().Sample)).OrderBy(k => k.Key).ToList();

    List<CollectKind> WoodKinds() =>
        ByKey(sapi.World.Blocks.Where(b => b?.Code != null && IsTreeLog(b)).Select(b => (WoodKey(b), new ItemStack(b))));

    List<CollectKind> FruitKinds()
    {
        var found = new List<(string?, ItemStack)>();
        foreach (var block in sapi.World.Blocks)
        {
            if (block?.Code == null) continue;
            if (block is BlockFruitTreeBranch branch && branch.TypeProps != null)
            {
                foreach (var props in branch.TypeProps.Values)
                {
                    if (FirstFruit(props.FruitStacks) is { } fruit) found.Add(("fruit-" + fruit.Collectible.Code, fruit));
                }
            }
            else if (FirstFruit(block.GetBehavior<BlockBehaviorFruitingBush>()?.harvestedStacks) is { } berry)
            {
                found.Add(("fruit-" + berry.Collectible.Code, berry));
            }
        }
        return ByKey(found);
    }

    List<CollectKind> MushroomKinds() =>
        ByKey(sapi.World.Blocks.Where(b => b?.Code != null && b.Code.Path.StartsWith("mushroom-") && !IsPoisonousMushroom(b))
            .Select(b => ((string?)("mushroom-" + b.FirstCodePart(1)), new ItemStack(b))));

    /// <summary>Loose things first (stones, flint, sticks, ore bits, seashells), then reeds, then herbs, then clay and peat.</summary>
    List<CollectKind> LooseKinds()
    {
        var all = ByKey(sapi.World.Blocks.Where(b => b?.Code != null && WildKind(b) != null).Select(b => (WildKey(b), new ItemStack(b))));
        string[] loose = { "loosestones", "looseflints", "loosestick", "looseores", "seashell" };
        int Order(CollectKind k) => System.Array.IndexOf(loose, k.Key) is int i and >= 0 ? i : k.Key.StartsWith("reed-") ? 10 : k.Key.StartsWith("herb-") ? 11 : 12;
        return all.OrderBy(Order).ThenBy(k => k.Key).ToList();
    }

    /// <summary>Ores first, then rock, then the kinds of ground.</summary>
    List<CollectKind> DigKinds()
    {
        var all = ByKey(sapi.World.Blocks.Where(b => b?.Code != null && DigKey(b) != null).Select(b => (DigKey(b), new ItemStack(b))));
        int Order(CollectKind k) => k.Key.StartsWith("ore-") ? 0 : k.Key.StartsWith("rock-") ? 1 : 2;
        return all.OrderBy(Order).ThenBy(k => k.Key).ToList();
    }
}
