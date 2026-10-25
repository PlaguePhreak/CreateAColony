using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace CreateAColony.Colonies;

/// <summary>
/// Recipes players teach a hut on its Crafting recipes tab (Teach recipe, as in MineColonies): the things that go
/// in, up to nine like the crafting grid, and what comes out. Every hut of that kind in the colony knows the recipe
/// and lists it with the game's recipes, so its worker crafts it the same way (AiTaskSettlerWorker.PlanRecipe).
/// Things with durability are tools: they wear down instead of being used up. Saved on the colony (CustomRecipes).
/// </summary>
public partial class ColonyManager
{
    /// <summary>Taught recipes turned into kinds, by hut type, until a recipe is taught or forgotten.</summary>
    readonly ConditionalWeakTable<Colony, Dictionary<string, List<SawKind>>> customKinds = new();

    /// <summary>Everything a hut of this type in this colony can craft: the game's recipes and the ones players taught it.</summary>
    public List<SawKind> HutRecipeKinds(Colony colony, string type)
    {
        var game = HutRecipeKinds(type);
        if (!colony.CustomRecipes.Any(r => r.HutType == type)) return game;
        var byType = customKinds.GetOrCreateValue(colony);
        if (byType.TryGetValue(type, out var known)) return known;

        // The game's kinds are shared by every colony, so a kind a taught recipe adds to is copied first.
        var kinds = game.ToDictionary(k => k.Key, k => k);
        var copied = new HashSet<string>();
        foreach (var custom in colony.CustomRecipes.Where(r => r.HutType == type))
        {
            if (ToSawRecipe(custom) is not { } recipe) continue;
            string key = recipe.Output.Collectible.Code.ToString();
            if (!kinds.TryGetValue(key, out var kind))
            {
                var sample = recipe.Output.Clone();
                kinds[key] = new SawKind { Key = key, Sample = sample, Recipes = new() { recipe } };
                copied.Add(key);
                continue;
            }
            if (copied.Add(key)) kinds[key] = kind = new SawKind { Key = key, Sample = kind.Sample, Recipes = kind.Recipes.ToList() };
            // A taught recipe comes before the game's, since the player chose it.
            kind.Recipes.Insert(kind.Recipes.Count(r => r.CustomId != 0), recipe);
        }
        return byType[type] = kinds.Values.OrderBy(k => FirstPart(k.Sample.Collectible.Code)).ThenBy(k => k.Key).ToList();
    }

    /// <summary>The recipes players taught this kind of hut, oldest first.</summary>
    public IEnumerable<CustomRecipe> TaughtRecipes(Colony colony, string type) => colony.CustomRecipes.Where(r => r.HutType == type);

    /// <summary>A taught recipe as the worker crafts it; null when something in it is gone (a mod was removed).</summary>
    public SawRecipe? ToSawRecipe(CustomRecipe custom)
    {
        if (StackOf(custom.Output) is not { } output) return null;
        var inputs = new List<(CraftingRecipeIngredient Part, int Quantity)>();
        var tools = new List<CraftingRecipeIngredient>();
        foreach (var part in custom.Inputs)
        {
            if (StackOf(part) is not { } stack) return null;
            // A tool can be any of its kind (any axe); with Any type, so can the rest (any planks).
            var code = stack.Collectible.Code;
            bool wild = (part.Tool || custom.AnyType) && code.Path.Contains('-');
            var ingredient = new CraftingRecipeIngredient
            {
                Type = part.IsBlock ? EnumItemClass.Block : EnumItemClass.Item,
                Code = wild ? new AssetLocation(code.Domain, FirstPart(code) + "-*") : code.Clone(),
                Quantity = part.Tool ? 1 : part.Quantity,
                IsTool = part.Tool
            };
            if (!ingredient.Resolve(sapi.World, "createacolony taught recipe")) return null;
            if (part.Tool) tools.Add(ingredient);
            else inputs.Add((ingredient, part.Quantity));
        }
        if (inputs.Count == 0) return null;
        return new SawRecipe
        {
            Inputs = inputs,
            Tool = tools.ElementAtOrDefault(0),
            Tool2 = tools.ElementAtOrDefault(1),
            Output = output,
            CustomId = custom.Id,
            Text = TaughtInputsText(custom)
        };
    }

    /// <summary>The item or block of a part, with the part's quantity; null when the game has no such thing now.</summary>
    ItemStack? StackOf(CustomRecipePart part)
    {
        var code = new AssetLocation(part.Code);
        CollectibleObject? thing = part.IsBlock ? sapi.World.GetBlock(code) : sapi.World.GetItem(code);
        if (thing == null || thing.Code == null) return null;
        return new ItemStack(thing, System.Math.Max(1, part.Quantity));
    }

    /// <summary>What a taught recipe takes, for its row: "2 Oak planks, 1 Stick + Flint axe (any type)".</summary>
    string TaughtInputsText(CustomRecipe custom)
    {
        var parts = custom.Inputs.Where(p => !p.Tool).Select(p => p.Quantity + " " + (StackOf(p)?.GetName() ?? p.Code));
        var tools = custom.Inputs.Where(p => p.Tool).Select(p => StackOf(p)?.GetName() ?? p.Code);
        string text = string.Join(", ", parts) + string.Concat(tools.Select(t => " + " + t));
        return custom.AnyType ? Lang.Get("createacolony:teach-anytype-note", text) : text;
    }

    /// <summary>
    /// Teaches every hut of this kind in the colony a recipe. Text is "any|inputs|output", each part "i:code*count"
    /// or "b:code*count" and the inputs split by commas. Returns a lang key when it can't.
    /// </summary>
    public string? TeachRecipe(Colony colony, Building hut, string text, string playerName)
    {
        if (!Config.CustomRecipes) return "createacolony:teach-off";
        if (!HasHutRecipes(hut.Type)) return "createacolony:bad-building";
        if (TaughtRecipes(colony, hut.Type).Count() >= Config.CustomRecipesPerHut) return "createacolony:teach-full";
        var fields = text.Split('|');
        if (fields.Length != 3) return "createacolony:bad-building";
        var slots = fields[1].Split(',', System.StringSplitOptions.RemoveEmptyEntries).Select(ParsePart).ToList();
        var output = ParsePart(fields[2]);
        if (slots.Count == 0) return "createacolony:teach-no-inputs";
        if (output == null) return "createacolony:teach-no-output";
        if (slots.Count > 9 || slots.Any(p => p == null)) return "createacolony:bad-building";

        // The same thing in several slots counts once, with their counts added up.
        var inputs = slots.GroupBy(p => (p!.Code, p.IsBlock))
            .Select(g => new CustomRecipePart { Code = g.Key.Code, IsBlock = g.Key.IsBlock, Quantity = g.Sum(p => p!.Quantity) })
            .ToList();
        foreach (var part in inputs)
        {
            var stack = StackOf(part);
            if (stack == null) return "createacolony:bad-building";
            part.Tool = stack.Collectible.GetMaxDurability(stack) > 1;
            if (part.Tool) part.Quantity = 1;
        }
        if (inputs.All(p => p.Tool)) return "createacolony:teach-only-tools";
        if (inputs.Count(p => p.Tool) > 2) return "createacolony:teach-many-tools";
        if (inputs.Any(p => !p.Tool && p.Code == output.Code)) return "createacolony:teach-self";
        if (StackOf(output) == null) return "createacolony:teach-no-output";

        var recipe = new CustomRecipe
        {
            Id = colony.NextCustomRecipeId++,
            HutType = hut.Type,
            Inputs = inputs,
            Output = output,
            AnyType = fields[0] == "1",
            TaughtBy = playerName
        };
        if (TaughtRecipes(colony, hut.Type).Any(r => SameRecipe(r, recipe))) return "createacolony:teach-known";
        colony.CustomRecipes.Add(recipe);
        customKinds.Remove(colony);
        LogEvent(colony, "event-recipe-taught", playerName, StackOf(output)!.GetName(), BuildingName(hut));
        MarkDirty(colony);
        return null;
    }

    /// <summary>Forgets a taught recipe. A thing no recipe of the hut makes any more is no longer kept by its huts.</summary>
    public string? ForgetRecipe(Colony colony, Building hut, int id)
    {
        var recipe = colony.CustomRecipes.Find(r => r.Id == id && r.HutType == hut.Type);
        if (recipe == null) return "createacolony:bad-building";
        colony.CustomRecipes.Remove(recipe);
        customKinds.Remove(colony);
        var keys = HutRecipeKinds(colony, hut.Type).Select(k => k.Key).ToHashSet();
        foreach (var other in colony.Buildings.Where(b => b.Type == hut.Type))
        {
            foreach (var key in other.RecipeKeep.Keys.Where(k => !keys.Contains(k)).ToList()) other.RecipeKeep.Remove(key);
        }
        MarkDirty(colony);
        return null;
    }

    static bool SameRecipe(CustomRecipe a, CustomRecipe b) =>
        a.AnyType == b.AnyType && a.Output.Code == b.Output.Code && a.Output.Quantity == b.Output.Quantity
        && a.Inputs.Count == b.Inputs.Count
        && a.Inputs.All(p => b.Inputs.Any(q => q.Code == p.Code && q.IsBlock == p.IsBlock && q.Quantity == p.Quantity));

    /// <summary>One part from Teach recipe: "i:game:stick*4". Null when it is not one.</summary>
    static CustomRecipePart? ParsePart(string text)
    {
        int star = text.LastIndexOf('*');
        if (text.Length < 3 || text[1] != ':' || star < 3 || !int.TryParse(text[(star + 1)..], out int count)) return null;
        if (text[0] is not ('i' or 'b') || count < 1 || count > 64) return null;
        return new CustomRecipePart { Code = text[2..star], IsBlock = text[0] == 'b', Quantity = count };
    }
}
