using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Cairo;
using CreateAColony.Colonies;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace CreateAColony.Map;

/// <summary>
/// One thing a colony marker can show: an ore, a fruit, a vegetable, a grain, an animal, a trader or a story
/// location. Icon is its waypoint icon name, Art the picture it is drawn from, and Badge the letters added in
/// the corner when several things share a picture ("" for none).
/// </summary>
public sealed record MarkerSubject(string Category, string Key, string Name, string Icon, AssetLocation Art, string Badge)
{
    public string Id => Category + ":" + Key;
}

/// <summary>
/// Every ore, fruit, vegetable, grain, animal, trader and story location the game has, its own and other mods',
/// each with a waypoint icon. The list is made from the loaded blocks, items, creatures and worldgen files, the
/// same way on the server and on every player's game, so both sides name the icons alike. Things with a picture
/// of their own (textures/icons/worldmap/vc-*.svg or textures/icons/vcart/*.svg, from tools/icons) use it; the
/// rest get their kind's picture with a letter. The client adds the icons to the game's waypoint list
/// (RegisterIcons), so players can use them for their own waypoints too.
/// </summary>
public static class MarkerSubjects
{
    public const string Ore = "ore", Fruit = "fruit", Vegetable = "vegetable", Grain = "grain", Animal = "animal",
        Trader = "trader", Story = "story";

    /// <summary>The order the kinds come in, in the Add waypoint window.</summary>
    static readonly string[] Categories = { Ore, Fruit, Vegetable, Grain, Animal, Trader, Story };

    sealed class Catalogue
    {
        public readonly List<MarkerSubject> All = new();
        public readonly Dictionary<string, MarkerSubject> ById = new();
    }

    static readonly ConditionalWeakTable<IWorldAccessor, Catalogue> catalogues = new();

    /// <summary>The thing with this category and key, or null when the game has nothing like it.</summary>
    public static MarkerSubject? Get(ICoreAPI api, string category, string? key) =>
        key != null && Of(api).ById.TryGetValue(category + ":" + Clean(key), out var subject) ? subject : null;

    static string Clean(string key) => Regex.Replace(key.ToLowerInvariant(), "[^a-z0-9_-]", "");

    public static IReadOnlyList<MarkerSubject> All(ICoreAPI api) => Of(api).All;

    static Catalogue Of(ICoreAPI api)
    {
        lock (catalogues)
        {
            if (!catalogues.TryGetValue(api.World, out var catalogue))
            {
                catalogue = Build(api);
                catalogues.Add(api.World, catalogue);
                api.Logger.Notification("[createacolony] Colony marker icons: {0}", string.Join(", ",
                    Categories.Select(c => catalogue.All.Count(s => s.Category == c) + " " + c)));
            }
            return catalogue;
        }
    }

    // ---- What each thing is called by ---------------------------------------------------------

    static readonly HashSet<string> Grades = new() { "poor", "medium", "rich", "bountiful" };

    /// <summary>The ore in an ore block ("nativecopper"), or null when the block is not ore.</summary>
    public static string? OreKey(Block block)
    {
        string path = block.Code?.Path ?? "";
        string first = path.Split('-')[0];
        if (first != "ore" && first != "looseores" && first != "crystalizedore") return null;
        if (block.Variant.TryGetValue("type", out var type) && !string.IsNullOrEmpty(type)) return type;
        if (block.Variant.TryGetValue("ore", out var ore) && !string.IsNullOrEmpty(ore)) return ore;
        var parts = path.Split('-');
        if (parts.Length >= 3 && Grades.Contains(parts[1])) return parts[2];
        return parts.Length >= 2 ? parts[1] : null;
    }

    static readonly string[] Processed =
    {
        "dried", "dry", "mash", "pressed", "cooked", "boiled", "roast", "flour", "dough", "bread", "pie", "soup", "stew",
        "meal", "portion", "juice", "candied", "slice", "chopped", "pickled", "jam", "salted", "smoked", "ferment",
        "rot", "seed", "peeled", "paste", "sauce", "porridge", "cake", "syrup", "oil"
    };

    /// <summary>The food kind (fruit, vegetable or grain) and key of an item ("fruit", "cherry"), or null.</summary>
    public static (string Category, string Key)? FoodKey(CollectibleObject? item)
    {
        if (item?.Code == null) return null;
        string? category = item.NutritionProps?.FoodCategory switch
        {
            EnumFoodCategory.Fruit => Fruit,
            EnumFoodCategory.Vegetable => Vegetable,
            EnumFoodCategory.Grain => Grain,
            _ => null
        };
        if (category == null) return null;
        string path = item.Code.Path;
        int dash = path.IndexOf('-');
        string first = dash < 0 ? path : path[..dash];
        if (first == category && dash > 0) return (category, path[(dash + 1)..]);
        // Another mod's food: the raw thing only, not what is cooked or made from it.
        if (item.Code.Domain == "game" || Processed.Any(path.Contains)) return null;
        return (category, path);
    }

    /// <summary>Parts of a creature's code that say its age or sex, or its colour, not what it is.</summary>
    static readonly HashSet<string> NotTheKind = new()
    {
        "male", "female", "baby", "pup", "cub", "calf", "kid", "lamb", "piglet", "foal", "fawn", "chick", "juvenile",
        "adult", "young", "rooster", "hen", "sow", "boar-male", "wild", "domestic", "tame", "tamed", "small", "large",
        "normal", "elder", "old"
    };
    static readonly HashSet<string> NotTheKindVariants = new()
    {
        "gender", "sex", "age", "stage", "color", "colour", "coat", "pattern", "skin", "texture", "state", "size", "fur"
    };

    static readonly Dictionary<string, Type?> entityClasses = new();

    static Type? EntityClass(ICoreAPI api, string? className)
    {
        if (string.IsNullOrEmpty(className)) return null;
        lock (entityClasses)
        {
            if (entityClasses.TryGetValue(className, out var known)) return known;
            Type? type = null;
            try { type = api.ClassRegistry.CreateEntity(className)?.GetType(); }
            catch (Exception) { /* not a creature class we can look at */ }
            return entityClasses[className] = type;
        }
    }

    /// <summary>A creature's kind and key: ("trader", "luxuries"), ("animal", "wolf"), or null for anything else.</summary>
    public static (string Category, string Key)? CreatureKey(ICoreAPI api, EntityProperties? props)
    {
        if (props?.Code == null || props.Code.Domain == "createacolony") return null;
        string path = props.Code.Path;
        var type = EntityClass(api, props.Class);
        if (path.StartsWith("humanoid-trader-")) return (Trader, path["humanoid-trader-".Length..]);
        if (type != null && typeof(EntityTrader).IsAssignableFrom(type)) return (Trader, path);
        if (type == null || !typeof(EntityAgent).IsAssignableFrom(type) || typeof(EntityPlayer).IsAssignableFrom(type)
            || typeof(EntityHumanoid).IsAssignableFrom(type) || path.StartsWith("humanoid")) return null;

        var drop = new HashSet<string>(props.Variant.Where(v => NotTheKindVariants.Contains(v.Key)).Select(v => v.Value));
        var parts = path.Split('-').Where(p => p.Length > 0 && !NotTheKind.Contains(p) && !drop.Contains(p)).ToList();
        if (parts.Count == 0) parts.Add(path.Split('-')[0]);
        return (Animal, string.Join("-", parts));
    }

    // ---- Building the list --------------------------------------------------------------------

    sealed class Found
    {
        public string Category = "", Key = "", Name = "";
        public AssetLocation Art = null!;
        public bool OwnArt;
    }

    static Catalogue Build(ICoreAPI api)
    {
        var world = api.World;
        var found = new Dictionary<string, Found>();

        void Add(string category, string key, Func<string> name)
        {
            key = Clean(key);
            if (key.Length == 0 || found.ContainsKey(category + ":" + key)) return;
            found[category + ":" + key] = new Found { Category = category, Key = key, Name = name() };
        }

        foreach (var block in world.Blocks)
        {
            if (block?.Code == null || OreKey(block) is not { } ore) continue;
            Add(Ore, ore, () => Lang.GetIfExists("ore-" + ore) ?? Lang.GetIfExists(block.Code.Domain + ":ore-" + ore) ?? Pretty(ore));
        }
        foreach (var item in world.Items)
        {
            if (FoodKey(item) is not { } food) continue;
            Add(food.Category, food.Key, () => item.GetHeldItemName(new ItemStack(item)));
        }
        foreach (var props in world.EntityTypes)
        {
            if (CreatureKey(api, props) is not { } creature) continue;
            Add(creature.Category, creature.Key, () => Lang.GetIfExists("createacolony:subject-" + creature.Key) ?? CreatureName(props));
        }
        foreach (string code in StoryCodes(api))
        {
            Add(Story, code, () => Lang.GetIfExists("createacolony:story-" + code) ?? Pretty(code));
        }

        // Pictures: the worldmap icons are waypoint icons already; the vcart ones only pictures.
        var worldmap = Names(api, "textures/icons/worldmap/");
        var art = Names(api, "textures/icons/vcart/");
        foreach (var f in found.Values) FindArt(f, worldmap, art);

        // Things that share a picture get letters to tell them apart, unless only one has it.
        var subjects = new List<MarkerSubject>();
        foreach (var group in found.Values.GroupBy(f => f.Category + "|" + f.Art))
        {
            var members = group.OrderBy(f => f.Key, StringComparer.Ordinal).ToList();
            bool needLetters = members.Count > 1 || !members[0].OwnArt;
            var badges = needLetters ? Badges(members) : null;
            foreach (var f in members)
            {
                bool isWaypointIcon = f.Art.Path.StartsWith("textures/icons/worldmap/") && f.Art.Domain == "createacolony";
                string icon = !needLetters && isWaypointIcon
                    ? f.Art.Path["textures/icons/worldmap/".Length..^".svg".Length]
                    : "vc-" + f.Category + "-" + f.Key;
                subjects.Add(new MarkerSubject(f.Category, f.Key, f.Name, icon, f.Art, badges?[f] ?? ""));
            }
        }

        var catalogue = new Catalogue();
        foreach (var s in subjects.OrderBy(s => Array.IndexOf(Categories, s.Category)).ThenBy(s => s.Key, StringComparer.Ordinal))
        {
            catalogue.All.Add(s);
            catalogue.ById[s.Id] = s;
        }
        return catalogue;
    }

    static HashSet<string> Names(ICoreAPI api, string folder) =>
        new(api.Assets.GetMany(folder, "createacolony", false).Select(a => a.Name[..a.Name.IndexOf('.')]));

    /// <summary>Picture names to try first, where the thing's own name does not find one.</summary>
    static readonly Dictionary<string, string> ArtAliases = new()
    {
        ["blueberry"] = "vc-blueberries", ["cranberry"] = "vc-cranberries", ["cherry"] = "vc-cherry",
        ["blackcurrant"] = "currant", ["redcurrant"] = "currant", ["whitecurrant"] = "currant",
        ["spelt"] = "vc-wheat", ["rye"] = "vc-wheat", ["amaranth"] = "grain", ["onion"] = "vc-onion",
        ["boar"] = "vc-boar", ["pig"] = "vc-pig", ["hare"] = "hare", ["salmon"] = "vc-fish", ["fish"] = "vc-fish",
        ["tobiascave"] = "cave", ["devastationarea"] = "devastation", ["village"] = "village",
    };

    /// <summary>
    /// Finds the thing's picture by its key alone, never by its name, which is in the player's language: the
    /// server and every player must pick the same picture, since that decides the icon's name.
    /// </summary>
    static void FindArt(Found f, HashSet<string> worldmap, HashSet<string> art)
    {
        AssetLocation? Try(string name)
        {
            if (name.StartsWith("vc-")) return worldmap.Contains(name) ? Worldmap(name) : null;
            foreach (string n in new[] { name, name + "s", name + "es", name.EndsWith("y") ? name[..^1] + "ies" : name })
            {
                if (worldmap.Contains("vc-" + n)) return Worldmap("vc-" + n);
            }
            return art.Contains(name) && !name.StartsWith("base-") ? new AssetLocation("createacolony", "textures/icons/vcart/" + name + ".svg") : null;
        }

        var tries = new List<string>();
        if (f.Category == Ore && ColonyManager.OreElementIcon(f.Key) is { } element) tries.Add(element);
        if (ArtAliases.TryGetValue(f.Key, out var alias)) tries.Add(alias);
        var parts = f.Key.Split('-', '_');
        tries.Add(f.Key);
        tries.AddRange(parts.Reverse());
        foreach (string t in tries)
        {
            if (Try(t) is { } loc) { f.Art = loc; f.OwnArt = true; return; }
        }
        // A picture whose name is in the thing's ("redapple" has "apple").
        string? inside = art.Where(a => !a.StartsWith("base-") && a.Length >= 4 && f.Key.Contains(a)).OrderByDescending(a => a.Length).FirstOrDefault();
        if (inside != null) { f.Art = new AssetLocation("createacolony", "textures/icons/vcart/" + inside + ".svg"); f.OwnArt = true; return; }
        f.Art = new AssetLocation("createacolony", "textures/icons/vcart/base-" + f.Category + ".svg");
        f.OwnArt = false;
    }

    static AssetLocation Worldmap(string name) => new("createacolony", "textures/icons/worldmap/" + name + ".svg");

    /// <summary>Letters for things that share a picture: the first letter of each name, more where two match.</summary>
    static Dictionary<Found, string> Badges(List<Found> members)
    {
        var result = new Dictionary<Found, string>();
        for (int length = 1; length <= 3; length++)
        {
            foreach (var f in members.Where(m => !result.ContainsKey(m)))
            {
                string letters = Letters(f.Name.Length > 0 ? f.Name : f.Key, length);
                bool clash = members.Any(o => o != f && Letters(o.Name.Length > 0 ? o.Name : o.Key, length) == letters);
                if (!clash || length == 3) result[f] = letters;
            }
        }
        return result;
    }

    /// <summary>"Native copper" → "N", "Nc", "Nco"; words after the first give their first letter first.</summary>
    static string Letters(string name, int length)
    {
        var words = Regex.Split(name, "[^\\p{L}\\p{N}]+").Where(w => w.Length > 0).ToArray();
        if (words.Length == 0) return "?";
        string pool = words[0][0] + string.Concat(words.Skip(1).Select(w => w[0])) + words[0][1..];
        string s = pool[..Math.Min(length, pool.Length)].ToLowerInvariant();
        return char.ToUpperInvariant(s[0]) + s[1..];
    }

    static string CreatureName(EntityProperties props)
    {
        string name = Lang.GetIfExists(props.Code.Domain + ":item-creature-" + props.Code.Path) ?? Pretty(props.Code.Path.Split('-')[0]);
        // "Wolf (male)" → "Wolf"
        int paren = name.IndexOf(" (", StringComparison.Ordinal);
        return paren > 0 ? name[..paren] : name;
    }

    static string Pretty(string code)
    {
        string s = code.Replace('_', ' ').Replace('-', ' ');
        return s.Length == 0 ? code : char.ToUpperInvariant(s[0]) + s[1..];
    }

    /// <summary>The codes of every story location in the worldgen files, the game's and other mods'.</summary>
    public static List<string> StoryCodes(ICoreAPI api)
    {
        var codes = new List<string>();
        foreach (var asset in api.Assets.GetMany("worldgen/storystructures.json"))
        {
            try
            {
                var root = JToken.Parse(asset.ToText());
                foreach (var structure in root["structures"] as JArray ?? new JArray())
                {
                    if (structure["code"]?.ToString() is { Length: > 0 } code && !codes.Contains(code)) codes.Add(code);
                }
            }
            catch (Exception e)
            {
                api.Logger.Warning("[createacolony] Could not read the story locations in {0}: {1}", asset.Location, e.Message);
            }
        }
        return codes;
    }

    // ---- The client: the icons in the game's waypoint list --------------------------------------

    /// <summary>
    /// Adds every thing's icon to the game's waypoint icons, with its name as the Add waypoint window's name
    /// suggestion. Icons the game has already (our worldmap ones) are left as they are.
    /// </summary>
    public static void RegisterIcons(ICoreClientAPI capi, WaypointMapLayer layer)
    {
        var lang = Lang.AvailableLanguages.TryGetValue(Lang.CurrentLocale, out var current) ? current.GetAllEntries() : null;
        foreach (var subject in All(capi))
        {
            if (layer.WaypointIcons.ContainsKey(subject.Icon)) continue;
            var s = subject;
            layer.WaypointIcons[s.Icon] = () => Texture(capi, s);
            capi.Gui.Icons.CustomIcons["wp" + s.Icon.UcFirst()] = (ctx, x, y, w, h, rgba) =>
                Draw(capi, s, ctx, ctx.GetTarget() as ImageSurface, x, y, w, h, rgba);
            if (layer.texturesByIcon != null && !layer.texturesByIcon.ContainsKey(s.Icon)) layer.texturesByIcon[s.Icon] = Texture(capi, s);
            string suggestion = "game:wpSuggestion-" + s.Icon;
            if (lang != null && !lang.ContainsKey(suggestion)) lang[suggestion] = s.Name;
        }
    }

    static LoadedTexture Texture(ICoreClientAPI capi, MarkerSubject s)
    {
        int size = (int)Math.Ceiling(20 * RuntimeEnv.GUIScale);
        using var surface = new ImageSurface(Format.Argb32, size, size);
        using (var ctx = new Context(surface))
        {
            Draw(capi, s, ctx, surface, 0, 0, size, size, new double[] { 1, 1, 1, 1 });
        }
        return new LoadedTexture(capi, capi.Gui.LoadCairoTexture(surface, true), size, size);
    }

    /// <summary>Draws the thing's picture, shrunk to the top left with its letters in the bottom right if it has any.</summary>
    static void Draw(ICoreClientAPI capi, MarkerSubject s, Context ctx, ImageSurface? surface, double x, double y, double w, double h, double[] rgba)
    {
        if (surface == null || capi.Assets.TryGet(s.Art) is not { } asset) return;
        int color = ColorUtil.ColorFromRgba(rgba);
        if (s.Badge.Length == 0)
        {
            capi.Gui.DrawSvg(asset, surface, ctx.Matrix, (int)x, (int)y, (int)w, (int)h, color);
            return;
        }
        capi.Gui.DrawSvg(asset, surface, ctx.Matrix, (int)x, (int)y, (int)(w * 0.74), (int)(h * 0.74), color);

        ctx.Save();
        ctx.SetSourceRGBA(rgba[0], rgba[1], rgba[2], rgba.Length > 3 ? rgba[3] : 1);
        ctx.SelectFontFace("Sans", FontSlant.Normal, FontWeight.Bold);
        double box = Math.Min(w, h) * 0.46;
        ctx.SetFontSize(box);
        var ext = ctx.TextExtents(s.Badge);
        double scale = Math.Min(box / Math.Max(1, ext.Width), box / Math.Max(1, ext.Height));
        ctx.SetFontSize(box * scale);
        ext = ctx.TextExtents(s.Badge);
        ctx.MoveTo(x + w - ext.Width - ext.XBearing, y + h - (ext.Height + ext.YBearing));
        ctx.ShowText(s.Badge);
        ctx.Restore();
    }
}
