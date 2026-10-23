using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace CreateAColony.Colonies;

/// <summary>
/// One block a builder has to get right. BlockId 0 means "clear this spot". Saved, when set, holds the
/// block's saved details from the export (a VS Roofing roof's frame, shape and materials).
/// </summary>
public readonly record struct BuildStep(int Dx, int Dy, int Dz, int BlockId, SavedBlockEntity? Saved = null);

/// <summary>
/// A block entity's data as the WorldEdit export stored it, with the export it came from: item and block
/// numbers inside the data are that world's, and the export's code lists turn them into this world's.
/// </summary>
public class SavedBlockEntity
{
    public required string Data;
    public required BlockSchematic Source;

    /// <summary>A fresh copy of the saved data; callers may change it.</summary>
    public TreeAttribute Tree() => Source.DecodeBlockEntityData(Data);
}

/// <summary>A blueprint turned into an ordered list of build steps, relative to the hut block.</summary>
public class Blueprint
{
    public required string Type;
    public required int Level;
    public required List<BuildStep> Steps;
    /// <summary>
    /// The side the building's entrance is on (0 east, 1 north, 2 west, 3 south, as BlockFacing's
    /// HorizontalAngleIndex), or null when the blueprint has no door. Worked out from the first door.
    /// </summary>
    public int? Front;
}

/// <summary>
/// Finds and prepares blueprints. A blueprint is a normal vanilla WorldEdit export (/we export), so
/// buildings are made in-game. They are looked up as blueprints/&lt;type&gt;/level&lt;N&gt;.json for the
/// Testing style and blueprints/&lt;style&gt;/&lt;type&gt;/level&lt;N&gt;.json for the others, first in
/// ModConfig/createacolony/ (so players can add their own without rebuilding the mod), then in the
/// mod's own assets. If the export contains the building's hut block, that is where the hut sits;
/// otherwise the hut goes at the bottom centre of the blueprint.
/// </summary>
public class BlueprintLibrary
{
    public const int MaxLevels = 5;

    readonly ICoreServerAPI sapi;
    readonly ColonyConfig config;
    readonly Dictionary<(string, string, int, int), Blueprint?> cache = new();
    readonly Dictionary<(string Style, string Type), int> maxLevels = new();

    public BlueprintLibrary(ICoreServerAPI sapi, ColonyConfig config)
    {
        this.sapi = sapi;
        this.config = config;
    }

    public static string UserFolder => Path.Combine(GamePaths.ModConfig, "createacolony", "blueprints");

    /// <summary>The highest level that has a blueprint in this style, 0 if none.</summary>
    public int MaxLevel(string style, string type)
    {
        style = BlueprintStyles.Normalize(style);
        if (maxLevels.TryGetValue((style, type), out int known)) return known;
        int max = 0;
        for (int level = 1; level <= MaxLevels; level++)
        {
            if (FindJson(style, type, level) != null) max = level;
            else break;
        }
        maxLevels[(style, type)] = max;
        return max;
    }

    /// <summary>Forgets loaded blueprints, so edited files are picked up without a restart.</summary>
    public void Reload()
    {
        cache.Clear();
        maxLevels.Clear();
    }

    public int MaxLevel(Building building) => MaxLevel(StyleFor(building), building.Type);

    /// <summary>
    /// The style a building's blueprints come from: its own, or, when that style has none for its type (a
    /// Town Hall from before its blueprints moved to Dev's Builds), the first style that has some.
    /// </summary>
    public string StyleFor(Building building)
    {
        string own = BlueprintStyles.Normalize(building.Style);
        if (MaxLevel(own, building.Type) > 0) return own;
        return BlueprintStyles.All.FirstOrDefault(style => MaxLevel(style, building.Type) > 0) ?? own;
    }

    public Blueprint? Get(string style, string type, int level, int rotation)
    {
        style = BlueprintStyles.Normalize(style);
        var key = (style, type, level, rotation);
        if (!cache.TryGetValue(key, out var blueprint))
        {
            blueprint = Load(style, type, level, rotation);
            cache[key] = blueprint;
        }
        return blueprint;
    }

    /// <summary>The building's blueprint for a level, in its own style and turn.</summary>
    public Blueprint? Get(Building building, int level) => Get(StyleFor(building), building.Type, level, building.Rotation);

    string? FindJson(string style, string type, int level)
    {
        string folder = BlueprintStyles.Folder(style);
        string file = Path.Combine(folder == "" ? UserFolder : Path.Combine(UserFolder, folder), type, $"level{level}.json");
        if (File.Exists(file)) return File.ReadAllText(file);
        string asset = folder == "" ? $"config/blueprints/{type}/level{level}.json" : $"config/blueprints/{folder}/{type}/level{level}.json";
        return sapi.Assets.TryGet(new AssetLocation(CreateAColonyModSystem.ModId, asset))?.ToText();
    }

    Blueprint? Load(string style, string type, int level, int rotation)
    {
        string? json = FindJson(style, type, level);
        if (json == null) return null;

        string error = "";
        var schematic = BlockSchematic.LoadFromString(json, ref error);
        if (schematic == null)
        {
            sapi.Logger.Error("[createacolony] Blueprint {0} level {1} ({2}) could not be read: {3}", type, level, style, error);
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
            // A torch that burned out while the building was being saved is built as a lit one.
            if (block != null && ColonyManager.IsTorch(block) && ba.GetBlock(ColonyManager.LitTorch(block.Code)) is { } lit) block = lit;
            if (block == null) missing.Add(code.ToString());
            else blocks[(dx, dy, dz)] = block;
        }
        if (missing.Count > 0)
        {
            sapi.Logger.Warning("[createacolony] Blueprint {0} level {1} ({2}) uses blocks that are not installed, they will be skipped: {3}",
                type, level, style, string.Join(", ", missing));
        }

        // Saved details for the blocks the config lets builders copy (never chests: their contents would come free).
        var saved = new Dictionary<(int, int, int), SavedBlockEntity>();
        foreach (var (index, data) in schematic.BlockEntities)
        {
            var at = ((int)(index & BlockSchematic.PosBitMask), (int)((index >> 20) & BlockSchematic.PosBitMask), (int)((index >> 10) & BlockSchematic.PosBitMask));
            if (blocks.TryGetValue(at, out var withData) && KeepsDetails(withData)) saved[at] = new SavedBlockEntity { Data = data, Source = schematic };
        }

        int? front = rotation == 0 ? EntranceSide(schematic, blocks) : null;

        // The lowest and highest block of each column, to tell a cellar (empty space under a floor, with saved
        // ground below it) from the ground underneath or a yard that was lower than the floor.
        var lowest = new Dictionary<(int, int), int>();
        var highest = new Dictionary<(int, int), int>();
        foreach (var ((x, y, z), _) in blocks)
        {
            if (!lowest.TryGetValue((x, z), out int low) || y < low) lowest[(x, z)] = y;
            if (!highest.TryGetValue((x, z), out int high) || y > high) highest[(x, z)] = y;
        }
        bool InCellar(int x, int y, int z) =>
            lowest.TryGetValue((x, z), out int low) && low < y && highest.TryGetValue((x, z), out int high) && high > y;

        // Where the hut block sits inside the blueprint.
        string hutCode = HutBlockCode(type);
        var anchor = blocks.FirstOrDefault(kv => kv.Value.Code.ToString() == hutCode).Key;
        if (!blocks.Any(kv => kv.Value.Code.ToString() == hutCode)) anchor = (schematic.SizeX / 2, 0, schematic.SizeZ / 2);
        var (ax, ay, az) = anchor;

        // Items lying on the ground (ground storage) are left out too: an empty one is nothing, and a full one would come free.
        bool IsMeta(Block b) => b.Id == BlockSchematic.FillerBlockId || b.Id == BlockSchematic.PathwayBlockId
            || b.Id == BlockSchematic.UndergroundBlockId || b.Id == BlockSchematic.AbovegroundBlockId || b.Id == BlockSchematic.ConnectorBlockId
            || b is BlockGroundStorage;

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
                // The top half of a door (or another part of a big block) comes with the door itself; until then the spot is cleared.
                if (block is BlockMultiblock || IsFurniturePart(block, saved.GetValueOrDefault((dx, dy, dz))))
                {
                    clear.Add(new BuildStep(dx - ax, dy - ay, dz - az, 0));
                    continue;
                }
                var step = new BuildStep(dx - ax, dy - ay, dz - az, block.Id, saved.GetValueOrDefault((dx, dy, dz)));
                if (block.Id == 0) clear.Add(step);
                else if (IsSolid(block)) solid.Add(step);
                else decor.Add(step);
            }
            else if (dy >= ay || InCellar(dx, dy, dz))
            {
                // Empty space at or above the hut's floor is cleared. Below it, only with blueprint blocks both
                // under and over it (a cellar dug into the ground it was saved with); elsewhere the ground is left alone.
                clear.Add(new BuildStep(dx - ax, dy - ay, dz - az, 0));
            }
        }

        // Clear from the top down so nothing is left floating, then walls bottom-up, then torches, doors and the like.
        clear.Reverse();
        return new Blueprint { Type = type, Level = level, Steps = clear.Concat(solid).Concat(decor).ToList(), Front = front };
    }

    /// <summary>
    /// The side of the blueprint its first door is on. A door's saved turn says which way it faces, which
    /// gives the wall it sits in (east or west, north or south); the nearer edge of the blueprint on that line
    /// is the outside, whichever way round the door was hung.
    /// </summary>
    static int? EntranceSide(BlockSchematic schematic, Dictionary<(int, int, int), Block> blocks)
    {
        foreach (var (index, data) in schematic.BlockEntities)
        {
            var at = ((int)(index & BlockSchematic.PosBitMask), (int)((index >> 20) & BlockSchematic.PosBitMask), (int)((index >> 10) & BlockSchematic.PosBitMask));
            if (!blocks.TryGetValue(at, out var block) || !IsDoor(block)) continue;
            var facing = BlockFacing.HorizontalFromYaw(schematic.DecodeBlockEntityData(data).GetFloat("rotateYRad"));
            var (x, _, z) = at;
            if (facing.Axis == EnumAxis.X) return x <= schematic.SizeX - 1 - x ? BlockFacing.WEST.HorizontalAngleIndex : BlockFacing.EAST.HorizontalAngleIndex;
            return z <= schematic.SizeZ - 1 - z ? BlockFacing.NORTH.HorizontalAngleIndex : BlockFacing.SOUTH.HorizontalAngleIndex;
        }
        return null;
    }

    public static bool IsDoor(Block block) => block.HasBehavior<BlockBehaviorDoor>();

    static bool IsSolid(Block block) => block.SideSolid.Any;

    /// <summary>
    /// Doors always keep theirs (which way they face); blocks placed from their item keep theirs to know which
    /// item (its wood and colours, a pole's banner); other blocks when the config lists them.
    /// </summary>
    bool KeepsDetails(Block block) =>
        block.EntityClass != null && (IsDoor(block) || PlacedAsItem(block) || config.BlueprintBlockDetails.Any(p => WildcardUtil.Match(new AssetLocation(p), block.Code)));

    /// <summary>
    /// True for blocks a builder places from their item, as a player does, so they set themselves up: the
    /// config's list (W4RD0's Furniture, Heraldry : Banners), and any block that takes its look from the item's
    /// attributes (Attribute Rendering Library: "types" such as wood and cloth).
    /// </summary>
    public bool PlacedAsItem(Block block) =>
        block.Id != 0 && (UsesAttributes(block) || config.BlueprintPlaceAsItem.Any(p => WildcardUtil.Match(new AssetLocation(p), block.Code)));

    /// <summary>A block whose wood, cloth and the like live in its block entity's "types", copied from the item it was placed from.</summary>
    public static bool UsesAttributes(Block block) => block.BlockBehaviors.Any(IsAttributeBehavior);

    /// <summary>Attribute Rendering Library's block behavior, found by name so the library stays optional.</summary>
    public static bool IsAttributeBehavior(BlockBehavior behavior) => behavior.GetType().Name == "BlockBehaviorShapeTexturesFromAttributes";

    /// <summary>
    /// A cell of a W4RD0's Furniture shelf or display other than its first one. The first cell places the
    /// others when it goes down, so until then the spot is cleared, like the top half of a door.
    /// </summary>
    static bool IsFurniturePart(Block block, SavedBlockEntity? saved)
    {
        if (saved == null || block.GetType().Name != "BlockFurnitureDisplay") return false;
        var tree = saved.Tree();
        return tree.GetInt("partX") != 0 || tree.GetInt("partY") != 0 || tree.GetInt("partZ") != 0;
    }

    public static string HutBlockCode(string type) => type switch
    {
        BuildingTypes.TownHall => "createacolony:townhall",
        BuildingTypes.Warehouse => "createacolony:warehouse",
        BuildingTypes.SupplyCamp => "createacolony:supplycamp",
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
    public const string Cook = "cook";
    public const string GuardTower = "guardtower";
    public const string Residence = "residence";
    public const string Prospector = "prospector";
    public const string Miner = "miner";
    public const string Quarry = "quarry";
    /// <summary>Not a colony building: only used to find the supply camp's own blueprint.</summary>
    public const string SupplyCamp = "supplycamp";
}
