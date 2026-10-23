using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace CreateAColony.Colonies;

/// <summary>
/// Which meals a cook knows: each pot recipe takes a Cooking level (config/cooking.json), and the cook's
/// usable Cooking level (capped by the Cook's Hut level) plus the Good Cook trait decides what they make.
/// Grilling in the fire pit never needs a level, so nobody starves while the cook learns.
/// </summary>
public partial class ColonyManager
{
    const string CookingAsset = "config/cooking.json";

    class CookingFile
    {
        public int[] ByIngredients = { 2, 2, 4, 6 };
        public Dictionary<string, int> Levels = new();
        /// <summary>Stone Bake Oven bakes, by the baked food's name ("bread", "pie") or code; "default" for the rest.</summary>
        public Dictionary<string, int> Baking = new();
    }

    CookingFile? cooking;

    CookingFile Cooking()
    {
        if (cooking != null) return cooking;
        cooking = sapi.Assets.TryGet(new AssetLocation(CreateAColonyModSystem.ModId, CookingAsset))?.ToObject<CookingFile>() ?? new CookingFile();
        return cooking;
    }

    /// <summary>The Cooking level a pot meal takes: from cooking.json, or from how many ingredient types it has.</summary>
    public int MealLevel(CookingRecipe recipe)
    {
        if (!Config.Skills) return 0;
        var file = Cooking();
        if (recipe.Code != null && file.Levels.TryGetValue(recipe.Code, out int level)) return Math.Clamp(level, 0, MaxLevel);
        int types = recipe.Ingredients?.Length ?? 1;
        var table = file.ByIngredients is { Length: > 0 } t ? t : new[] { 2, 2, 4, 6 };
        return Math.Clamp(table[Math.Clamp(types - 1, 0, table.Length - 1)], 0, MaxLevel);
    }

    /// <summary>The level a cook cooks at: what the Cook's Hut lets them use of Cooking, plus Good Cook's extra level.</summary>
    public int CookLevel(Colony colony, CitizenRecord cook) =>
        Math.Max(0, UsableLevel(colony, cook, "cooking") + (int)Math.Round(TraitAdd(cook, "cookLevel")));

    public bool KnowsMeal(Colony colony, CitizenRecord cook, CookingRecipe recipe) => !Config.Skills || MealLevel(recipe) <= CookLevel(colony, cook);
}
