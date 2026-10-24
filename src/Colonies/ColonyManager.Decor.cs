using System;
using System.Collections.Generic;
using System.Linq;
using CreateAColony.Blocks;
using CreateAColony.Network;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using ItemMatch = System.Func<Vintagestory.API.Common.ItemStack, bool>;

namespace CreateAColony.Colonies;

/// <summary>What a settler can put up at home, each with the block it goes on.</summary>
public enum DecorKind { Painting, Trophy, Weapons, Books, Bottles, Banner }

/// <summary>
/// One decoration a settler is going to put up: where, and where it gets the things for it. Fill means the
/// rack or shelf is up already and only gets more on it.
/// </summary>
public sealed class DecorPlan
{
    public DecorKind Kind;
    public BlockPos Spot = new(0);
    public BlockFacing Wall = BlockFacing.NORTH;
    public bool Fill;
    /// <summary>The chest with the painting, rack, shelf or pole, or null when it is up already.</summary>
    public BlockPos? MountFrom;
    /// <summary>The chest with what goes on it (antlers, weapons, books, bottles, a banner); null for a painting.</summary>
    public BlockPos? ContentFrom;
    /// <summary>How many things to put on it.</summary>
    public int Want;
}

/// <summary>
/// Settlers decorate their homes with things the household owns, the things in the chests of their home:
/// they hang paintings, put antlers and other trophies on an antler mount, weapons on a tool rack, books in
/// a bookshelf, bottles in A Culinary Artillery's bottle rack and banners on a Heraldry wall pole. The rack,
/// shelf, mount or pole comes from a chest at home, else from the warehouse. A Hunter may also take trophies
/// from its hut and the warehouse. Everything goes inside the home, under its roof: wall pieces against a
/// wall at about eye height, shelves and racks on the floor with their back to a wall, never next to a door
/// or in the way. A rack or shelf that is up gets filled before another one goes up, and a home gets at most
/// DecorPerKind of each. Mods that are not installed simply have nothing to put up.
/// </summary>
public partial class ColonyManager
{
    const string AcaDomain = "aculinaryartillery";
    static readonly HashSet<EnumTool> RackWeapons = new()
    {
        EnumTool.Sword, EnumTool.Spear, EnumTool.Bow, EnumTool.Sling, EnumTool.Crossbow, EnumTool.Javelin, EnumTool.Pike,
        EnumTool.Club, EnumTool.Mace, EnumTool.Warhammer, EnumTool.Poleaxe, EnumTool.Halberd, EnumTool.Polearm, EnumTool.Firearm
    };
    static readonly DecorKind[] DecorKinds = (DecorKind[])Enum.GetValues(typeof(DecorKind));

    // ---- What goes where ------------------------------------------------------------------

    /// <summary>The item of the block a decoration goes on: a painting itself, or an empty rack, shelf, mount or pole.</summary>
    public static bool IsDecorMount(DecorKind kind, ItemStack? s)
    {
        if (s?.Block is not { } block) return false;
        string path = block.Code.Path;
        return kind switch
        {
            DecorKind.Painting => block is BlockPainting p && p.Art != BlockPainting.Blank,
            DecorKind.Trophy => block is BlockAntlerMount,
            DecorKind.Weapons => block is BlockToolRack,
            DecorKind.Books => block is BlockBookshelf,
            DecorKind.Bottles => block.Code.Domain == AcaDomain && path.StartsWith("bottlerack-"),
            DecorKind.Banner => block.Code.Domain == HeraldryDomain && path.StartsWith("pole-banner-wall"),
            _ => false
        };
    }

    /// <summary>What goes on a decoration's rack, shelf, mount or pole. Nothing goes on a painting.</summary>
    public static bool IsDecorContent(DecorKind kind, ItemStack? s)
    {
        if (s?.Collectible is not { } c) return false;
        return kind switch
        {
            DecorKind.Trophy => c.Attributes?["antlerMountable"].AsBool() == true,
            DecorKind.Weapons => c.Tool is { } tool && RackWeapons.Contains(tool),
            DecorKind.Books => c.Attributes?["bookshelveable"].AsBool() == true,
            DecorKind.Bottles => IsRackableBottle(s),
            DecorKind.Banner => c.Code.Domain == HeraldryDomain && c.Code.Path.StartsWith("banner"),
            _ => false
        };
    }

    /// <summary>A bottle the bottle rack takes: one it says it takes, but not an open one with more than a sip in it, as A Culinary Artillery refuses those.</summary>
    static bool IsRackableBottle(ItemStack s)
    {
        var c = s.Collectible;
        if (c.Attributes?["bottlerackable"].AsBool() != true) return false;
        bool open = c.GetType().GetProperty("IsTopOpened")?.GetValue(c) is true;
        return !(open && c is BlockLiquidContainerBase liquid && liquid.GetCurrentLitres(s) > 0.2f);
    }

    /// <summary>The kind of decoration each block is, by block id, worked out once.</summary>
    readonly Dictionary<int, DecorKind?> decorKindCache = new();

    /// <summary>The kind of decoration a placed block is, or null.</summary>
    DecorKind? DecorKindOf(Block block)
    {
        if (decorKindCache.TryGetValue(block.Id, out var known)) return known;
        return decorKindCache[block.Id] = WorkOutDecorKind(block);
    }

    static DecorKind? WorkOutDecorKind(Block block)
    {
        var stack = new ItemStack(block);
        foreach (var kind in DecorKinds) if (IsDecorMount(kind, stack)) return kind;
        // An empty canvas hung by a player is still a painting, for the room it takes.
        return block is BlockPainting ? DecorKind.Painting : null;
    }

    /// <summary>Hangs on a wall (true) or stands on the floor with its back to one (false).</summary>
    static bool OnWall(DecorKind kind) => kind is not (DecorKind.Books or DecorKind.Bottles);

    /// <summary>How many things go on a new one, at most; what does not fit goes back into the chest.</summary>
    static int DecorCapacity(DecorKind kind) => kind switch
    {
        DecorKind.Trophy or DecorKind.Banner => 1,
        DecorKind.Weapons => 4,
        DecorKind.Books => 14,
        DecorKind.Bottles => 16,
        _ => 0
    };

    /// <summary>The inventory a rack, shelf, mount or pole shows its things in. The tool rack is not a container, so it is asked for its own.</summary>
    static InventoryBase? DecorInventory(BlockEntity? be) => be switch
    {
        BlockEntityToolrack rack => rack.inventory,
        BlockEntityContainer container => container.Inventory,
        _ => null
    };

    /// <summary>The free places on a rack, shelf, mount or pole (a bookshelf has fewer places than slots).</summary>
    static int FreeDecorSlots(BlockEntity? be) => DecorInventory(be)?.Count(slot => slot.Empty && slot.MaxSlotStackSize > 0) ?? 0;

    // ---- Looking at a home ----------------------------------------------------------------

    /// <summary>What a home has up already: its decorations by kind, and the air the bigger paintings hang over.</summary>
    sealed class HomeDecor
    {
        public readonly Dictionary<DecorKind, List<BlockPos>> Mounts = new();
        public readonly HashSet<BlockPos> Covered = new();
        public BlockPos Min = new(0), Max = new(0);
    }

    HomeDecor LookAtHome(Building home)
    {
        var (min, max) = ZoneOf(home);
        var seen = new HomeDecor { Min = min, Max = max };
        var ba = sapi.World.BlockAccessor;
        var pos = new BlockPos(0);
        for (int x = min.X; x <= max.X; x++)
        for (int y = min.Y; y <= max.Y; y++)
        for (int z = min.Z; z <= max.Z; z++)
        {
            pos.Set(x, y, z);
            var block = ba.GetBlock(pos);
            if (block.Id == 0 || DecorKindOf(block) is not { } kind) continue;
            if (!seen.Mounts.TryGetValue(kind, out var list)) seen.Mounts[kind] = list = new List<BlockPos>();
            list.Add(pos.Copy());
            if (block is BlockPainting painting && BlockFacing.FromCode(painting.Variant["side"]) is { } wall)
            {
                foreach (var cell in PaintingCells(pos, wall, painting.Size)) seen.Covered.Add(cell);
            }
        }
        return seen;
    }

    /// <summary>How many blocks wide and tall a painting of this size is.</summary>
    static (int W, int H) PaintingSize(string size) => size switch
    {
        "wide" => (2, 1),
        "tall" => (1, 2),
        "large" => (2, 2),
        _ => (1, 1)
    };

    /// <summary>
    /// The blocks a painting covers: its own, and up and to the right as you look at the wall (paintings.py
    /// draws the north one that way, and the game turns it for the other walls).
    /// </summary>
    static IEnumerable<BlockPos> PaintingCells(BlockPos pos, BlockFacing wall, string size)
    {
        var (w, h) = PaintingSize(size);
        var right = wall.GetCW();
        for (int i = 0; i < w; i++)
        for (int j = 0; j < h; j++)
        {
            yield return pos.AddCopy(right.Normali.X * i, j, right.Normali.Z * i);
        }
    }

    /// <summary>The block turned to hang on or stand against this wall: its "side" variant, which for all of these is the side the wall is on.</summary>
    Block? TurnedFor(Block block, BlockFacing wall)
    {
        if (block.Variant.ContainsKey("side")) return sapi.World.GetBlock(block.CodeWithVariant("side", wall.Code));
        string path = block.Code.Path;
        foreach (var face in BlockFacing.HORIZONTALS)
        {
            if (!path.EndsWith("-" + face.Code)) continue;
            return sapi.World.GetBlock(new AssetLocation(block.Code.Domain, path[..^face.Code.Length] + wall.Code));
        }
        return block;
    }

    /// <summary>How many air blocks are below this one, down to the floor; -1 if the floor is not solid or too far.</summary>
    int HeightAboveFloor(BlockPos pos)
    {
        var ba = sapi.World.BlockAccessor;
        var below = pos.Copy();
        for (int h = 0; h <= 4; h++)
        {
            below.Y--;
            var block = ba.GetBlock(below);
            if (block.Id == 0) continue;
            return block.SideSolid[BlockFacing.UP.Index] ? h : -1;
        }
        return -1;
    }

    static bool IsDoorLike(Block block)
    {
        string path = block.Code.Path;
        return block is BlockBaseDoor || path.Contains("door") || path.Contains("gate");
    }

    /// <summary>A door within two blocks, so nothing is put up where people walk in and out.</summary>
    bool NearDoor(BlockPos pos)
    {
        var ba = sapi.World.BlockAccessor;
        var at = new BlockPos(0);
        for (int dx = -2; dx <= 2; dx++)
        for (int dz = -2; dz <= 2; dz++)
        for (int dy = -1; dy <= 1; dy++)
        {
            at.Set(pos.X + dx, pos.Y + dy, pos.Z + dz);
            if (IsDoorLike(ba.GetBlock(at))) return true;
        }
        return false;
    }

    /// <summary>
    /// A block a decoration can take: air, inside the home's walls (not the yard round it), under a roof, and
    /// not already covered by a painting.
    /// </summary>
    bool FreeIndoors(HomeDecor seen, BlockPos pos)
    {
        if (pos.X <= seen.Min.X || pos.X >= seen.Max.X || pos.Z <= seen.Min.Z || pos.Z >= seen.Max.Z || pos.Y < seen.Min.Y || pos.Y > seen.Max.Y) return false;
        if (seen.Covered.Contains(pos)) return false;
        var ba = sapi.World.BlockAccessor;
        return ba.GetBlock(pos).Id == 0 && ba.GetRainMapHeightAt(pos.X, pos.Z) > pos.Y;
    }

    /// <summary>The wall on this side can hold the block (the game's own test for hanging things).</summary>
    bool WallHolds(BlockPos pos, BlockFacing wall, Block block)
    {
        var wallPos = pos.AddCopy(wall);
        return sapi.World.BlockAccessor.GetBlock(wallPos).CanAttachBlockAt(sapi.World.BlockAccessor, block, wallPos, wall.Opposite);
    }

    /// <summary>
    /// Where a decoration can go in the home, nearest the settler's bed first: a wall spot at the right height
    /// for wall pieces (paintings with room for their size), or a floor spot with its back to a wall and room
    /// in front for shelves and racks. Null when there is no room.
    /// </summary>
    (BlockPos Pos, BlockFacing Wall)? FindDecorSpot(HomeDecor seen, DecorKind kind, Block mount, BlockPos near)
    {
        var ba = sapi.World.BlockAccessor;
        string size = mount is BlockPainting painting ? painting.Size : "small";
        var spots = new List<(BlockPos, BlockFacing, double)>();
        var pos = new BlockPos(0);
        for (int x = seen.Min.X + 1; x < seen.Max.X; x++)
        for (int y = seen.Min.Y; y <= seen.Max.Y; y++)
        for (int z = seen.Min.Z + 1; z < seen.Max.Z; z++)
        {
            pos.Set(x, y, z);
            if (!FreeIndoors(seen, pos)) continue;
            int h = HeightAboveFloor(pos);
            bool fits = kind switch
            {
                DecorKind.Banner => h == 2,                 // the banner hangs down towards the floor
                DecorKind.Trophy => h is 1 or 2,
                DecorKind.Books or DecorKind.Bottles => h == 0,
                _ => h == 1                                // about eye height
            };
            if (!fits || NearDoor(pos)) continue;
            foreach (var wall in BlockFacing.HORIZONTALS)
            {
                if (TurnedFor(mount, wall) is not { } turned) continue;
                if (OnWall(kind) ? !WallFits(seen, pos, wall, turned, kind, size) : !FloorFits(seen, pos, wall)) continue;
                double score = pos.DistanceSqTo(near.X, near.Y, near.Z) - (kind == DecorKind.Trophy && h == 2 ? 2 : 0);
                spots.Add((pos.Copy(), wall, score));
            }
        }
        if (spots.Count == 0) return null;
        var best = spots.OrderBy(s => s.Item3).First();
        return (best.Item1, best.Item2);
    }

    bool WallFits(HomeDecor seen, BlockPos pos, BlockFacing wall, Block turned, DecorKind kind, string size)
    {
        var cells = kind == DecorKind.Painting ? PaintingCells(pos, wall, size).ToList() : new List<BlockPos> { pos };
        foreach (var cell in cells)
        {
            if (cell != pos && !FreeIndoors(seen, cell)) return false;
            if (!WallHolds(cell, wall, turned)) return false;
        }
        if (kind == DecorKind.Banner)
        {
            // Room below for the banner to hang.
            if (!FreeIndoors(seen, pos.DownCopy()) || !FreeIndoors(seen, pos.DownCopy(2))) return false;
        }
        return true;
    }

    /// <summary>A floor spot with a solid wall behind, room to walk in front, and no chest, bed or other block entity beside it to get in the way of.</summary>
    bool FloorFits(HomeDecor seen, BlockPos pos, BlockFacing wall)
    {
        var ba = sapi.World.BlockAccessor;
        var behind = pos.AddCopy(wall);
        if (!ba.GetBlock(behind).SideSolid[wall.Opposite.Index]) return false;
        var front = pos.AddCopy(wall.Opposite);
        if (!FreeIndoors(seen, front) || !FreeIndoors(seen, front.UpCopy()) || !FreeIndoors(seen, front.AddCopy(wall.Opposite))) return false;
        foreach (var side in BlockFacing.HORIZONTALS)
        {
            if (side == wall) continue;
            if (ba.GetBlockEntity(pos.AddCopy(side)) != null) return false;
        }
        return true;
    }

    // ---- Choosing what to put up ----------------------------------------------------------

    /// <summary>
    /// The next decoration this settler puts up at home, or null when the household has nothing for one,
    /// or no room. Kinds take turns by settler and day, so homes do not all fill up with the same thing.
    /// </summary>
    public DecorPlan? PlanDecor(Colony colony, CitizenRecord citizen, Building home, System.Func<BlockPos, bool> unreachable)
    {
        if (!Config.SettlersDecorate) return null;
        var chests = HomeChests(home);
        var spare = chests.Concat(WarehouseContainers(colony)).ToList();
        HomeDecor? seen = null;
        var near = citizen.HomeBedPos ?? home.HutPos;
        int start = (int)((citizen.CitizenId + (long)sapi.World.Calendar.TotalDays) % DecorKinds.Length);

        for (int n = 0; n < DecorKinds.Length; n++)
        {
            var kind = DecorKinds[(start + n) % DecorKinds.Length];
            if (kind == DecorKind.Painting)
            {
                if (FirstWith(chests, s => IsDecorMount(kind, s), unreachable) is not { } from) continue;
                var block = from.Inventory.First(s => !s.Empty && IsDecorMount(kind, s.Itemstack)).Itemstack.Block;
                seen ??= LookAtHome(home);
                if (FindDecorSpot(seen, kind, block, near) is not { } spot) continue;
                return new DecorPlan { Kind = kind, Spot = spot.Pos, Wall = spot.Wall, MountFrom = from.Pos.Copy() };
            }

            // A Hunter keeps its own trophies, from its hut or the warehouse.
            var sources = chests;
            if (kind == DecorKind.Trophy && citizen.JobCode == Jobs.Hunter && citizen.WorkplacePos != null && GetBuilding(colony, citizen.WorkplacePos) is { } hut)
            {
                sources = chests.Concat(HutStorage(colony, hut)).Concat(WarehouseContainers(colony)).ToList();
            }
            if (FirstWith(sources, s => IsDecorContent(kind, s), unreachable) is not { } content) continue;
            int have = CountItems(new[] { content }, s => IsDecorContent(kind, s));

            seen ??= LookAtHome(home);
            var ba = sapi.World.BlockAccessor;
            var mounts = seen.Mounts.TryGetValue(kind, out var list) ? list : new List<BlockPos>();
            var open = mounts.FirstOrDefault(p => !unreachable(p) && FreeDecorSlots(ba.GetBlockEntity(p)) > 0);
            if (open != null)
            {
                int free = FreeDecorSlots(ba.GetBlockEntity(open));
                return new DecorPlan { Kind = kind, Spot = open, Fill = true, ContentFrom = content.Pos.Copy(), Want = Math.Min(free, have) };
            }
            if (mounts.Count >= Config.DecorPerKind) continue;
            if (FirstWith(spare, s => IsDecorMount(kind, s), unreachable) is not { } mountFrom) continue;
            var mount = mountFrom.Inventory.First(s => !s.Empty && IsDecorMount(kind, s.Itemstack)).Itemstack.Block;
            if (FindDecorSpot(seen, kind, mount, near) is not { } at) continue;
            return new DecorPlan
            {
                Kind = kind, Spot = at.Pos, Wall = at.Wall, MountFrom = mountFrom.Pos.Copy(), ContentFrom = content.Pos.Copy(),
                Want = Math.Min(DecorCapacity(kind), have)
            };
        }
        return null;
    }

    static BlockEntityContainer? FirstWith(IEnumerable<BlockEntityContainer> containers, ItemMatch match, System.Func<BlockPos, bool> unreachable) =>
        containers.FirstOrDefault(c => !unreachable(c.Pos) && c.Inventory.Any(s => !s.Empty && match(s.Itemstack)));

    // ---- Putting it up --------------------------------------------------------------------

    /// <summary>
    /// Puts the decoration up from what the settler carries: places the painting, rack, shelf, mount or pole
    /// if it is not up yet, then puts the antlers, weapons, books, bottles or banner on it. False if the spot
    /// was taken in the meantime or nothing could be put up.
    /// </summary>
    public bool PutUpDecor(Colony colony, CitizenRecord citizen, Building home, DecorPlan plan)
    {
        var ba = sapi.World.BlockAccessor;
        bool done = false;
        if (!plan.Fill)
        {
            var sample = PackItems(citizen).FirstOrDefault(s => IsDecorMount(plan.Kind, s));
            if (sample?.Block is not { } mount || TurnedFor(mount, plan.Wall) is not { } turned) return false;
            var seen = LookAtHome(home);
            bool fits = FreeIndoors(seen, plan.Spot) && (OnWall(plan.Kind)
                ? WallFits(seen, plan.Spot, plan.Wall, turned, plan.Kind, mount is BlockPainting p ? p.Size : "small")
                : FloorFits(seen, plan.Spot, plan.Wall));
            if (!fits) return false;
            var stack = TakeFromPack(colony, citizen, s => IsDecorMount(plan.Kind, s) && s.Collectible == sample.Collectible, 1);
            if (stack == null) return false;
            PlaceDecor(turned, plan, stack);
            done = true;
        }

        var be = ba.GetBlockEntity(plan.Spot);
        if (DecorInventory(be) is { } inv)
        {
            bool changed = false;
            foreach (var slot in inv)
            {
                if (!slot.Empty || slot.MaxSlotStackSize <= 0) continue;
                // Only from the inventory, never a weapon off the settler's own tool bar.
                if (!PackItems(citizen).Any(s => IsDecorContent(plan.Kind, s))) break;
                var thing = TakeFromPack(colony, citizen, s => IsDecorContent(plan.Kind, s), 1);
                if (thing == null) break;
                var source = new DummySlot(thing);
                if (source.TryPutInto(sapi.World, slot, 1) <= 0)
                {
                    AddToPack(colony, citizen, thing, false);
                    continue;
                }
                slot.MarkDirty();
                changed = true;
            }
            if (changed)
            {
                // The game's own racks and shelves redraw when their contents arrive. A mod's display (the bottle
                // rack) does not, so players are told to redraw it once the contents are there.
                bool redrawsItself = be is not BlockEntityDisplay || be.GetType().Namespace == typeof(BlockEntityDisplay).Namespace;
                be!.MarkDirty(redrawsItself);
                if (!redrawsItself) sapi.ModLoader.GetModSystem<ColonyNetwork>().SendDisplayRedraw(plan.Spot);
                done = true;
            }
        }
        if (done)
        {
            AddThought(citizen, "decorated");
            MarkDirty(colony);
        }
        return done;
    }

    /// <summary>
    /// Places the block from its item, as a player does, so it takes the item's wood and look. Shelves and
    /// mounts are then turned to face away from the wall, and a Heraldry pole gets its banner.
    /// </summary>
    void PlaceDecor(Block turned, DecorPlan plan, ItemStack stack)
    {
        var ba = sapi.World.BlockAccessor;
        ba.SetBlock(turned.Id, plan.Spot, stack);
        var be = ba.GetBlockEntity(plan.Spot);
        if (be?.GetBehavior<BEBehaviorShapeMaterialFromAttributes>() is { } shaped)
        {
            // As the game turns them for a player standing in front, looking at the wall.
            var front = plan.Wall.Opposite;
            shaped.MeshAngleY = (float)Math.Atan2(front.Normali.X, front.Normali.Z);
            shaped.Init();
            be.MarkDirty(true);
        }
        ba.TriggerNeighbourBlockUpdate(plan.Spot);
        if (turned.Sounds?.Place != null) sapi.World.PlaySoundAt(turned.Sounds.Place, plan.Spot, 0);
    }

    /// <summary>What a settler carries for a decoration, so leftovers go back home and nothing else does.</summary>
    public static bool IsDecorItem(DecorKind kind, ItemStack s) => IsDecorMount(kind, s) || IsDecorContent(kind, s);
}
