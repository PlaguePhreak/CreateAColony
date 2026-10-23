"""
Turns MineColonies (Structurize) .blueprint files into Vintage Story WorldEdit exports.

Run:  python3 tools/blueprints/convert_minecolonies.py <style folder> [--library <out folder>]

<style folder> is a MineColonies style such as medievaloak/ (its subfolders hold the .blueprint files).
The huts this mod has (see PACKS) become the asset packs in assets/createacolony/config/blueprints/<pack>/,
checked like every other blueprint (kit.check); a picture of each building's levels goes to
tools/blueprints/previews/<pack>/. With --library, every blueprint in the style (also the buildings this
mod has no hut for, walls, decorations) is written as a plain WorldEdit export under <out folder>, in the
same folders, for /we import.

Minecraft blocks become the nearest Vintage Story block from Aaron's own exports (kit.VERIFIED and
kit.CONFIDENT): oak for every wood, shale for every stone, light mud brick for plaster and pale bricks,
oak shingles (plank stairs) for Domum Ornamentum roof tiles. Glass, carpets, plants, redstone and other
things with nothing close in the game are left out. Structurize's "keep what is there" placeholders are
left out and its "solid ground" placeholder becomes soil, which builders count as built on any ground.
"""
import argparse
import glob
import json
import math
import os
import sys
from collections import Counter

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import kit  # noqa: E402
import nbt  # noqa: E402
import render  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", ".."))
BLUEPRINTS = os.path.join(REPO, "assets", "createacolony", "config", "blueprints")

# pack folder: (name on the pictures, {our hut type: MineColonies building, alternative building or None})
MAIN = {
    "townhall": "townhall", "builder": "builder", "farmer": "farmer", "forester": "lumberjack",
    "cook": "cook", "residence": "residence", "guardtower": "guardtower", "tavern": "tavern",
    "warehouse": "warehouse", "miner": "miner",
    # MineColonies has no potter, prospector or quarry; these are the closest workshops.
    "potter": "glassblower", "prospector": "sifter", "quarry": "stonemason",
}
ALT = {"townhall": "alttownhall", "builder": "altbuilder", "farmer": "altfarmer", "forester": "altlumberjack",
       "cook": "altcook", "residence": "altresidence", "guardtower": "altguardtower", "warehouse": "altwarehouse",
       "miner": "altminer"}
PACKS = {
    "medievaloak": ("Medieval Oak", MAIN),
    "medievaloakalt": ("Medieval Oak (Alt)", {**MAIN, **ALT}),
}
SUPPLYCAMP = "supplycamp"
NAMES = {"townhall": "Town Hall", "builder": "Builder's Hut", "farmer": "Farmer's Hut", "forester": "Forester's Hut",
         "cook": "Cook's Hut", "potter": "Potter's Hut", "residence": "Residence", "guardtower": "Guard Tower",
         "tavern": "Tavern", "warehouse": "Warehouse", "prospector": "Prospector's Hut", "miner": "Miner's Hut",
         "quarry": "Quarry", "supplycamp": "Supply Camp"}

# ---- Materials ----------------------------------------------------------------------------------

LIGHT, WOOD, LOG, COBBLE, BRICK, POLISHED, ROCK = "light", "wood", "log", "cobble", "brick", "polished", "rock"
DARK_COLOURS = ("red", "brown", "purple", "magenta", "crimson")
STONY_COLOURS = ("gray", "black", "cyan", "blue", "green")


def family(mc):
    """What kind of material a Minecraft block (or Domum Ornamentum texture) is made of."""
    n = mc.split(":")[-1]
    if "cobble" in n:
        return COBBLE
    if n.endswith(("_log", "_wood", "_stem", "_hyphae")) or n in ("oak_wood",):
        return LOG
    if "planks" in n or n in ("bookshelf", "crafting_table", "barrel"):
        return WOOD
    if "paper" in n or "wheat" in n or "clay" in n or n in ("white_bricks", "cream_bricks", "beige_bricks"):
        return LIGHT
    if any(k in n for k in ("wool", "concrete", "terracotta")):
        colour = n.split("_")[0] if not n.startswith("light_") else "light"
        if colour in DARK_COLOURS:
            return WOOD
        if colour in STONY_COLOURS:
            return BRICK
        return LIGHT
    if "brick" in n or "tiles" in n:
        if n.startswith(("cream", "beige", "white", "yellow", "orange")):
            return LIGHT
        return BRICK
    if any(k in n for k in ("polished", "smooth", "quartz", "chiseled", "cut_", "pillar", "purpur")):
        return POLISHED
    return ROCK


FULL = {LIGHT: kit.COB, WOOD: kit.PLANKS, LOG: kit.LOG_UD, COBBLE: kit.STONE, BRICK: kit.ASHLAR,
        POLISHED: kit.POLISHED, ROCK: kit.STONE}  # natural rock cannot be carried, so it is built as cobblestone


def full(fam, axis="y"):
    if fam == LOG:
        return {"y": kit.LOG_UD, "x": kit.LOG_WE, "z": kit.LOG_NS}[axis]
    return FULL[fam]


def stair(fam, facing, half):
    material = "planks" if fam in (WOOD, LOG, LIGHT) else "ashlar" if fam in (BRICK, POLISHED) else "stone"
    return kit.stairs(material, facing, upside_down=half == "top")


def slab_of(fam, kind):
    if kind == "double":
        return full(fam)
    return kit.slab("planks" if fam in (WOOD, LOG, LIGHT) else "stone", "up" if kind == "top" else "down")


def textures(te):
    return (te or {}).get("textureData") or {}


def main_texture(te, default="minecraft:oak_planks"):
    t = textures(te)
    return t.get("minecraft:block/oak_planks") or next(iter(t.values()), default)


def centre_texture(te):
    t = textures(te)
    return t.get("minecraft:block/dark_oak_planks") or t.get("minecraft:block/clay") or main_texture(te)


DROP = ("glass", "pane", "iron_bars", "chain", "carpet", "flower", "potted_", "sapling", "leaves", "vine", "grass",
        "fern", "bush", "tulip", "poppy", "dandelion", "daisy", "bluet", "orchid", "allium", "cornflower",
        "lily", "rose", "peony", "lilac", "sunflower", "roots", "fungus", "mushroom", "kelp", "seagrass", "coral",
        "pickle", "water", "lava", "fire", "button", "lever", "pressure_plate", "rail", "redstone", "repeater",
        "hopper", "tripwire", "target", "note_block", "jukebox", "piston", "dispenser", "dropper", "bell", "anvil",
        "brewing", "enchanting", "skull", "head", "cake", "candle", "cobweb", "end_rod", "lightning_rod",
        "scaffolding", "beehive", "bee_nest", "sign", "banner_pattern", "fence_gate", "cactus", "bamboo",
        "melon", "pumpkin", "cauldron_", "dead_bush", "sugar_cane", "wheat", "carrots", "potatoes", "beetroots",
        "ender_chest", "spawner", "hanging_roots", "twisting", "weeping", "azalea", "moss_carpet", "tall_",
        "large_", "torchflower", "pitcher", "lectern", "composter", "jack_o_lantern", "carved_pumpkin")

TABLES = ("crafting_table", "fletching_table", "smithing_table", "cartography_table", "loom", "stonecutter",
          "grindstone")


def convert_block(name, props, te):
    """
    The Vintage Story block for one Minecraft block: a code, None to leave it out, or a tag for blocks
    placed afterwards ("door", "bed", "hut", "lantern", "torch", "banner"). Returns (code or tag, extra).
    """
    ns, n = name.split(":", 1)
    p = props
    facing, half = p.get("facing", "north"), p.get("half", "bottom")
    if name == "minecraft:air" or ns == "structurize" and n != "blocksolidsubstitution":
        return None, None
    if n == "blocksolidsubstitution":
        return "game:soil-medium-none", None
    if ns == "minecolonies":
        if n.startswith("blockhut"):
            return "hut", n[len("blockhut"):]
        if n == "blockminecoloniesrack":
            return kit.chest("south"), None
        if n == "barrel_block":
            return kit.BARREL, None
        if n == "composted_dirt":
            return "game:soil-medium-none", None
        if n == "colony_wall_banner":
            return "banner", facing
        return None, None
    if ns == "domum_ornamentum":
        if n in ("shingle", "vanilla_stairs_compat"):
            fam = WOOD if n == "shingle" else family(main_texture(te))
            return stair(fam, facing, half), None
        if n in ("shingle_slab",):
            return kit.slab("planks"), None
        if n == "vanilla_slab_compat":
            return slab_of(family(main_texture(te)), p.get("type", "bottom")), None
        if n in ("plain", "framed", "one_crossed_lr", "one_crossed_rl", "double_crossed", "side_framed",
                 "horizontal_plain", "side_framed_horizontal", "up_gated", "down_gated"):
            return full(family(centre_texture(te))), None
        if n in ("squarepillar", "blockypillar"):
            return kit.LOG_UD, None
        if n in ("vanilla_fence_compat",):
            return (kit.FENCE if family(main_texture(te)) in (WOOD, LOG) else kit.DRYSTONE).format("empty"), None
        if n == "vanilla_wall_compat":
            return kit.DRYSTONE.format("empty"), None
        if n in ("panel", "fancy_trapdoors", "vanilla_trapdoors_compat"):
            return (None if p.get("open") == "true" else kit.slab("planks", "up" if half == "top" else "down")), None
        if n == "fancy_door":
            return "door", (facing, half)
        if n.startswith("blockbarreldeco"):
            return kit.BARREL, None
        if n in ("fancy_light", "center_light"):
            return "lantern", None
        if n.endswith("_bricks") or n.endswith("_extra"):
            return full(family(n)), None
        return None, None
    # Minecraft
    if n.endswith("_door"):
        return "door", (facing, half)
    if n.endswith("_bed"):
        return "bed", (facing, p.get("part", "foot"))
    if n in ("lantern", "soul_lantern"):
        return "lantern", None
    if n in ("wall_torch", "soul_wall_torch"):
        return "torch", facing
    if n in ("torch", "soul_torch"):
        return "torch", "up"
    if n.endswith("_wall_banner"):
        return None, None
    if n == "ladder":
        return "ladder", kit.OPPOSITE[facing]
    if n in ("campfire", "soul_campfire"):
        return kit.FIREPIT, None
    if n in ("furnace", "smoker", "blast_furnace"):
        return kit.OVEN, None
    if n in TABLES:
        return kit.TABLE, None
    if n == "chest" or n == "trapped_chest":
        return kit.chest(facing), None
    if n == "barrel":
        return kit.BARREL, None
    if n == "bookshelf" or n == "chiseled_bookshelf":
        return kit.BOOKSHELF, None
    if n == "hay_block":
        return kit.HAY, None
    if n in ("cauldron", "water_cauldron", "decorated_pot"):
        return kit.VESSEL, None
    if n == "grass_block":
        return "game:soil-medium-normal", None
    if n in ("dirt", "coarse_dirt", "rooted_dirt", "podzol", "mud", "farmland", "sand", "red_sand", "soul_soil",
             "soul_sand", "mycelium", "clay"):
        return "game:soil-medium-none", None
    if n in ("dirt_path",):
        return kit.DAUB, None
    if n == "gravel":
        return kit.PATH, None
    if n.endswith("_trapdoor"):
        return (None if p.get("open") == "true" else kit.slab("planks", "up" if half == "top" else "down")), None
    if any(k in n for k in DROP):
        return None, None
    fam = family(n)
    if n.endswith("_stairs"):
        return stair(fam, facing, half), None
    if n.endswith("_slab"):
        return slab_of(fam, p.get("type", "bottom")), None
    if n.endswith("_fence"):
        return (kit.FENCE if fam in (WOOD, LOG) else kit.DRYSTONE).format("empty"), None
    if n.endswith("_wall"):
        return kit.DRYSTONE.format("empty"), None
    if n.endswith(("_log", "_wood", "_stem", "_hyphae")):
        if n.startswith("stripped_") and p.get("axis", "y") == "y":
            return kit.DEBARKED, None
        return full(LOG, p.get("axis", "y")), None
    return full(fam), None


# ---- Reading a blueprint ----------------------------------------------------------------------------

def read(path):
    d = nbt.read(path)
    sx, sy, sz = d["size_x"], d["size_y"], d["size_z"]
    shorts = []
    for v in d["blocks"]:
        shorts += [(v >> 16) & 0xFFFF, v & 0xFFFF]
    palette = d["palette"]
    tes = {(t["x"], t["y"], t["z"]): t for t in d.get("tile_entities", [])}
    blocks, i = {}, 0
    for y in range(sy):
        for z in range(sz):
            for x in range(sx):
                entry = palette[shorts[i]]
                i += 1
                if entry["Name"] != "minecraft:air":
                    blocks[(x, y, z)] = (entry["Name"], entry.get("Properties", {}), tes.get((x, y, z)))
    off = (d.get("optional_data") or {}).get("structurize", {}).get("primary_offset")
    return blocks, (off["x"], off["y"], off["z"]) if off else None


DIRS = {"north": (0, -1), "south": (0, 1), "east": (1, 0), "west": (-1, 0)}


def build(path, type=None, building=None):
    """
    A kit.Build from a .blueprint. type: our hut type (its hut block replaces the MineColonies one at the
    blueprint's primary offset), None for a plain export with no hut block. Returns (build, notes).
    """
    blocks, offset = read(path)
    b = kit.Build(type or "export", 1)
    notes = []
    dropped = Counter()
    late = []
    for pos, (name, props, te) in blocks.items():
        code, extra = convert_block(name, props, te)
        if code is None:
            if name != "minecraft:air" and not name.startswith("structurize:"):
                dropped[name.split(":")[1]] += 1
            continue
        if code in ("door", "bed", "hut", "lantern", "torch", "ladder", "banner"):
            late.append((pos, code, extra, props))
            continue
        b.put(*pos, code)
    beds_done = set()
    for pos, code, extra, props in late:
        x, y, z = pos
        if code == "hut":
            if type and pos == offset:
                b.put(x, y, z, kit.hut_code(type))
            continue
        if code == "door":
            facing, half = extra
            if half == "upper":
                continue
            kind = "game:door-solid-oak"
            rot = 0.0 if facing in ("north", "south") else math.pi / 2
            b.put(x, y, z, kind, kit.door_data(kind, x, y, z, rot))
            b.put(x, y + 1, z, kit.DOOR_TOP)
        elif code == "bed":
            facing, part = extra
            dx, dz = DIRS[facing]
            head = pos if part == "head" else (x + dx, y, z + dz)
            foot = (head[0] - dx, y, head[2] - dz)
            if head in beds_done:
                continue
            beds_done.add(head)
            a, c = sorted([head, foot])  # a: the west or north end
            if dx:  # lying east-west: a hay bed, head at the west end
                b.put(*a, "game:bed-hay-head-east"), b.put(*c, "game:bed-hay-feet-east")
            else:  # north-south: a wooden bed, head at the south end
                b.put(*c, "game:bed-wood-head-north"), b.put(*a, "game:bed-wood-feet-north")
        elif code == "lantern":
            hanging = props.get("hanging") == "true"
            b.lantern(x, y, z, "down" if hanging else "up")
        elif code == "torch":
            b.put(x, y, z, kit.torch(extra) if extra != "up" else "game:torch-basic-lit-up")
        elif code == "ladder":
            b.put(x, y, z, f"game:ladder-wood-oak-{extra}")
        elif code == "banner":
            side = kit.OPPOSITE[extra]
            if side in ("north", "east", "south"):
                b.put(x, y, z, f"heraldrybanners:pole-banner-wall2-{side}", kit.BANNER_DATA)
                b.items.update(kit.BANNER_ITEMS)
    if dropped:
        notes.append("left out: " + ", ".join(f"{k} {n}" for k, n in dropped.most_common(8)) + (" and more" if len(dropped) > 8 else ""))
    return b, offset, notes


# ---- Making them fit this mod --------------------------------------------------------------------

def tidy(b):
    """Fixes what a straight copy leaves that the checks refuse: lights, ladders and beds with nothing to hold them."""
    fixes = Counter()
    for (x, y, z), (code, data) in list(b.blocks.items()):
        if code == "game:lantern-small-down" and b.get(x, y + 1, z) is None:
            b.put(x, y, z, kit.lantern_code("up"), kit.lantern_data(x, y, z, "up"))
            code = "game:lantern-small-up"
        if code in ("game:lantern-small-up", "game:torch-basic-lit-up") and b.get(x, y - 1, z) is None:
            b.rm(x, y, z)
            fixes["light with nothing under it"] += 1
        elif code.startswith("game:torch-basic-lit-") and not code.endswith("-up"):
            dx, dz = kit.SIDES[kit.OPPOSITE[code.rsplit("-", 1)[1]]]
            if b.get(x + dx, y, z + dz) in (None, kit.DOOR_TOP):
                b.rm(x, y, z)
                fixes["torch with no wall"] += 1
        elif code.startswith("game:ladder-"):
            dx, dz = kit.SIDES[code.rsplit("-", 1)[1]]
            behind = b.get(x + dx, y, z + dz)
            if behind in (None, kit.DOOR_TOP) or behind.startswith("game:ladder-"):
                b.put(x + dx, y, z + dz, kit.PLANKS)
                fixes["planks put behind a ladder"] += 1
    return fixes


def fit_hut(b, type, offset):
    """Hut block rules: something under it, nothing on it."""
    x, y, z = offset
    if b.get(x, y - 1, z) is None:
        b.put(x, y - 1, z, kit.PLANKS)
    if b.get(x, y + 1, z) is not None:
        b.rm(x, y + 1, z)
    if kit.hut_code(type) not in {c for c, _ in b.blocks.values()}:
        b.put(x, y, z, kit.hut_code(type))


def ground_of(b, offset):
    return offset[1]


def add_work_blocks(b, type, offset, notes):
    """Puts in what our jobs need where the MineColonies building has something else for it."""
    codes = Counter(c for c, _ in b.blocks.values())
    hx, hy, hz = offset
    if type in ("cook", "tavern") and not codes[kit.FIREPIT]:
        ovens = [p for p, (c, _) in b.blocks.items() if c == kit.OVEN]
        spot = min(ovens, key=lambda p: abs(p[0] - hx) + abs(p[2] - hz) + abs(p[1] - hy) * 3) if ovens else None
        if spot is None:
            spot = free_floor_spot(b, offset)
        b.put(*spot, kit.FIREPIT)
        notes.append(f"a fire pit at {rel(spot, offset)} ({'in place of an oven' if ovens else 'on the floor'})")
    if type == "miner":
        ladders = sorted(p for p, (c, _) in b.blocks.items() if c.startswith("game:ladder-"))
        if ladders:
            low = min(ladders, key=lambda p: (p[1], abs(p[0] - hx) + abs(p[2] - hz)))
            spot = low
        else:
            spot = free_floor_spot(b, offset)
        b.put(*spot, "createacolony:waypoint-mine")
        notes.append(f"the Mine waypoint at {rel(spot, offset)} (the bottom of the shaft ladder)" if ladders else
                     f"the Mine waypoint at {rel(spot, offset)}")
    if type == "quarry":
        xs = [p[0] for p in b.blocks]
        zs = [p[2] for p in b.blocks]
        spot = ((min(xs) + max(xs)) // 2, hy, min(zs) - 8)
        b.put(spot[0], spot[1] - 1, spot[2], kit.PATH)
        b.put(*spot, "createacolony:waypoint-quarry")
        notes.append(f"the Quarry waypoint at {rel(spot, offset)}, 8 blocks beyond the north wall, so the pit misses the building")
    if type == "guardtower" and not any(c.startswith("game:chest-") for c in codes):
        spot = free_floor_spot(b, offset)
        b.put(*spot, kit.chest("south"))
        notes.append(f"a chest for weapons at {rel(spot, offset)}")
    if type in ("residence", "guardtower") and not any(c.startswith("game:bed-") for c in codes):
        spot = free_floor_spot(b, offset, wide=True)
        b.bed(*spot)
        notes.append(f"a bed at {rel(spot, offset)}")


def free_floor_spot(b, offset, wide=False):
    """
    An empty spot on a floor near the hut block (on its storey if it can, else the nearest storey above or
    below), away from doors. wide: the spot east of it is free too, for a bed.
    """
    hx, hy, hz = offset
    best = None
    for y in range(hy - 3, hy + 16):
        for dx in range(-7, 8):
            for dz in range(-7, 8):
                p = (hx + dx, y, hz + dz)
                cells = [p, (p[0] + 1, y, p[2])] if wide else [p]
                if any((c[0], c[2]) == (hx, hz) or b.get(*c) is not None or b.get(c[0], y - 1, c[2]) in (None, kit.DOOR_TOP)
                       or (b.get(c[0], y - 1, c[2]) or "").startswith(("game:ladder", "game:door", "game:bed", "game:chest"))
                       or b.get(c[0], y + 1, c[2]) is not None for c in cells):
                    continue
                if any((b.get(c[0] + ex, y, c[2] + ez) or "").startswith(("game:door-", "game:ladder-"))
                       for c in cells for ex, ez in DIRS.values()):
                    continue
                d = abs(dx) + abs(dz) + abs(y - hy) * 3
                if best is None or d < best[0]:
                    best = (d, p)
    return best[1] if best else (hx + 1, hy, hz)


def clear_doors(b):
    """Takes away loose furnishings standing in a doorway (as MineColonies allows, but our builders do not)."""
    removed = 0
    keep = (kit.STONE, kit.ASHLAR, kit.PLANKS, kit.COB, kit.LOG_UD, kit.LOG_NS, kit.LOG_WE)
    for (x, y, z), (code, _) in list(b.blocks.items()):
        if not code.startswith("game:door-"):
            continue
        def solid(c):
            return c is not None and c != kit.DOOR_TOP and not c.startswith("game:door-")
        sx = sum(1 for d in (-1, 1) for dy in (0, 1) if solid(b.get(x + d, y + dy, z)))
        sz = sum(1 for d in (-1, 1) for dy in (0, 1) if solid(b.get(x, y + dy, z + d)))
        ax, az = (0, 1) if sx >= sz else (1, 0)
        for d in (-1, 1):
            for dy in (0, 1):
                p = (x + d * ax, y + dy, z + d * az)
                other = b.get(*p)
                if other and not other.startswith(("game:torch", "heraldry", "game:ladder-", "game:lantern")):
                    if other in keep or other.startswith("game:door-") or other == kit.DOOR_TOP:
                        continue
                    b.rm(*p)
                    removed += 1
    return removed


def rel(p, offset):
    return (p[0] - offset[0], p[1] - offset[1], p[2] - offset[2])


# ---- Running ----------------------------------------------------------------------------------------

NEEDS = {
    "cook": [lambda c: c == kit.FIREPIT],
    "tavern": [lambda c: c == kit.FIREPIT],
    "residence": [lambda c: c.startswith("game:bed-")],
    "guardtower": [lambda c: c.startswith("game:bed-"), lambda c: c.startswith("game:chest-")],
    "warehouse": [lambda c: c.startswith("game:chest-")],
    "miner": [lambda c: c == "createacolony:waypoint-mine"],
    "quarry": [lambda c: c == "createacolony:waypoint-quarry"],
}
for _type, _tests in NEEDS.items():
    for _t in _tests:
        _t.__doc__ = "the work blocks for " + _type


def index(style):
    """{building name: {level: path}} for a style folder."""
    out = {}
    for f in glob.glob(os.path.join(style, "**", "*.blueprint"), recursive=True):
        name = os.path.basename(f)[:-len(".blueprint")]
        base = name.rstrip("0123456789")
        level = int(name[len(base):]) if name[len(base):] else 1
        out.setdefault(base, {})[level] = f
    return out


def warehouse_reach(rel_blocks):
    r, h = 5, 3
    return sum(1 for (x, y, z), c in rel_blocks.items() if c.startswith("game:chest-") and abs(x) <= r and abs(z) <= r and abs(y) <= h)


def make_pack(pack, style, report):
    title, mapping = PACKS[pack]
    found = index(style)
    out, previews = os.path.join(BLUEPRINTS, pack), os.path.join(HERE, "previews", pack)
    os.makedirs(previews, exist_ok=True)
    failed = False
    jobs = list(mapping.items()) + [("supplycamp", SUPPLYCAMP)]
    for type, building in jobs:
        levels = found.get(building, {})
        pictures, lines = [], []
        for level in sorted(levels):
            if level > 5:
                continue
            b, offset, notes = build(levels[level], type)
            if type == "supplycamp":
                # MineColonies' supply camp has no hut block: ours goes by the camp fire, or the middle.
                offset = supplycamp_spot(b)
            if offset is None:
                print(f"{pack} {type} level {level}: no hut position in {levels[level]}")
                failed = True
                continue
            fixes = tidy(b)
            fit_hut(b, type, offset)
            add_work_blocks(b, type, offset, notes)
            gone = clear_doors(b)
            if gone:
                fixes["furnishing taken out of a doorway"] += gone
            bp = kit.export(b)
            problems = kit.check(bp, type, NEEDS.get(type, ()), door_needed=False)
            if not any(c.startswith("game:door-") for c in bp["BlockCodes"].values()):
                notes.append("no door (open-sided in MineColonies), so it is placed facing the way it was drawn")
            name = f"{pack} {type} level {level} (from {building}{level})"
            if problems:
                failed = True
                print(f"{name}: NOT written")
                for p in problems[:12]:
                    print("   ", p)
                continue
            relb = kit.relative(bp, type)
            line = f"{name}: {bp['SizeX']}x{bp['SizeY']}x{bp['SizeZ']}, {len(relb)} blocks"
            beds = sum(1 for c in relb.values() if c.startswith("game:bed-") and "-head-" in c)
            chests = sum(1 for c in relb.values() if c.startswith("game:chest-"))
            line += f", {beds} beds, {chests} chests"
            if type == "warehouse":
                line += f" ({warehouse_reach(relb)} within the warehouse's reach)"
            print(line)
            for n in notes + [f"{k}: {v}" for k, v in fixes.items()]:
                print("    " + n)
            os.makedirs(os.path.join(out, type), exist_ok=True)
            with open(os.path.join(out, type, f"level{level}.json"), "w") as f:
                json.dump(bp, f, separators=(",", ":"))
            pictures.append(relb)
            lines.append(line)
            lines += ["    " + n for n in notes]
            for item, n in sorted(kit.materials(bp).items(), key=lambda kv: -kv[1]):
                lines.append(f"    {n:5d}  {item}")
            lines.append("")
        if pictures:
            render.sheet(pictures, f"{NAMES[type]} - {title}", os.path.join(previews, f"{type}.png"), cut=2)
            with open(os.path.join(previews, f"{type}.txt"), "w") as f:
                f.write(f"{NAMES[type]} ({title}), from MineColonies' {building}: what each level needs, one item per block\n\n" + "\n".join(lines))
        report[(pack, type)] = len(pictures)
    return failed


def supplycamp_spot(b):
    fires = [p for p, (c, _) in b.blocks.items() if c == kit.FIREPIT]
    xs = [p[0] for p in b.blocks]
    zs = [p[2] for p in b.blocks]
    ys = Counter(p[1] for p, (c, _) in b.blocks.items() if not c.startswith("game:soil"))
    if fires:
        fx, fy, fz = fires[0]
        for dx, dz in ((2, 0), (-2, 0), (0, 2), (0, -2), (1, 1)):
            p = (fx + dx, fy, fz + dz)
            if b.get(*p) is None and b.get(p[0], fy + 1, p[2]) is None:
                return p
    floor = min(ys) if ys else 0
    return ((min(xs) + max(xs)) // 2, floor, (min(zs) + max(zs)) // 2)


def make_library(style, outdir):
    """Every blueprint in the style as a plain WorldEdit export, no hut blocks."""
    n = 0
    for f in glob.glob(os.path.join(style, "**", "*.blueprint"), recursive=True):
        b, _, _ = build(f, None)
        tidy(b)
        if not b.blocks:
            continue
        bp = kit.export(b)
        target = os.path.join(outdir, os.path.relpath(f, style))[:-len(".blueprint")] + ".json"
        os.makedirs(os.path.dirname(target), exist_ok=True)
        with open(target, "w") as fh:
            json.dump(bp, fh, separators=(",", ":"))
        n += 1
    print(f"library: {n} WorldEdit exports in {outdir}")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("style")
    ap.add_argument("--library")
    ap.add_argument("--pack", action="append")
    args = ap.parse_args()
    report, failed = {}, False
    for pack in args.pack or PACKS:
        failed |= make_pack(pack, args.style, report)
    if args.library:
        make_library(args.style, args.library)
    sys.exit(1 if failed else 0)


if __name__ == "__main__":
    main()
