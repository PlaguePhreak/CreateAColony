using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Util;
using ItemMatch = System.Func<Vintagestory.API.Common.ItemStack, bool>;

namespace CreateAColony.Colonies;

/// <summary>
/// The Crafting recipes tab of huts that are not crafter huts (Forester, Farmer, Gatherer, Miner, Quarry):
/// the player sets how many of a thing to keep, such as firewood or sticks at a Forester's Hut, and the
/// hut's worker crafts it at the hut block between its other work, as a player would at the crafting grid.
/// What a hut can make is set by config HutRecipeOutputs and HutRecipeTools and found in the game's grid
/// recipes. The crafting itself is in AiTaskSettlerWorker (PlanRecipe).
/// </summary>
public partial class ColonyManager
{
    readonly Dictionary<string, List<SawKind>> hutRecipeKinds = new();
    readonly Dictionary<string, string> partNames = new();

    /// <summary>True for a hut with a Crafting recipes tab.</summary>
    public bool HasHutRecipes(string type) => CraftTabOf(type) == null && Config.HutRecipeOutputs.ContainsKey(type);

    /// <summary>Everything a hut of this type can craft, from the game's grid recipes.</summary>
    public List<SawKind> HutRecipeKinds(string type)
    {
        if (hutRecipeKinds.TryGetValue(type, out var known)) return known;
        var kinds = new List<SawKind>();
        if (HasHutRecipes(type))
        {
            var outputs = Config.HutRecipeOutputs[type];
            var tools = Config.HutRecipeTools.GetValueOrDefault(type) ?? System.Array.Empty<string>();
            kinds = ScanGridRecipes(_ => true, tool => tools.Contains(FirstPart(tool.Code)),
                output => output.Collectible.Code.Domain == "game" && outputs.Contains(FirstPart(output.Collectible.Code)), Config.HutRecipeMaxInputs);
        }
        return hutRecipeKinds[type] = kinds;
    }

    public int RecipeKeep(Building hut, string key) => hut.RecipeKeep.GetValueOrDefault(key);

    public void SetRecipeKeep(Colony colony, Building hut, string key, int keep)
    {
        if (keep > 0) hut.RecipeKeep[key] = keep;
        else hut.RecipeKeep.Remove(key);
        MarkDirty(colony);
    }

    /// <summary>How many of a thing the colony has: in the warehouse, at the hut and with the worker.</summary>
    public int RecipeStock(Colony colony, Building hut, SawKind kind, CitizenRecord? worker)
    {
        ItemMatch mine = kind.IsMine;
        int count = CountItems(WarehouseContainers(colony), mine) + CountItems(HutStorage(colony, hut), mine);
        if (worker != null) count += PackCount(worker, mine);
        return count;
    }

    /// <summary>The things a hut has too few of, most wanted first.</summary>
    public List<SawKind> WantedRecipes(Colony colony, Building hut, CitizenRecord? worker)
    {
        if (hut.RecipeKeep.Count == 0 && !HasOrders(colony, hut)) return new List<SawKind>();
        return ByPriority(hut, HutRecipeKinds(hut.Type)
            .Select(k => (Kind: k, Want: RecipeWant(colony, hut, k.Key)))
            .Where(k => k.Want > 0)
            .Select(k => (k.Kind, k.Kind.Key, Short: k.Want - RecipeStock(colony, hut, k.Kind, worker)))
            .Where(k => k.Short > 0))
            .ToList();
    }

    /// <summary>True when the worker carries everything the recipe takes, and its tool if it needs one.</summary>
    public bool HasRecipeInputs(CitizenRecord worker, SawRecipe recipe, ItemStack? inHand) =>
        HasSawInputs(worker, recipe)
        && (recipe.Tool == null || recipe.Tool.SatisfiesAsIngredient(inHand, false) || PackCount(worker, s => recipe.Tool.SatisfiesAsIngredient(s, false)) > 0);

    /// <summary>Crafts the recipe once from the worker's inventory; what it made goes to its inventory or the hut storage.</summary>
    public bool CraftHutRecipe(Colony colony, Building hut, CitizenRecord worker, SawRecipe recipe)
    {
        if (!SawCraftInputs(colony, worker, recipe)) return false;
        var made = recipe.Output.Clone();
        int count = made.StackSize;
        StoreHarvest(colony, hut, worker, new[] { made }, hut.HutPos);
        AddStat(hut, "crafted", count);
        return true;
    }

    /// <summary>The things a hut keeps for recipes it is short of, so couriers leave them at the hut.</summary>
    ItemMatch HutRecipeInputs(Colony colony, Building hut)
    {
        if (hut.RecipeKeep.Count == 0 && !HasOrders(colony, hut)) return _ => false;
        var parts = WantedRecipes(colony, hut, WorkerAt(colony, hut, JobOf(hut.Type) ?? ""))
            .SelectMany(k => k.Recipes).SelectMany(r => r.Inputs).Select(i => i.Part).ToList();
        return s => parts.Any(p => p.SatisfiesAsIngredient(s, false));
    }

    /// <summary>What a recipe is made from, for its row: "1 Log, 2 Sticks".</summary>
    public string RecipeInputsText(SawRecipe recipe) =>
        string.Join(", ", recipe.Inputs.Select(i => i.Quantity + " " + PartName(i.Part)))
        + (recipe.Tool != null ? " + " + PartName(recipe.Tool) : "");

    /// <summary>The name of a recipe ingredient; for one like log-*, the name of the first thing that fits.</summary>
    string PartName(CraftingRecipeIngredient part)
    {
        if (part.ResolvedItemStack != null && !part.IsWildCard) return part.ResolvedItemStack.GetName();
        string key = part.Type + ":" + part.Code;
        if (partNames.TryGetValue(key, out var name)) return name;
        name = FirstPart(part.Code);
        IEnumerable<CollectibleObject> all = part.Type == EnumItemClass.Block ? sapi.World.Blocks : sapi.World.Items;
        foreach (var c in all)
        {
            if (c?.Code == null || !WildcardUtil.Match(part.Code, c.Code)) continue;
            var stack = new ItemStack(c);
            if (!part.SatisfiesAsIngredient(stack, false)) continue;
            name = stack.GetName();
            break;
        }
        return partNames[key] = name;
    }

    /// <summary>A line about what the hut crafts, for the hut window.</summary>
    public string HutRecipesInfo(Building hut) =>
        Lang.Get("createacolony:recipes-keeping", HutRecipeKinds(hut.Type).Count(k => RecipeKeep(hut, k.Key) > 0));
}
