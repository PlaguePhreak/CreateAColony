using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CreateAColony.Colonies;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace CreateAColony.Import;

/// <summary>
/// Turns a MineColonies .blueprint into a blueprint our builders use (a WorldEdit-style schematic).
/// Each Minecraft block is matched against an editable rule table (config/mcimport.json); a rule
/// lists Vintage Story block codes to try, and the first one installed in the running game wins.
/// Blocks with no match are left empty and reported, so they can be fixed up by hand in WorldEdit.
/// </summary>
public class MineColoniesImporter
{
    public class Rule
    {
        /// <summary>Regex on the Minecraft block name. Named groups "wood" and "rock" pick the material.</summary>
        public string Match = "";
        /// <summary>Optional block-state conditions, e.g. { "type": "double" } for double slabs.</summary>
        public Dictionary<string, string>? When;
        /// <summary>Vintage Story codes to try in order, with {placeholders}. "skip" leaves the spot untouched.</summary>
        public string[] Blocks = Array.Empty<string>();
    }

    public class ImportConfig
    {
        public Dictionary<string, string> Woods = new();
        public Dictionary<string, string> Rocks = new();
        public List<Rule> Rules = new();
    }

    public class Result
    {
        public int Blocks;
        public string HutInfo = "";
        public Dictionary<string, int> Unmatched = new();
    }

    const string Skip = "skip";
    const string Air = "game:air";

    readonly ICoreServerAPI sapi;
    readonly ImportConfig config;
    readonly List<(Regex Regex, Rule Rule)> rules;

    public MineColoniesImporter(ICoreServerAPI sapi, ImportConfig config)
    {
        this.sapi = sapi;
        this.config = config;
        rules = config.Rules.Select(r => (new Regex(r.Match, RegexOptions.CultureInvariant), r)).ToList();
    }

    public static string ImportFolder => Path.Combine(GamePaths.ModConfig, "createacolony", "import");

    /// <summary>The rule table: ModConfig/createacolony/mcimport.json if the player made one, else the mod's own.</summary>
    public static ImportConfig LoadConfig(ICoreServerAPI api)
    {
        string user = Path.Combine(GamePaths.ModConfig, "createacolony", "mcimport.json");
        if (File.Exists(user)) return JsonConvert.DeserializeObject<ImportConfig>(File.ReadAllText(user)) ?? new ImportConfig();
        return api.Assets.Get(new AssetLocation(CreateAColonyModSystem.ModId, "config/mcimport.json")).ToObject<ImportConfig>();
    }

    public Result Convert(string inputPath, string buildingType, string outputPath)
    {
        var nbt = NbtReader.ReadGzipFile(inputPath);
        int sx = Convert<int>(nbt["size_x"]), sy = Convert<int>(nbt["size_y"]), sz = Convert<int>(nbt["size_z"]);

        var palette = ((List<object>)nbt["palette"]).Cast<Dictionary<string, object>>().Select(p =>
        (
            Name: (string)p["Name"],
            Props: p.TryGetValue("Properties", out var props)
                ? ((Dictionary<string, object>)props).ToDictionary(kv => kv.Key, kv => kv.Value.ToString() ?? "")
                : new Dictionary<string, string>()
        )).ToList();

        // Blocks are stored as 16-bit palette indices, two per int, x fastest, then z, then y.
        var ints = (int[])nbt["blocks"];
        var indices = new ushort[sx * sy * sz];
        for (int i = 0; i < indices.Length; i++)
        {
            int packed = ints[i / 2];
            indices[i] = (ushort)(i % 2 == 0 ? (packed >> 16) & 0xffff : packed & 0xffff);
        }

        // Domum Ornamentum blocks (timber frames) keep their real material in their tile entity.
        var textures = new Dictionary<(int, int, int), string>();
        if (nbt.TryGetValue("tile_entities", out var tes))
        {
            foreach (var te in ((List<object>)tes).Cast<Dictionary<string, object>>())
            {
                if (te.TryGetValue("textureData", out var tex) && tex is Dictionary<string, object> map && map.Count > 0)
                {
                    textures[(Convert<int>(te["x"]), Convert<int>(te["y"]), Convert<int>(te["z"]))] = map.Values.First().ToString()!;
                }
            }
        }

        var hut = FindHut(nbt, palette.Select(p => p.Name).ToList(), indices, sx, sz);
        var result = new Result { HutInfo = hut == null ? "none" : $"{hut.Value.x},{hut.Value.y},{hut.Value.z}" };

        var codeIds = new Dictionary<string, int>();
        var outIndices = new List<uint>();
        var outIds = new List<int>();
        var cache = new Dictionary<string, string?>();

        for (int y = 0; y < sy; y++)
        for (int z = 0; z < sz; z++)
        for (int x = 0; x < sx; x++)
        {
            var (name, props) = palette[indices[(y * sz + z) * sx + x]];
            string? code;
            if (name.StartsWith("minecolonies:blockhut"))
            {
                code = hut != null && hut.Value == (x, y, z) ? BlueprintLibrary.HutBlockCode(buildingType) : Air;
            }
            else
            {
                if (name.StartsWith("domum_ornamentum:"))
                {
                    name = textures.TryGetValue((x, y, z), out var material) ? material : "minecraft:oak_planks";
                    props = new Dictionary<string, string>();
                }
                string key = name + "|" + string.Join(",", props.OrderBy(kv => kv.Key).Select(kv => kv.Key + "=" + kv.Value));
                if (!cache.TryGetValue(key, out code))
                {
                    code = Resolve(name, props);
                    cache[key] = code;
                }
                if (code == null)
                {
                    result.Unmatched[name] = result.Unmatched.GetValueOrDefault(name) + 1;
                    code = Air;
                }
            }
            if (code == Skip) continue;

            if (!codeIds.TryGetValue(code, out int id)) codeIds[code] = id = codeIds.Count;
            outIndices.Add((uint)x | ((uint)z << 10) | ((uint)y << 20));
            outIds.Add(id);
            if (code != Air) result.Blocks++;
        }

        var schematic = new JObject
        {
            ["GameVersion"] = GameVersion.ShortGameVersion,
            ["SizeX"] = sx,
            ["SizeY"] = sy,
            ["SizeZ"] = sz,
            ["BlockCodes"] = new JObject(codeIds.Select(kv => new JProperty(kv.Value.ToString(), kv.Key))),
            ["ItemCodes"] = new JObject(),
            ["Indices"] = new JArray(outIndices),
            ["BlockIds"] = new JArray(outIds),
            ["DecorIndices"] = new JArray(),
            ["DecorIds"] = new JArray(),
            ["BlockEntities"] = new JObject(),
            ["Entities"] = new JArray(),
            ["EntranceRotation"] = -1
        };
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllText(outputPath, schematic.ToString(Formatting.None));
        return result;
    }

    static (int x, int y, int z)? FindHut(Dictionary<string, object> nbt, List<string> names, ushort[] indices, int sx, int sz)
    {
        if (nbt.TryGetValue("optional_data", out var od) && od is Dictionary<string, object> data
            && data.TryGetValue("structurize", out var s) && s is Dictionary<string, object> structurize
            && structurize.TryGetValue("primary_offset", out var o) && o is Dictionary<string, object> offset)
        {
            return (Convert<int>(offset["x"]), Convert<int>(offset["y"]), Convert<int>(offset["z"]));
        }
        for (int i = 0; i < indices.Length; i++)
        {
            if (names[indices[i]].StartsWith("minecolonies:blockhut")) return (i % sx, i / (sx * sz), i / sx % sz);
        }
        return null;
    }

    /// <summary>The first installed Vintage Story block for this Minecraft block, "skip", or null if nothing matches.</summary>
    string? Resolve(string name, Dictionary<string, string> props)
    {
        foreach (var (regex, rule) in rules)
        {
            var m = regex.Match(name);
            if (!m.Success) continue;
            if (rule.When != null && rule.When.Any(kv => props.GetValueOrDefault(kv.Key) != kv.Value)) continue;

            var vars = Variables(m, props);
            foreach (string template in rule.Blocks)
            {
                if (template == Skip) return Skip;
                string code = Regex.Replace(template, @"\{(\w+)\}", v => vars.GetValueOrDefault(v.Groups[1].Value, v.Value));
                if (code.Contains('*'))
                {
                    // A wildcard picks the first installed block that fits, for names we can't be sure of.
                    var found = sapi.World.SearchBlocks(new AssetLocation(code)).Select(b => b.Code.ToString()).OrderBy(c => c.Length).ThenBy(c => c).FirstOrDefault();
                    if (found != null) return found;
                }
                else if (sapi.World.GetBlock(new AssetLocation(code)) != null)
                {
                    return code;
                }
            }
            return null; // the rule matched but none of its blocks are installed
        }
        return null;
    }

    Dictionary<string, string> Variables(Match m, Dictionary<string, string> props)
    {
        var vars = new Dictionary<string, string>(props);
        string wood = m.Groups["wood"].Success ? m.Groups["wood"].Value : "oak";
        string rock = m.Groups["rock"].Success ? m.Groups["rock"].Value : "stone";
        vars["vswood"] = config.Woods.GetValueOrDefault(wood, "oak");
        vars["vsrock"] = config.Rocks.GetValueOrDefault(rock, "granite");
        vars["vsaxis"] = props.GetValueOrDefault("axis") switch { "x" => "we", "z" => "ns", _ => "ud" };
        // Minecraft stairs say which half the step sits in; Vintage Story says whether the stair is upside down.
        vars["stairhalf"] = props.GetValueOrDefault("half") == "top" ? "down" : "up";
        vars["slabhalf"] = props.GetValueOrDefault("type") == "top" ? "up" : "down";
        vars["facing"] = props.GetValueOrDefault("facing", "north");
        return vars;
    }

    static T Convert<T>(object value) => (T)System.Convert.ChangeType(value, typeof(T));
}
