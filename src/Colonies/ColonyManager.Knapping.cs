using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.GameContent;
using ItemMatch = System.Func<Vintagestory.API.Common.ItemStack, bool>;

namespace CreateAColony.Colonies;

/// <summary>
/// The craftsman of a Primitive Craftsman's hut knaps flint and stones into tool heads (axe, knife, shovel,
/// hoe, spear and arrow heads) at the hut, then puts each head on a stick when the game has a crafting
/// recipe for that, making the finished stone tool. The game's knapping recipes say what can be made and
/// from which stone. The craftsman does not chip voxels one by one like a player but strikes a set number
/// of times. The Knapping tab says how many of each thing to keep. Finished tools go to the warehouse,
/// where the other workers pick up the tools they need.
/// </summary>
public partial class ColonyManager
{
    /// <summary>One thing the craftsman can make: the stones it is knapped from, the head, and the crafting step that finishes it, if any.</summary>
    public sealed class KnapKind
    {
        /// <summary>The finished thing's code: the tool when the head is put on a handle, else the head.</summary>
        public required string Key;
        public required ItemStack Sample;
        /// <summary>The stones the head can be knapped from (one per knapping), from every knapping recipe that makes it.</summary>
        public required List<CraftingRecipeIngredient> Stones;
        /// <summary>What comes off the knapping.</summary>
        public required ItemStack Piece;
        /// <summary>The crafting recipe that puts the head on a handle, and what else it takes; null when the head is the finished thing.</summary>
        public GridRecipe? Assembly;
        public List<(CraftingRecipeIngredient Part, int Quantity)> Handles = new();

        public bool IsStone(ItemStack s) => Stones.Any(i => i.SatisfiesAsIngredient(s, false));
        public bool IsPiece(ItemStack s) => s.Collectible.Code.Equals(Piece.Collectible.Code);
        public bool IsMine(ItemStack s) => s.Collectible.Code.ToString() == Key;
        public bool IsHandle(ItemStack s) => Handles.Any(h => h.Part.SatisfiesAsIngredient(s, false));
    }

    List<KnapKind>? knapKinds;

    /// <summary>Everything the game's knapping recipes make, finished on a handle where a crafting recipe allows it.</summary>
    public List<KnapKind> KnapKinds()
    {
        if (knapKinds != null) return knapKinds;
        var kinds = new Dictionary<string, KnapKind>();
        var assemblies = sapi.World.GridRecipes.Where(r => r?.Output?.ResolvedItemStack != null && r.Enabled && r.RequiresTrait == null
            && r.ResolvedIngredients != null && r.ResolvedIngredients.Count(i => i != null) >= 2).ToList();
        foreach (var recipe in sapi.GetKnappingRecipes())
        {
            var piece = recipe?.Output?.ResolvedItemstack;
            var stone = recipe?.Ingredient;
            if (recipe == null || piece == null || stone == null || piece.Collectible.Code.Domain == "createacolony") continue;
            var (assembly, handles) = FindHafting(assemblies, piece);
            var finished = assembly?.Output?.ResolvedItemStack ?? piece;
            string key = finished.Collectible.Code.ToString();
            if (kinds.TryGetValue(key, out var known))
            {
                if (!known.Stones.Any(s => s.Code?.Equals(stone.Code) == true)) known.Stones.Add(stone);
                continue;
            }
            var sample = finished.Clone();
            sample.StackSize = Math.Max(1, finished.StackSize);
            kinds[key] = new KnapKind
            {
                Key = key,
                Sample = sample,
                Stones = new List<CraftingRecipeIngredient> { stone },
                Piece = piece.Clone(),
                Assembly = assembly,
                Handles = handles
            };
        }
        return knapKinds = kinds.Values.OrderBy(k => FirstPart(k.Sample.Collectible.Code)).ThenBy(k => k.Key).ToList();
    }

    /// <summary>
    /// A crafting recipe without tools that takes this head once plus a few other things (a stick), and what
    /// else it takes. A recipe that makes a thing named after the head (axe from axehead) comes first, then
    /// the one that takes the fewest other things.
    /// </summary>
    (GridRecipe?, List<(CraftingRecipeIngredient, int)>) FindHafting(List<GridRecipe> recipes, ItemStack piece)
    {
        GridRecipe? best = null;
        List<(CraftingRecipeIngredient, int)> bestParts = new();
        int bestScore = int.MaxValue;
        string head = FirstPart(piece.Collectible.Code);
        foreach (var recipe in recipes)
        {
            var parts = recipe.ResolvedIngredients!.Where(i => i != null).Select(i => i!).ToList();
            var forPiece = parts.Where(i => i.SatisfiesAsIngredient(piece, false)).ToList();
            if (forPiece.Count != 1 || parts.Any(i => i.IsTool || !i.Consume)) continue;
            var output = recipe.Output!.ResolvedItemStack!;
            if (output.Collectible.Code.Equals(piece.Collectible.Code) || output.Collectible.Code.Domain == "createacolony") continue;
            var others = parts.Where(i => i != forPiece[0]).GroupBy(i => i.Type + ":" + i.Code)
                .Select(g => (Part: g.First(), Quantity: g.Sum(i => Math.Max(1, i.Quantity)))).ToList();
            int count = others.Sum(o => o.Quantity);
            if (count == 0 || count > Config.CraftsmanMaxHandleParts) continue;
            string made = FirstPart(output.Collectible.Code);
            int score = count + (head.StartsWith(made) ? 0 : 100);
            if (score >= bestScore) continue;
            best = recipe;
            bestParts = others.Select(o => (o.Part, o.Quantity)).ToList();
            bestScore = score;
        }
        return (best, bestParts);
    }

    /// <summary>How many of a thing are ready: at the hut, in the warehouse and with the craftsman.</summary>
    public int KnapStock(Colony colony, Building hut, KnapKind kind, CitizenRecord? craftsman)
    {
        ItemMatch mine = kind.IsMine;
        int count = CountItems(WarehouseContainers(colony), mine) + CountItems(HutStorage(colony, hut), mine);
        if (craftsman != null) count += PackCount(craftsman, mine);
        return count;
    }

    /// <summary>The things there are too few of, most wanted first.</summary>
    public List<KnapKind> WantedKnapping(Colony colony, Building hut, CitizenRecord? craftsman) => ByPriority(hut, KnapKinds()
        .Select(k => (Kind: k, Want: CraftWant(colony, hut, k.Key)))
        .Where(k => k.Want > 0)
        .Select(k => (k.Kind, k.Kind.Key, Short: k.Want - KnapStock(colony, hut, k.Kind, craftsman)))
        .Where(k => k.Short > 0))
        .ToList();

    /// <summary>
    /// The rows of the Knapping tab: everything kept in stock, and everything that can be made from the
    /// stones the colony has (in the warehouse, the hut storage or with the craftsman).
    /// </summary>
    public List<KnapKind> KnapRows(Colony colony, Building hut, CitizenRecord? craftsman)
    {
        var stacks = WarehouseContainers(colony).Cast<BlockEntityContainer>().Concat(HutStorage(colony, hut))
            .SelectMany(c => c.Inventory).Where(s => !s.Empty).Select(s => s.Itemstack!).ToList();
        if (craftsman != null) stacks.AddRange(PackItems(craftsman));
        stacks = stacks.GroupBy(s => s.Collectible.Code).Select(g => g.First()).ToList();
        return KnapKinds().Where(k => CraftKeep(hut, k.Key) > 0 || stacks.Any(k.IsStone)).ToList();
    }

    /// <summary>True when the craftsman carries everything the hafting takes.</summary>
    public bool HasHandles(CitizenRecord craftsman, KnapKind kind) =>
        kind.Handles.All(h => PackCount(craftsman, s => h.Part.SatisfiesAsIngredient(s, false)) >= h.Quantity);

    /// <summary>The last strike: one stone becomes the head. False if the craftsman has no stone for it.</summary>
    public bool FinishKnapping(Colony colony, Building hut, CitizenRecord craftsman, KnapKind kind)
    {
        if (TakeFromPack(colony, craftsman, kind.IsStone, 1) == null) return false;
        var made = kind.Piece.Clone();
        int count = made.StackSize;
        AddToPack(colony, craftsman, made);
        if (made.StackSize > 0) StoreHarvest(colony, hut, craftsman, new[] { made }, hut.HutPos);
        AddStat(hut, "knapped", count);
        return true;
    }

    /// <summary>Puts a head on its handle. False, taking nothing, if the head or a handle part is missing.</summary>
    public bool Haft(Colony colony, Building hut, CitizenRecord craftsman, KnapKind kind)
    {
        if (kind.Assembly == null || PackCount(craftsman, kind.IsPiece) < 1 || !HasHandles(craftsman, kind)) return false;
        TakeFromPack(colony, craftsman, kind.IsPiece, 1);
        foreach (var (part, quantity) in kind.Handles)
        {
            int left = quantity;
            while (left > 0 && TakeFromPack(colony, craftsman, s => part.SatisfiesAsIngredient(s, false), left) is { } taken) left -= taken.StackSize;
        }
        var made = kind.Assembly.Output!.ResolvedItemStack!.Clone();
        int count = made.StackSize;
        AddToPack(colony, craftsman, made);
        if (made.StackSize > 0) StoreHarvest(colony, hut, craftsman, new[] { made }, hut.HutPos);
        AddStat(hut, "hafted", count);
        return true;
    }

    /// <summary>At a Primitive Craftsman's hut stones, handles and unfinished heads stay; finished things go to the warehouse.</summary>
    ItemMatch CraftsmanHutJunk(Colony colony, Building hut)
    {
        var kept = KnapKinds().Where(k => CraftWant(colony, hut, k.Key) > 0).ToList();
        return s => !kept.Any(k => k.IsStone(s) || k.IsHandle(s) || (k.Assembly != null && k.IsPiece(s)));
    }

    /// <summary>A line about what the craftsman keeps, for the hut window.</summary>
    public string CraftsmanInfo(Building hut)
    {
        int kept = KnapKinds().Count(k => CraftKeep(hut, k.Key) > 0);
        return Lang.Get("createacolony:knapping-keeping", kept, KnapKinds().Count);
    }
}
