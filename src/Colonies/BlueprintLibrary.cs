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
    /// <summary>
    /// Waypoint blocks in the blueprint, by kind ("mine", "quarry"), as offsets from the hut block: where
    /// the Miner's Hut's shaft or the Quarry's pit goes.
    /// </summary>
    public Dictionary<string, (int Dx, int Dy, int Dz)> Waypoints = new();
}

/// <summary>
/// One piece of a Miner's Hut's mineshaft (BlueprintLibrary.GetMinePiece): its steps as offsets from where it
/// lines up (the entrance's Mine waypoint, a section's bottom middle), digging first, top down; the sides it
/// is open on, each with the middle of that edge (X, Z offsets; on its bottom layer); and its box around that spot.
/// </summary>
public class MinePiece
{
    public required string Name;
    public required List<BuildStep> Steps;
    public required List<(BlockFacing Face, int X, int Z)> Open;
    public (int X, int Y, int Z) Min, Max;
}

/// <summary>
/// Finds and prepares blueprints. A blueprint is a normal vanilla WorldEdit export (/we export), so
/// buildings are made in-game. They are looked up as blueprints/&lt;type&gt;/level&lt;N&gt;.json for the
/// Testing style and blueprints/&lt;style&gt;/&lt;type&gt;/level&lt;N&gt;.json for the others, first in
/// ModConfig/createacolony/ (so players can add their own without rebuilding the mod), then in the
/// mod's own assets. If the export contains the building's hut block, that is where the hut sits;
/// otherwise the hut goes at the bottom centre of the blueprint.
/// A type can have variants, other buildings of the same type that players pick between when placing
/// one (a small or a medium house): blueprints/&lt;style&gt;/&lt;type&gt;/&lt;variant&gt;/level&lt;N&gt;.json. The
/// files right in the type's folder are the variant named "" and come first.
/// </summary>
public class BlueprintLibrary
{
    public const int MaxLevels = 5;

    readonly ICoreServerAPI sapi;
    readonly ColonyConfig config;
    readonly Dictionary<(string, string, string, int, int), Blueprint?> cache = new();
    readonly Dictionary<(string Style, string Type, string Variant), int> maxLevels = new();
    readonly Dictionary<(string Style, string Type), List<string>> variants = new();

    public BlueprintLibrary(ICoreServerAPI sapi, ColonyConfig config)
    {
        this.sapi = sapi;
        this.config = config;
    }

    public static string UserFolder => Path.Combine(GamePaths.ModConfig, "createacolony", "blueprints");

    /// <summary>The highest level that has a blueprint in this style and variant (the first variant when it has none of that name), 0 if none.</summary>
    public int MaxLevel(string style, string type, string variant = "")
    {
        style = BlueprintStyles.Normalize(style);
        variant = VariantFor(style, type, variant);
        if (maxLevels.TryGetValue((style, type, variant), out int known)) return known;
        int max = 0;
        for (int level = 1; level <= MaxLevels; level++)
        {
            if (FindJson(style, type, variant, level) != null) max = level;
            else break;
        }
        maxLevels[(style, type, variant)] = max;
        return max;
    }

    /// <summary>
    /// The variants of a type in this style that have a level 1: "" (the files right in the type's folder)
    /// first, then its subfolders in alphabetical order. Empty when the type has no blueprints here.
    /// </summary>
    public List<string> Variants(string style, string type)
    {
        style = BlueprintStyles.Normalize(style);
        if (variants.TryGetValue((style, type), out var known)) return known;
        string folder = BlueprintStyles.Folder(style);
        var names = new SortedSet<string>(StringComparer.Ordinal);
        string userType = Path.Combine(UserFolder, folder, type);
        if (Directory.Exists(userType))
        {
            foreach (var dir in Directory.GetDirectories(userType)) names.Add(Path.GetFileName(dir));
        }
        string prefix = $"config/blueprints/{folder}/{type}/";
        foreach (var asset in sapi.Assets.GetMany(prefix, CreateAColonyModSystem.ModId, false))
        {
            var rest = asset.Location.Path.Substring(prefix.Length).Split('/');
            if (rest.Length == 2) names.Add(rest[0]);
        }
        var list = new List<string>();
        if (FindJson(style, type, "", 1) != null) list.Add("");
        list.AddRange(names.Where(n => IsVariantName(n) && FindJson(style, type, n, 1) != null));
        variants[(style, type)] = list;
        return list;
    }

    /// <summary>Variant names are plain lower-case words and numbers, so they are safe in file paths and messages.</summary>
    public static bool IsVariantName(string name) => name.Length > 0 && name.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9');

    /// <summary>The variant itself when the type has it in this style, otherwise its first one ("" when it has none).</summary>
    public string VariantFor(string style, string type, string? variant)
    {
        var list = Variants(style, type);
        return variant != null && list.Contains(variant) ? variant : list.FirstOrDefault() ?? "";
    }

    /// <summary>Forgets loaded blueprints, so edited files are picked up without a restart.</summary>
    public void Reload()
    {
        cache.Clear();
        mineCache.Clear();
        maxLevels.Clear();
        variants.Clear();
    }

    public int MaxLevel(Building building) => MaxLevel(StyleFor(building), building.Type, building.Variant);

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

    public Blueprint? Get(string style, string type, int level, int rotation, string variant = "")
    {
        style = BlueprintStyles.Normalize(style);
        variant = VariantFor(style, type, variant);
        var key = (style, type, variant, level, rotation);
        if (!cache.TryGetValue(key, out var blueprint))
        {
            blueprint = Load(style, type, variant, level, rotation);
            cache[key] = blueprint;
        }
        return blueprint;
    }

    /// <summary>The building's blueprint for a level, in its own style, variant and turn.</summary>
    public Blueprint? Get(Building building, int level) => Get(StyleFor(building), building.Type, level, building.Rotation, building.Variant);

    string? FindJson(string style, string type, string variant, int level)
    {
        string folder = BlueprintStyles.Folder(style);
        string typeFolder = variant == "" ? type : $"{type}/{variant}";
        string file = Path.Combine(folder == "" ? UserFolder : Path.Combine(UserFolder, folder), typeFolder, $"level{level}.json");
        if (File.Exists(file)) return File.ReadAllText(file);
        string asset = folder == "" ? $"config/blueprints/{typeFolder}/level{level}.json" : $"config/blueprints/{folder}/{typeFolder}/level{level}.json";
        return sapi.Assets.TryGet(new AssetLocation(CreateAColonyModSystem.ModId, asset))?.ToText();
    }

    Blueprint? Load(string style, string type, string variant, int level, int rotation)
    {
        string? json = FindJson(style, type, variant, level);
        if (variant != "") style += "/" + variant;
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

        // Waypoints mark where a mine shaft or a quarry pit goes. The builder only clears their spot, and
        // leaves out the blueprint's blocks where the miner or quarrier will dig, so the two never fight.
        var waypoints = new Dictionary<string, (int Dx, int Dy, int Dz)>();
        foreach (var ((x, y, z), b) in blocks)
        {
            if (IsWaypoint(b)) waypoints[b.Variant["type"] ?? b.Code.Path] = (x - ax, y - ay, z - az);
        }
        var dugOut = DugOut(waypoints);

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
                if (IsWaypoint(block))
                {
                    clear.Add(new BuildStep(dx - ax, dy - ay, dz - az, 0));
                    continue;
                }
                if (dugOut.Contains((dx - ax, dy - ay, dz - az))) continue;
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
            else if (dugOut.Contains((dx - ax, dy - ay, dz - az))) continue;
            else if (dy >= ay || InCellar(dx, dy, dz))
            {
                // Empty space at or above the hut's floor is cleared. Below it, only with blueprint blocks both
                // under and over it (a cellar dug into the ground it was saved with); elsewhere the ground is left alone.
                clear.Add(new BuildStep(dx - ax, dy - ay, dz - az, 0));
            }
        }

        // Clear from the top down so nothing is left floating, then walls bottom-up, then torches, doors and the like.
        clear.Reverse();
        return new Blueprint { Type = type, Level = level, Steps = clear.Concat(solid).Concat(decor).ToList(), Front = front, Waypoints = waypoints };
    }

    // ---- Mineshaft pieces ------------------------------------------------------------------

    /// <summary>The pieces a Miner's Hut's mineshaft is made of: the way in, and the sections it grows from there.</summary>
    public const string MineEntrance = "entrance";
    public static readonly string[] MineSections = { "cross", "tee", "straight" };

    readonly Dictionary<(string, string, int), MinePiece?> mineCache = new();

    /// <summary>A mineshaft piece (blueprints/&lt;style&gt;/mineshaft/&lt;name&gt;.json) turned by rotation degrees, or null when there is none.</summary>
    public MinePiece? GetMinePiece(string style, string name, int rotation)
    {
        style = BlueprintStyles.Normalize(style);
        var key = (style, name, rotation);
        if (!mineCache.TryGetValue(key, out var piece)) mineCache[key] = piece = LoadMinePiece(style, name, rotation);
        return piece;
    }

    string? FindMineJson(string style, string name)
    {
        string folder = BlueprintStyles.Folder(style);
        string file = Path.Combine(UserFolder, folder, "mineshaft", name + ".json");
        if (File.Exists(file)) return File.ReadAllText(file);
        return sapi.Assets.TryGet(new AssetLocation(CreateAColonyModSystem.ModId, $"config/blueprints/{folder}/mineshaft/{name}.json"))?.ToText();
    }

    /// <summary>
    /// Reads a mineshaft piece. Unlike a building, its natural ground (rock, soil, gravel, sand, ore) is
    /// left as the miner finds it, every empty spot in it is dug out, and everything else (planks, logs,
    /// ladders, torches) is built. The entrance lines up by its Mine waypoint with the hut's (or else the
    /// middle of its top); a section by the middle of its bottom. Its open sides are the sides with an
    /// empty spot in the middle of the edge, in its bottom four layers.
    /// </summary>
    MinePiece? LoadMinePiece(string style, string name, int rotation)
    {
        string? json = FindMineJson(style, name);
        if (json == null) return null;
        string error = "";
        var schematic = BlockSchematic.LoadFromString(json, ref error);
        if (schematic == null)
        {
            sapi.Logger.Error("[createacolony] Mineshaft piece {0} ({1}) could not be read: {2}", name, style, error);
            return null;
        }
        schematic.Init(sapi.World.BlockAccessor);
        if (rotation != 0) schematic.TransformWhilePacked(sapi.World, EnumOrigin.BottomCenter, rotation);

        var ba = sapi.World.BlockAccessor;
        var blocks = new Dictionary<(int, int, int), Block>();
        for (int i = 0; i < schematic.Indices.Count; i++)
        {
            uint index = schematic.Indices[i];
            var at = ((int)(index & BlockSchematic.PosBitMask), (int)((index >> 20) & BlockSchematic.PosBitMask), (int)((index >> 10) & BlockSchematic.PosBitMask));
            var block = ba.GetBlock(schematic.BlockCodes[schematic.BlockIds[i]]);
            if (block != null && ColonyManager.IsTorch(block) && ba.GetBlock(ColonyManager.LitTorch(block.Code)) is { } lit) block = lit;
            if (block != null) blocks[at] = block;
        }
        var saved = new Dictionary<(int, int, int), SavedBlockEntity>();
        foreach (var (index, data) in schematic.BlockEntities)
        {
            var at = ((int)(index & BlockSchematic.PosBitMask), (int)((index >> 20) & BlockSchematic.PosBitMask), (int)((index >> 10) & BlockSchematic.PosBitMask));
            if (blocks.TryGetValue(at, out var withData) && KeepsDetails(withData)) saved[at] = new SavedBlockEntity { Data = data, Source = schematic };
        }

        int sx = schematic.SizeX, sy = schematic.SizeY, sz = schematic.SizeZ;
        var anchor = (sx / 2, 0, sz / 2);
        if (name == MineEntrance)
        {
            anchor = (sx / 2, sy - 1, sz / 2);
            foreach (var (at, b) in blocks) if (IsWaypoint(b) && (b.Variant["type"] ?? "") == "mine") anchor = at;
        }
        var (ax, ay, az) = anchor;

        var digs = new List<BuildStep>();
        var solid = new List<BuildStep>();
        var decor = new List<BuildStep>();
        for (int dy = sy - 1; dy >= 0; dy--)
        for (int dx = 0; dx < sx; dx++)
        for (int dz = 0; dz < sz; dz++)
        {
            if (!blocks.TryGetValue((dx, dy, dz), out var block) || IsWaypoint(block) || block.Id == 0
                || block is BlockMultiblock || IsFurniturePart(block, saved.GetValueOrDefault((dx, dy, dz))))
            {
                digs.Add(new BuildStep(dx - ax, dy - ay, dz - az, 0));
                continue;
            }
            if (IsMetaBlock(block) || IsNaturalGround(block)) continue;
            var step = new BuildStep(dx - ax, dy - ay, dz - az, block.Id, saved.GetValueOrDefault((dx, dy, dz)));
            if (IsSolid(block)) solid.Add(step);
            else decor.Add(step);
        }

        // An open side: an empty spot in the middle of that edge, low down where the way through is.
        bool Empty(int x, int y, int z) => !blocks.TryGetValue((x, y, z), out var b) || b.Id == 0 || IsWaypoint(b);
        var open = new List<(BlockFacing, int, int)>();
        foreach (var face in BlockFacing.HORIZONTALS)
        {
            int x = face == BlockFacing.EAST ? sx - 1 : face == BlockFacing.WEST ? 0 : sx / 2;
            int z = face == BlockFacing.SOUTH ? sz - 1 : face == BlockFacing.NORTH ? 0 : sz / 2;
            for (int y = 0; y < Math.Min(4, sy); y++)
            {
                if (!Empty(x, y, z)) continue;
                open.Add((face, x - ax, z - az));
                break;
            }
        }
        return new MinePiece
        {
            Name = name, Steps = digs.Concat(solid).Concat(decor).ToList(), Open = open,
            Min = (-ax, -ay, -az), Max = (sx - 1 - ax, sy - 1 - ay, sz - 1 - az)
        };
    }

    /// <summary>
    /// The ground a mineshaft piece leaves as it is: rock, soil, gravel, sand, clay and ore as worldgen puts
    /// them down, and water or lava. Built stone (cobblestone, bricks) does not count and is built.
    /// </summary>
    public static bool IsNaturalGround(Block block)
    {
        if (block.IsLiquid() || block.BlockMaterial is EnumBlockMaterial.Lava) return true;
        if (block.Code?.Domain != "game") return false;
        string path = block.Code.Path;
        return NaturalPrefixes.Any(path.StartsWith);
    }

    static readonly string[] NaturalPrefixes =
    {
        "rock-", "soil-", "gravel-", "sand-", "ore-", "crackedrock-", "rawclay-", "peat-", "muddygravel", "forestfloor",
        "looseboulders", "loosestones", "looseores", "loosegears", "sludgygravel", "suevite", "meteorite", "glacierice", "packedglacierice"
    };

    static bool IsMetaBlock(Block b) => b.Id == BlockSchematic.FillerBlockId || b.Id == BlockSchematic.PathwayBlockId
        || b.Id == BlockSchematic.UndergroundBlockId || b.Id == BlockSchematic.AbovegroundBlockId || b.Id == BlockSchematic.ConnectorBlockId
        || b is BlockGroundStorage;

    /// <summary>A Mine or Quarry waypoint block (createacolony:waypoint-mine, createacolony:waypoint-quarry).</summary>
    public static bool IsWaypoint(Block block) => block.Code?.Domain == CreateAColonyModSystem.ModId && block.Code.Path.StartsWith("waypoint-");

    /// <summary>
    /// The spots, as offsets from the hut block, that the miner or quarrier digs out around the blueprint's
    /// waypoints: the shaft's steps and the top of its tunnels below a Mine waypoint, and the widest,
    /// deepest pit below a Quarry waypoint. The blueprint's own blocks there are left out of the building.
    /// </summary>
    HashSet<(int, int, int)> DugOut(Dictionary<string, (int Dx, int Dy, int Dz)> waypoints)
    {
        var dug = new HashSet<(int, int, int)>();
        // Shaft plans are worked out well above the bottom of the world, then moved back.
        const int Lift = 10000;
        if (waypoints.TryGetValue("mine", out var mine))
        {
            foreach (var spot in ColonyManager.ShaftPlan(new BlockPos(mine.Dx, mine.Dy + Lift, mine.Dz), 3, Math.Max(1, config.MinerTunnelLength)))
            {
                for (int h = 0; h < spot.Height; h++) dug.Add((spot.Feet.X, spot.Feet.Y + h - Lift, spot.Feet.Z));
            }
        }
        if (waypoints.TryGetValue("quarry", out var pit))
        {
            int r = Math.Max(1, Math.Max(config.QuarryRadius, config.QuarryMaxRadius));
            int depth = Math.Clamp(config.QuarryDepthPerLevel * MaxLevels, 1, 8 * r - 1);
            for (int i = -r; i <= r; i++)
            for (int j = -r; j <= r; j++)
            for (int k = 1; k <= depth; k++)
            {
                dug.Add((pit.Dx + i, pit.Dy - k, pit.Dz + j));
            }
        }
        return dug;
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
    public const string Smelter = "smelter";
    public const string Blacksmith = "blacksmith";
    public const string Sawmill = "sawmill";
    public const string Barracks = "barracks";
    /// <summary>Not a colony building: only used to find the supply camp's own blueprint.</summary>
    public const string SupplyCamp = "supplycamp";
}
