using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace CreateAColony.Colonies;

/// <summary>One block a builder has to get right. BlockId 0 means "clear this spot".</summary>
public readonly record struct BuildStep(int Dx, int Dy, int Dz, int BlockId);

/// <summary>A blueprint turned into an ordered list of build steps, relative to the hut block.</summary>
public class Blueprint
{
    public required string Type;
    public required int Level;
    public required List<BuildStep> Steps;
}

/// <summary>
/// Finds and prepares blueprints. A blueprint is a normal vanilla WorldEdit export (/we export), so
/// buildings are made in-game. They are looked up as blueprints/&lt;type&gt;/level&lt;N&gt;.json, first in
/// ModConfig/createacolony/ (so players can add their own without rebuilding the mod), then in the
/// mod's own assets. If the export contains the building's hut block, that is where the hut sits;
/// otherwise the hut goes at the bottom centre of the blueprint.
/// </summary>
public class BlueprintLibrary
{
    public const int MaxLevels = 5;

    readonly ICoreServerAPI sapi;
    readonly Dictionary<(string, int, int), Blueprint?> cache = new();

    public BlueprintLibrary(ICoreServerAPI sapi) => this.sapi = sapi;

    public static string UserFolder => Path.Combine(GamePaths.ModConfig, "createacolony", "blueprints");

    /// <summary>The highest level that has a blueprint, 0 if none.</summary>
    public int MaxLevel(string type)
    {
        int max = 0;
        for (int level = 1; level <= MaxLevels; level++)
        {
            if (FindJson(type, level) != null) max = level;
            else break;
        }
        return max;
    }

    /// <summary>Forgets loaded blueprints, so edited files are picked up without a restart.</summary>
    public void Reload() => cache.Clear();

    public Blueprint? Get(string type, int level, int rotation)
    {
        var key = (type, level, rotation);
        if (!cache.TryGetValue(key, out var blueprint))
        {
            blueprint = Load(type, level, rotation);
            cache[key] = blueprint;
        }
        return blueprint;
    }

    string? FindJson(string type, int level)
    {
        string file = Path.Combine(UserFolder, type, $"level{level}.json");
        if (File.Exists(file)) return File.ReadAllText(file);
        return sapi.Assets.TryGet(new AssetLocation(CreateAColonyModSystem.ModId, $"config/blueprints/{type}/level{level}.json"))?.ToText();
    }

    Blueprint? Load(string type, int level, int rotation)
    {
        string? json = FindJson(type, level);
        if (json == null) return null;

        string error = "";
        var schematic = BlockSchematic.LoadFromString(json, ref error);
        if (schematic == null)
        {
            sapi.Logger.Error("[createacolony] Blueprint {0} level {1} could not be read: {2}", type, level, error);
            return null;
        }
        schematic.Init(sapi.World.BlockAccessor);
        if (rotation != 0) schematic.TransformWhilePacked(sapi.World, EnumOrigin.BottomCenter, rotation);

        var ba = sapi.World.BlockAccessor;
        var blocks = new Dictionary<(int, int, int), Block>();
        var missing = new HashSet<string>();
        for (int i = 0; i < schematic.Indices.Count; i++)
        {
            uint index = schematic.Indices[i];
            int dx = (int)(index & BlockSchematic.PosBitMask);
            int dy = (int)((index >> 20) & BlockSchematic.PosBitMask);
            int dz = (int)((index >> 10) & BlockSchematic.PosBitMask);
            var code = schematic.BlockCodes[schematic.BlockIds[i]];
            var block = ba.GetBlock(code);
            if (block == null) missing.Add(code.ToString());
            else blocks[(dx, dy, dz)] = block;
        }
        if (missing.Count > 0)
        {
            sapi.Logger.Warning("[createacolony] Blueprint {0} level {1} uses blocks that are not installed, they will be skipped: {2}",
                type, level, string.Join(", ", missing));
        }

        // Where the hut block sits inside the blueprint.
        string hutCode = HutBlockCode(type);
        var anchor = blocks.FirstOrDefault(kv => kv.Value.Code.ToString() == hutCode).Key;
        if (!blocks.Any(kv => kv.Value.Code.ToString() == hutCode)) anchor = (schematic.SizeX / 2, 0, schematic.SizeZ / 2);
        var (ax, ay, az) = anchor;

        bool IsMeta(Block b) => b.Id == BlockSchematic.FillerBlockId || b.Id == BlockSchematic.PathwayBlockId
            || b.Id == BlockSchematic.UndergroundBlockId || b.Id == BlockSchematic.AbovegroundBlockId || b.Id == BlockSchematic.ConnectorBlockId;

        var clear = new List<BuildStep>();
        var solid = new List<BuildStep>();
        var decor = new List<BuildStep>();
        for (int dy = 0; dy < schematic.SizeY; dy++)
        for (int dx = 0; dx < schematic.SizeX; dx++)
        for (int dz = 0; dz < schematic.SizeZ; dz++)
        {
            if ((dx, dy, dz) == anchor) continue;
            if (blocks.TryGetValue((dx, dy, dz), out var block))
            {
                if (IsMeta(block)) continue;
                var step = new BuildStep(dx - ax, dy - ay, dz - az, block.Id);
                if (block.Id == 0) clear.Add(step);
                else if (IsSolid(block)) solid.Add(step);
                else decor.Add(step);
            }
            else if (dy >= ay)
            {
                // Empty space at or above the hut's floor is cleared; below it the ground is left alone.
                clear.Add(new BuildStep(dx - ax, dy - ay, dz - az, 0));
            }
        }

        // Clear from the top down so nothing is left floating, then walls bottom-up, then torches, doors and the like.
        clear.Reverse();
        return new Blueprint { Type = type, Level = level, Steps = clear.Concat(solid).Concat(decor).ToList() };
    }

    static bool IsSolid(Block block) => block.SideSolid.Any;

    public static string HutBlockCode(string type) => type switch
    {
        BuildingTypes.TownHall => "createacolony:townhall",
        BuildingTypes.Warehouse => "createacolony:warehouse",
        _ => $"createacolony:hut-{type}"
    };
}

public static class BuildingTypes
{
    public const string TownHall = "townhall";
    public const string Warehouse = "warehouse";
    public const string Builder = "builder";
    public const string Forester = "forester";
    public const string Farmer = "farmer";
    public const string Potter = "potter";
    public const string Tavern = "tavern";
}
