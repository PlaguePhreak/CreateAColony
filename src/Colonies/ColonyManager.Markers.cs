using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace CreateAColony.Colonies;

/// <summary>
/// Map markers the colony's settlers put down: what the Explorer and Gatherer find, and where settlers fell.
/// The colony keeps them, and every member sees them on the "Colony markers" layer of their world map
/// (Map.ColonyMarkerMapLayer). Each ore, fruit, vegetable, grain, animal, trader and story location has an icon
/// of its own (Map.MarkerSubjects), other mods' too; the kinds below give the group, colour and spare icon.
/// </summary>
public partial class ColonyManager
{
    /// <summary>
    /// A kind of marker: the group that switches it on and off on the hut window, the icon to use, the game's own
    /// icon if that one is missing, and the waypoint colour. The "-any" kinds are for things with their own icon
    /// (Map.MarkerSubjects); the rest are kept for markers put down before 0.80.0, and for things without one.
    /// </summary>
    public readonly record struct MarkerKind(string Code, string Group, string Icon, string Fallback, string Color);

    /// <summary>The marker groups each hut type puts down, in the order its Markers tab lists them.</summary>
    public static readonly Dictionary<string, string[]> MarkerGroups = new()
    {
        [BuildingTypes.Explorer] = new[] { "ore", "clay", "soil", "traders", "animals", "danger", "landmarks" },
        [BuildingTypes.Gatherer] = new[] { "berries", "fruit", "mushrooms", "crops", "reeds", "resin", "bees" },
    };

    const string White = "#FFFFFF";
    const string Danger = "#ED272A";
    const string Game = "#8BC34A";

    static readonly Dictionary<string, MarkerKind> Kinds = new[]
    {
        // Ore, by the More Map Icons element icon.
        new MarkerKind("ore-cuprum", "ore", "vc-copper", "pick", White),
        new MarkerKind("ore-tin", "ore", "vc-tin", "pick", White),
        new MarkerKind("ore-plumbum", "ore", "vc-lead", "pick", White),
        new MarkerKind("ore-zink", "ore", "vc-zinc", "pick", White),
        new MarkerKind("ore-bismuth", "ore", "vc-bismuth", "pick", White),
        new MarkerKind("ore-ferrum", "ore", "vc-iron", "pick", White),
        new MarkerKind("ore-meteoriciron", "ore", "vc-meteoriciron", "pick", White),
        new MarkerKind("ore-aurum", "ore", "vc-gold", "pick", White),
        new MarkerKind("ore-argentum", "ore", "vc-silver", "pick", White),
        new MarkerKind("ore-chromium", "ore", "vc-chromium", "pick", White),
        new MarkerKind("ore-titanium", "ore", "vc-titanium", "pick", White),
        new MarkerKind("ore-nickel", "ore", "vc-nickel", "pick", White),
        new MarkerKind("ore-uranium", "ore", "vc-uranium", "pick", White),
        new MarkerKind("ore-manganese", "ore", "vc-manganese", "pick", White),
        new MarkerKind("ore-aluminum", "ore", "vc-aluminum", "pick", White),
        new MarkerKind("ore-sulfur", "ore", "vc-sulfur", "pick", White),
        new MarkerKind("ore-boron", "ore", "vc-borax", "pick", White),
        new MarkerKind("ore-fluorite", "ore", "vc-fluorite", "pick", White),
        new MarkerKind("ore-phosphorus", "ore", "vc-phosphorus", "pick", White),
        new MarkerKind("ore-potassium", "ore", "vc-potassium", "pick", White),
        new MarkerKind("ore-blackcoal", "ore", "vc-blackcoal", "rocks", "#080504"),
        new MarkerKind("ore-browncoal", "ore", "vc-browncoal", "rocks", "#5D3D21"),
        new MarkerKind("ore-quartz", "ore", "vc-quartz", "rocks", White),
        new MarkerKind("ore-olivine", "ore", "vc-olivine", "rocks", "#47B749"),
        new MarkerKind("ore-saltpeter", "ore", "vc-saltpeter", "rocks", White),
        new MarkerKind("ore-salt", "ore", "vc-salt", "rocks", White),
        // Clay and good soil.
        new MarkerKind("clay-blue", "clay", "vc-blueclay", "vessel", "#28417A"),
        new MarkerKind("clay-red", "clay", "vc-redclay", "vessel", "#ED272A"),
        new MarkerKind("clay-fire", "clay", "vc-fireclay", "vessel", "#706D64"),
        new MarkerKind("soil-peat", "soil", "vc-peat", "rocks", "#885530"),
        new MarkerKind("soil-terrapreta", "soil", "vc-terrapreta", "rocks", "#080504"),
        // Traders, by the game's trader type.
        new MarkerKind("trader-agriculture", "traders", "vc-agriculturetrader", "trader", White),
        new MarkerKind("trader-artisan", "traders", "vc-artisantrader", "trader", White),
        new MarkerKind("trader-buildmaterials", "traders", "vc-buildingtrader", "trader", White),
        new MarkerKind("trader-clothing", "traders", "vc-clothingtrader", "trader", White),
        new MarkerKind("trader-commodities", "traders", "vc-commoditiestrader", "trader", White),
        new MarkerKind("trader-furniture", "traders", "vc-furnituretrader", "trader", White),
        new MarkerKind("trader-luxuries", "traders", "vc-luxuriestrader", "trader", White),
        new MarkerKind("trader-survivalgoods", "traders", "vc-survivalgoodstrader", "trader", White),
        new MarkerKind("trader-treasurehunter", "traders", "vc-treasuretrader", "trader", White),
        new MarkerKind("trader-other", "traders", "vc-trader", "trader", White),
        // Animals to hunt, and things to keep away from.
        new MarkerKind("animal-chicken", "animals", "vc-chicken", "circle", Game),
        new MarkerKind("animal-boar", "animals", "vc-boar", "circle", Game),
        new MarkerKind("animal-goat", "animals", "vc-goat", "circle", Game),
        new MarkerKind("animal-horse", "animals", "vc-horse", "circle", Game),
        new MarkerKind("animal-fish", "animals", "vc-fish", "circle", Game),
        new MarkerKind("animal-deer", "animals", "vc-deer", "circle", Game),
        new MarkerKind("danger-wolf", "danger", "vc-wolf", "circle", Danger),
        new MarkerKind("danger-bear", "danger", "vc-bear", "circle", Danger),
        new MarkerKind("danger-spider", "danger", "vc-spider", "circle", Danger),
        new MarkerKind("danger-deadly", "danger", "vc-deadly", "circle", Danger),
        // Landmarks.
        new MarkerKind("landmark-translocator", "landmarks", "vc-translocator", "spiral", "#4FC3F7"),
        new MarkerKind("landmark-ruins", "landmarks", "vc-ruins", "ruins", White),
        new MarkerKind("landmark-mountain", "landmarks", "vc-mountains", "star1", White),
        // What the Gatherer finds.
        new MarkerKind("berries-blueberry", "berries", "vc-blueberries", "circle", "#5C6BC0"),
        new MarkerKind("berries-cranberry", "berries", "vc-cranberries", "circle", "#C62828"),
        new MarkerKind("berries-other", "berries", "vc-berries", "circle", "#AB47BC"),
        new MarkerKind("fruit-apple", "fruit", "vc-apple", "circle", "#E53935"),
        new MarkerKind("fruit-cherry", "fruit", "vc-cherry", "circle", "#AD1457"),
        new MarkerKind("fruit-pear", "fruit", "vc-pear", "circle", "#C0CA33"),
        new MarkerKind("fruit-other", "fruit", "vc-fruittree", "circle", "#FB8C00"),
        new MarkerKind("mushrooms-edible", "mushrooms", "vc-mushrooms", "circle", "#A1887F"),
        new MarkerKind("mushrooms-poisonous", "mushrooms", "vc-poisonousmushrooms", "circle", Danger),
        new MarkerKind("crops-carrot", "crops", "vc-carrot", "circle", "#FB8C00"),
        new MarkerKind("crops-parsnip", "crops", "vc-parsnip", "circle", White),
        new MarkerKind("crops-onion", "crops", "vc-onion", "circle", "#D7CCC8"),
        new MarkerKind("crops-turnip", "crops", "vc-turnip", "circle", "#CE93D8"),
        new MarkerKind("crops-cabbage", "crops", "vc-cabbage", "circle", "#9CCC65"),
        new MarkerKind("crops-pumpkin", "crops", "vc-pumpkin", "circle", "#FF9800"),
        new MarkerKind("crops-grain", "crops", "vc-wheat", "circle", "#FFD54F"),
        new MarkerKind("crops-other", "crops", "vc-sprout", "circle", Game),
        new MarkerKind("reeds-cattail", "reeds", "vc-cattail", "circle", "#8D6E63"),
        new MarkerKind("resin-tree", "resin", "vc-resin", "circle", "#FFB300"),
        new MarkerKind("bees-hive", "bees", "vc-bees", "bee", "#FFD54F"),
        // Things with an icon of their own (Map.MarkerSubjects).
        new MarkerKind("ore-any", "ore", "vc-ingots", "pick", White),
        new MarkerKind("berries-any", "berries", "vc-berries", "circle", "#AB47BC"),
        new MarkerKind("fruit-any", "fruit", "vc-fruittree", "circle", "#FB8C00"),
        new MarkerKind("crops-any", "crops", "vc-sprout", "circle", "#94C948"),
        new MarkerKind("animal-any", "animals", "vc-deer", "circle", Game),
        new MarkerKind("danger-any", "danger", "vc-deadly", "circle", Danger),
        new MarkerKind("trader-any", "traders", "vc-trader", "trader", White),
        new MarkerKind("story-any", "landmarks", "vc-ruins", "ruins", "#FDBB3A"),
        // A settler fell here.
        new MarkerKind("grave", "graves", "vc-rip", "gravestone", "#C8C8C8"),
    }.ToDictionary(k => k.Code);

    /// <summary>Ores by a piece of their block code, most specific first ("quartz_nativegold" is gold, not quartz).</summary>
    static readonly (string Part, string Kind)[] OreParts =
    {
        ("nativegold", "ore-aurum"), ("nativesilver", "ore-argentum"), ("nativecopper", "ore-cuprum"), ("malachite", "ore-cuprum"),
        ("cassiterite", "ore-tin"), ("galena", "ore-plumbum"), ("sphalerite", "ore-zink"), ("bismuthinite", "ore-bismuth"),
        ("meteoriciron", "ore-meteoriciron"), ("limonite", "ore-ferrum"), ("hematite", "ore-ferrum"), ("magnetite", "ore-ferrum"),
        ("chromite", "ore-chromium"), ("ilmenite", "ore-titanium"), ("pentlandite", "ore-nickel"), ("uranium", "ore-uranium"),
        ("rhodochrosite", "ore-manganese"), ("bauxite", "ore-aluminum"), ("sulfur", "ore-sulfur"), ("kernite", "ore-boron"),
        ("borax", "ore-boron"), ("fluorite", "ore-fluorite"), ("phosphorite", "ore-phosphorus"), ("sylvite", "ore-potassium"),
        ("anthracite", "ore-blackcoal"), ("bituminouscoal", "ore-blackcoal"), ("lignite", "ore-browncoal"),
        ("olivine", "ore-olivine"), ("quartz", "ore-quartz"), ("saltpeter", "ore-saltpeter"), ("halite", "ore-salt"), ("rocksalt", "ore-salt"),
    };

    /// <summary>The marker kind for an ore ("nativecopper" is ore-cuprum), or ore-any.</summary>
    public static string OreMarkerKind(string ore)
    {
        foreach (var (orePart, kind) in OreParts)
        {
            if (ore.Contains(orePart)) return kind;
        }
        return "ore-any";
    }

    /// <summary>The element icon for an ore ("nativecopper" is vc-copper), or null.</summary>
    public static string? OreElementIcon(string ore)
    {
        foreach (var (orePart, kind) in OreParts)
        {
            if (ore.Contains(orePart)) return Kinds[kind].Icon;
        }
        return null;
    }

    public static string MarkerGroup(string kind) => Kinds.TryGetValue(kind, out var k) ? k.Group : "";

    /// <summary>The icon, the game's icon for players without More Map Icons, and the colour of a kind of marker.</summary>
    public static MarkerKind? KindOf(string kind) => Kinds.TryGetValue(kind, out var k) ? k : null;

    /// <summary>
    /// Puts a marker down for the colony, unless one of the same kind is already close by, the hut is set not to
    /// mark that group, or the colony has as many markers as the config allows. True when a marker was added.
    /// </summary>
    public bool AddMarker(Colony colony, Building? hut, string kind, Vec3d pos, string? title = null, int citizenId = 0, Map.MarkerSubject? subject = null)
    {
        if (!Kinds.TryGetValue(kind, out var def)) return false;
        if (hut != null && hut.MarkersOff.Contains(def.Group)) return false;
        double spacing = Config.MarkerSpacing.GetValueOrDefault(def.Group, 32);
        string icon = subject?.Icon ?? "";
        if (colony.Markers.Any(m => m.Kind == kind && m.Icon == icon && (m.X - pos.X) * (m.X - pos.X) + (m.Z - pos.Z) * (m.Z - pos.Z) < spacing * spacing)) return false;
        if (hut != null && colony.Markers.Count(m => m.BuildingId != 0) >= Config.MaxColonyMarkers) return false;

        colony.Markers.Add(new ColonyMarker
        {
            MarkerId = colony.NextMarkerId++,
            Kind = kind,
            X = pos.X, Y = pos.Y, Z = pos.Z,
            Title = title ?? subject?.Name ?? Lang.Get("createacolony:marker-" + kind),
            Icon = icon,
            BuildingId = hut?.BuildingId ?? 0,
            TotalDays = sapi.World.Calendar.TotalDays,
            CitizenId = citizenId
        });
        if (hut != null) AddStat(hut, "markers");
        MarkDirty(colony);
        ShareMarkers(colony);
        return true;
    }

    /// <summary>Marks where a settler fell, with a grave; only their latest spot is kept.</summary>
    public void MarkFallen(Colony colony, CitizenRecord citizen, Vec3d pos, bool died)
    {
        if (!Config.MarkSettlerDeaths) return;
        colony.Markers.RemoveAll(m => m.Kind == "grave" && m.CitizenId == citizen.CitizenId);
        string title = Lang.Get(died ? "createacolony:marker-grave-died" : "createacolony:marker-grave-downed", citizen.Name);
        AddMarker(colony, null, "grave", pos, title, citizen.CitizenId);
    }

    /// <summary>Takes away every marker a hut's worker put down. Returns how many there were.</summary>
    public int RemoveHutMarkers(Colony colony, Building hut)
    {
        int removed = colony.Markers.RemoveAll(m => m.BuildingId == hut.BuildingId);
        if (removed == 0) return 0;
        MarkDirty(colony);
        ShareMarkers(colony);
        return removed;
    }

    /// <summary>Sends the colony's markers again to every online member's "Colony markers" map layer.</summary>
    void ShareMarkers(Colony colony)
    {
        try
        {
            var layer = sapi.ModLoader.GetModSystem<WorldMapManager>()?.MapLayers.OfType<Map.ColonyMarkerMapLayer>().FirstOrDefault();
            if (layer == null) return;
            foreach (var member in colony.Members)
            {
                if (sapi.World.PlayerByUid(member.PlayerUid) is IServerPlayer { ConnectionState: EnumClientState.Playing } player) layer.Resend(player);
            }
        }
        catch (Exception e)
        {
            sapi.Logger.Warning("[createacolony] Could not show the colony markers on the map: {0}", e.InnerException?.Message ?? e.Message);
        }
    }

    // ---- What a settler sees around it ----------------------------------------------------

    /// <summary>
    /// Looks over the land around a spot, the way an Explorer or Gatherer does when it stops there, and puts a
    /// marker on what its hut marks. Returns how many markers it put down.
    /// </summary>
    public int LookAround(Colony colony, Building hut, BlockPos center, int radius)
    {
        if (!MarkerGroups.TryGetValue(hut.Type, out var groups)) return 0;
        var wanted = new HashSet<string>(groups.Where(g => !hut.MarkersOff.Contains(g)));
        if (wanted.Count == 0) return 0;
        int placed = 0;
        var ba = sapi.World.BlockAccessor;
        var pos = new BlockPos(0);
        int seaLevel = sapi.World.SeaLevel;

        for (int dx = -radius; dx <= radius; dx++)
        for (int dz = -radius; dz <= radius; dz++)
        {
            if (dx * dx + dz * dz > radius * radius) continue;
            pos.Set(center.X + dx, center.Y, center.Z + dz);
            if (ba.GetChunkAtBlockPos(pos) == null) continue;
            int top = ba.GetTerrainMapheightAt(pos);
            for (int y = top - 1; y <= top + 2; y++)
            {
                pos.Y = y;
                var block = ba.GetBlock(pos);
                if (block.Id == 0) continue;
                if (BlockMarker(block, pos) is not var (kind, subject) || !wanted.Contains(MarkerGroup(kind))) continue;
                if (AddMarker(colony, hut, kind, pos.ToVec3d().Add(0.5, 0.5, 0.5), subject: subject)) placed++;
            }
        }

        // A high peak is a landmark: the highest ground the explorer saw here, well above sea level.
        if (wanted.Contains("landmarks") && ba.GetChunkAtBlockPos(center) != null)
        {
            int top = ba.GetTerrainMapheightAt(center);
            if (top - seaLevel >= Config.ExplorerMountainHeight
                && AddMarker(colony, hut, "landmark-mountain", new Vec3d(center.X + 0.5, top + 1, center.Z + 0.5))) placed++;
        }

        // Story locations the explorer comes near: a marker at their middle.
        if (wanted.Contains("landmarks") && sapi.ModLoader.GetModSystem<GenStoryStructures>()?.Structures is { } story)
        {
            foreach (var (code, location) in story)
            {
                if (location?.CenterPos == null) continue;
                var area = location.Location;
                double distance = area != null
                    ? area.ShortestDistanceFrom(center.X, area.Y1, center.Z)
                    : Math.Sqrt((double)(location.CenterPos.X - center.X) * (location.CenterPos.X - center.X) + (double)(location.CenterPos.Z - center.Z) * (location.CenterPos.Z - center.Z));
                if (distance > radius + 32 || Map.MarkerSubjects.Get(sapi, Map.MarkerSubjects.Story, code) is not { } subject) continue;
                if (AddMarker(colony, hut, "story-any", location.CenterPos.ToVec3d().Add(0.5, 0, 0.5), subject: subject)) placed++;
            }
        }

        foreach (var entity in sapi.World.GetEntitiesAround(center.ToVec3d(), radius + 8, 24, e => e.Alive && e is not EntityPlayer))
        {
            if (CreatureMarker(entity) is not var (kind, subject) || !wanted.Contains(MarkerGroup(kind))) continue;
            if (AddMarker(colony, hut, kind, entity.Pos.XYZ, subject: subject)) placed++;
        }
        return placed;
    }

    /// <summary>The marker for a block, and what it marks if that has its own icon; null when it is not worth one.</summary>
    (string Kind, Map.MarkerSubject? Subject)? BlockMarker(Block block, BlockPos pos)
    {
        if (Map.MarkerSubjects.OreKey(block) is { } ore)
        {
            if (Map.MarkerSubjects.Get(sapi, Map.MarkerSubjects.Ore, ore) is { } subject) return ("ore-any", subject);
            foreach (var (orePart, kind) in OreParts)
            {
                if (ore.Contains(orePart)) return (kind, null);
            }
            return null;
        }
        string path = block.Code.Path;
        var ba = sapi.World.BlockAccessor;
        if (block is BlockCrop)
        {
            if (!(ba.GetBlockEntity(pos.DownCopy()) is BlockEntityFarmland) && Produce(block, TypeOf(block)) is { } crop) return ("crops-any", crop);
        }
        else if (path.Contains("berrybush") || path.StartsWith("fruitingbush"))
        {
            if (Produce(block, TypeOf(block)) is { } berries) return ("berries-any", berries);
        }
        else if (ba.GetBlockEntity(pos) is BlockEntityFruitTreePart { PartType: not EnumTreePartType.Stem } part
            && Produce(block, part.TreeType) is { } fruit) return ("fruit-any", fruit);
        var plain = PlainBlockMarker(block, pos);
        return plain == null ? null : (plain, null);
    }

    /// <summary>The produce of a wild crop, berry bush or fruit tree: what it is called by ("carrot"), or its drops.</summary>
    Map.MarkerSubject? Produce(Block block, string? type)
    {
        foreach (string category in new[] { Map.MarkerSubjects.Fruit, Map.MarkerSubjects.Vegetable, Map.MarkerSubjects.Grain })
        {
            if (Map.MarkerSubjects.Get(sapi, category, type) is { } subject) return subject;
        }
        try
        {
            foreach (var drop in block.Drops ?? Array.Empty<BlockDropItemStack>())
            {
                drop.Resolve(sapi.World, "produce marker", block.Code);
                if (Map.MarkerSubjects.FoodKey(drop.ResolvedItemstack?.Collectible) is { } food
                    && Map.MarkerSubjects.Get(sapi, food.Category, food.Key) is { } subject) return subject;
            }
        }
        catch (Exception)
        {
            // Unusual drops: the plant gets its group's icon.
        }
        return null;
    }

    static string? TypeOf(Block block) =>
        block.Variant.TryGetValue("type", out var type) && !string.IsNullOrEmpty(type) ? type : block.Code.Path.Split('-').ElementAtOrDefault(1);

    /// <summary>The marker for a block that is not ore, or null when it is not worth one.</summary>
    string? PlainBlockMarker(Block block, BlockPos pos)
    {
        string path = block.Code.Path;
        if (path.StartsWith("rawclay-")) return path.Contains("blue") ? "clay-blue" : path.Contains("fire") ? "clay-fire" : path.Contains("red") ? "clay-red" : null;
        if (path.StartsWith("peat-")) return "soil-peat";
        if (path.Contains("terrapreta")) return "soil-terrapreta";
        if (path.StartsWith("statictranslocator")) return "landmark-translocator";
        if (path.StartsWith("wildbeehive")) return "bees-hive";
        if (path.StartsWith("log-resin-")) return "resin-tree";
        if (path.StartsWith("tallplant-coopersreed") || path.StartsWith("tallplant-papyrus") || path.StartsWith("tallplant-tule")) return "reeds-cattail";
        if (path.StartsWith("mushroom-")) return IsPoisonousMushroom(block) ? "mushrooms-poisonous" : "mushrooms-edible";
        if (block is BlockCrop && !(sapi.World.BlockAccessor.GetBlockEntity(pos.DownCopy()) is BlockEntityFarmland)) return CropMarker(path);
        if (path.Contains("berrybush") || path.StartsWith("fruitingbush") || block.Attributes?["fruitingBush"].Exists == true || sapi.World.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorFruitingBush>() != null)
        {
            return path.Contains("blueberry") ? "berries-blueberry" : path.Contains("cranberry") ? "berries-cranberry" : "berries-other";
        }
        if (sapi.World.BlockAccessor.GetBlockEntity(pos) is BlockEntityFruitTreePart { PartType: not EnumTreePartType.Stem } part)
        {
            string type = part.TreeType ?? "";
            return type.Contains("apple") ? "fruit-apple" : type.Contains("cherry") ? "fruit-cherry" : type.Contains("pear") ? "fruit-pear" : "fruit-other";
        }
        return null;
    }

    static string CropMarker(string path)
    {
        string crop = path.Split('-').ElementAtOrDefault(1) ?? "";
        return crop switch
        {
            "carrot" => "crops-carrot",
            "parsnip" => "crops-parsnip",
            "onion" => "crops-onion",
            "turnip" => "crops-turnip",
            "cabbage" => "crops-cabbage",
            "pumpkin" => "crops-pumpkin",
            "spelt" or "rye" or "rice" or "amaranth" or "flax" or "sunflower" or "cassava" => "crops-grain",
            _ => "crops-other"
        };
    }

    /// <summary>A mushroom that hurts whoever eats it, going by what eating its drop does.</summary>
    bool IsPoisonousMushroom(Block block)
    {
        if (poisonous.TryGetValue(block.Id, out bool known)) return known;
        return poisonous[block.Id] = EatingHurts(block);
    }

    /// <summary>Mushroom blocks found to be poisonous or not, by block id.</summary>
    readonly Dictionary<int, bool> poisonous = new();

    bool EatingHurts(Block block)
    {
        try
        {
            foreach (var drop in block.Drops ?? Array.Empty<BlockDropItemStack>())
            {
                drop.Resolve(sapi.World, "mushroom marker", block.Code);
                if (drop.ResolvedItemstack?.Collectible.GetNutritionProperties(sapi.World, drop.ResolvedItemstack, null) is { Health: < 0 }) return true;
            }
        }
        catch (Exception)
        {
            // Unusual drops: count it as edible, which only changes the marker.
        }
        return false;
    }

    /// <summary>The marker for a creature and what it is, or null when it is not worth one.</summary>
    (string Kind, Map.MarkerSubject? Subject)? CreatureMarker(Entity entity)
    {
        if (Map.MarkerSubjects.CreatureKey(sapi, entity.Properties) is { } creature
            && Map.MarkerSubjects.Get(sapi, creature.Category, creature.Key) is { } subject)
        {
            if (creature.Category == Map.MarkerSubjects.Trader) return ("trader-any", subject);
            return (IsDangerous(entity.Properties) ? "danger-any" : "animal-any", subject);
        }
        var plain = PlainCreatureMarker(entity);
        return plain == null ? null : (plain, null);
    }

    /// <summary>Creature types found to go after players or not, by code.</summary>
    readonly Dictionary<AssetLocation, bool> dangerous = new();

    /// <summary>A creature that seeks out players to attack them (wolves, bears, drifters), going by its AI.</summary>
    bool IsDangerous(EntityProperties props)
    {
        if (dangerous.TryGetValue(props.Code, out bool known)) return known;
        bool result = false;
        foreach (var behavior in props.Server?.BehaviorsAsJsonObj ?? Array.Empty<JsonObject>())
        {
            if (behavior["code"].AsString() != "taskai") continue;
            foreach (var task in behavior["aitasks"].AsArray() ?? Array.Empty<JsonObject>())
            {
                string code = task["code"].AsString() ?? "";
                if (code != "seekentity" && code != "meleeattack" && code != "stayclosetotarget") continue;
                if (task["entityCodes"].AsArray<string>() is { } codes && codes.Any(c => c.StartsWith("player"))
                    && (code == "seekentity" || task["retaliateAttacks"].AsBool(true) == false)) result = true;
            }
        }
        string path = props.Code.Path;
        result |= path.StartsWith("drifter") || path.StartsWith("locust") || path.StartsWith("bell") || path.StartsWith("shiver")
            || path.StartsWith("bowtorn") || path.StartsWith("eidolon") || path.StartsWith("erel");
        return dangerous[props.Code] = result;
    }

    /// <summary>The marker for a creature without an icon of its own, or null when it is not worth one.</summary>
    static string? PlainCreatureMarker(Entity entity)
    {
        string path = entity.Code.Path;
        if (entity is EntityTrader || path.StartsWith("humanoid-trader-"))
        {
            string type = path.StartsWith("humanoid-trader-") ? path["humanoid-trader-".Length..] : "";
            return Kinds.ContainsKey("trader-" + type) ? "trader-" + type : "trader-other";
        }
        if (path.StartsWith("wolf") || path.StartsWith("hyena")) return "danger-wolf";
        if (path.StartsWith("bear")) return "danger-bear";
        if (path.StartsWith("spider")) return "danger-spider";
        if (path.StartsWith("drifter") || path.StartsWith("locust") || path.StartsWith("bell") || path.StartsWith("shiver")
            || path.StartsWith("bowtorn") || path.StartsWith("eidolon") || path.StartsWith("erel")) return "danger-deadly";
        if (path.StartsWith("chicken")) return "animal-chicken";
        if (path.StartsWith("pig") || path.StartsWith("boar")) return "animal-boar";
        if (path.StartsWith("goat") || path.StartsWith("sheep")) return "animal-goat";
        if (path.StartsWith("horse")) return "animal-horse";
        if (path.StartsWith("salmon") || path.StartsWith("fish")) return "animal-fish";
        if (path.StartsWith("deer") || path.StartsWith("moose") || path.StartsWith("elk")) return "animal-deer";
        return null;
    }

    /// <summary>How many markers a hut's worker has put down, for its window.</summary>
    public int MarkerCount(Colony colony, Building hut) => colony.Markers.Count(m => m.BuildingId == hut.BuildingId);
}
