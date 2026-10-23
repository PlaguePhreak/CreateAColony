"""
Building kit for the generated asset packs (12th Century Inspired, KCD2 Inspired): blocks, walls, roofs, doors and furniture,
written out as Vintage Story 1.22 WorldEdit exports that the mod's builders can build.

Every block code used here comes from Aaron's own exports (game 1.22.7), apart from the few listed in
CONFIDENT, which are vanilla variants of blocks his exports do use (other fence joins, other torch sides,
chests). check() refuses anything else.

Coordinates: x east, y up, z south. The hut block stands at (0, 0, 0) on the floor at y -1. Every
building's front door is in its south wall, so the mod turns the front towards the player placing it.
"""
import base64
import json
import math
import struct
from collections import Counter

# ---- Blocks ---------------------------------------------------------------------------------

LOG_UD, LOG_NS, LOG_WE = "game:log-placed-oak-ud", "game:log-placed-oak-ns", "game:log-placed-oak-we"
DEBARKED = "game:debarkedlog-oak-ud"
DAUB = "game:packeddirt"            # wattle and daub infill
COB = "game:mudbrick-light"         # cob / mud brick
PLANKS = "game:planks-oak-hor"
PLANKS_NS, PLANKS_WE, PLANKS_UD = "game:planks-oak-ns", "game:planks-oak-we", "game:planks-oak-ud"
EARTH = "game:rammed-light-plain"   # rammed earth floor
STONE = "game:cobblestone-shale"    # rubble stone walls
ASHLAR = "game:stonebricks-shale"   # dressed stone for corners and doorways
POLISHED = "game:rockpolished-shale"
PATH = "game:stonepath-free"
HAY = "game:hay-normal-ud"          # thatch
FIREPIT = "game:firepit-cold"
OVEN = "game:clayoven-east"
BARREL = "game:barrel"
VESSEL = "game:storagevessel-red-fired"
BASKET = "game:stationarybasket-north"
BOOKSHELF = "game:bookshelf"
DOOR_TOP = "game:multiblock-monolithic-0-p1-0"
HUT = "createacolony:hut-{}"

# W4RD0's Furniture (optional; skipped with a warning when the mod is not installed). The "rough" line
# has no wood types, so nothing about it has to be saved.
TABLE = "w4rd0sfurniture:roughtable-north"
BENCH = "w4rd0sfurniture:roughbench-west"
STOOL = "w4rd0sfurniture:roughstool-west"


def chair(back):
    """A dining chair with its back to that side (north, east, south, west)."""
    return f"w4rd0sfurniture:roughchairdining-{back}"


def stairs(material, rises, upside_down=False):
    """A stair block that climbs towards `rises`."""
    half = "down" if upside_down else "up"
    return {
        "planks": f"game:plankstairs-oak-{half}-{rises}-free",
        "stone": f"game:cobblestonestairs-shale-{half}-{rises}-free",
        "ashlar": f"game:stonebrickstairs-shale-{half}-{rises}-free",
    }[material]


def slab(material, half="down"):
    return {"planks": f"game:plankslab-oak-{half}-free", "stone": f"game:cobblestoneslab-shale-{half}-free"}[material]


def torch(facing):
    """A wall torch that faces this way (it hangs on the wall on the other side)."""
    return f"game:torch-basic-lit-{facing}"


def ladder():
    """A ladder against the block north of it."""
    return "game:ladder-wood-oak-north"


def lantern_code(facing):
    """A copper lantern: "down" hangs from the block above, a side faces that way off the wall behind it."""
    return f"game:lantern-small-{facing}"


def chest(facing):
    return f"game:chest-{facing}"


# Fences are written with the right joins for their neighbours (see Build.join_fences).
FENCE = "game:woodenfence-oak-{}-free"
DRYSTONE = "game:drystonefence-shale-{}-free"
WATTLE = "game:wattle-sticks-{}-free"

VERIFIED = set("""
game:log-placed-oak-ud game:log-placed-oak-ns game:log-placed-oak-we game:debarkedlog-oak-ud
game:packeddirt game:mudbrick-light game:planks-oak-hor game:planks-oak-ns game:planks-oak-we game:planks-oak-ud
game:rammed-light-plain game:cobblestone-shale game:stonebricks-shale game:rockpolished-shale game:stonepath-free
game:hay-normal-ud game:firepit-cold game:clayoven-east game:barrel game:storagevessel-red-fired
game:stationarybasket-north game:stationarybasket-east game:bookshelf game:multiblock-monolithic-0-p1-0
game:door-crude game:door-solid-oak game:ladder-wood-oak-north game:ladder-stick-north
game:bed-hay-head-east game:bed-hay-feet-east game:bed-wood-head-north game:bed-wood-feet-north
game:torch-basic-lit-east game:torch-basic-lit-west game:torch-basic-lit-north game:torch-basic-lit-south
game:plankslab-oak-down-free game:plankslab-oak-up-free game:cobblestoneslab-shale-down-free game:cobblestoneslab-shale-up-free
w4rd0sfurniture:roughtable-north w4rd0sfurniture:roughbench-west w4rd0sfurniture:roughstool-west
w4rd0sfurniture:roughchairdining-north w4rd0sfurniture:roughchairdining-east
w4rd0sfurniture:roughchairdining-south w4rd0sfurniture:roughchairdining-west
heraldrybanners:pole-banner-wall2-north heraldrybanners:pole-banner-wall2-east heraldrybanners:pole-banner-wall2-south
createacolony:waypoint-mine createacolony:waypoint-quarry
game:lantern-small-down game:lantern-small-north game:lantern-small-east game:lantern-small-south game:lantern-small-west
game:soil-medium-none game:soil-medium-normal game:soil-medium-sparse game:soil-medium-verysparse
""".split())
for _half in ("up", "down"):
    for _side in ("north", "east", "south", "west"):
        VERIFIED.add(f"game:plankstairs-oak-{_half}-{_side}-free")
        VERIFIED.add(f"game:cobblestonestairs-shale-{_half}-{_side}-free")
        VERIFIED.add(f"game:stonebrickstairs-shale-{_half}-{_side}-free")
for _joins in ("e", "empty", "es", "esw", "ew", "n", "ne", "nes", "new", "ns", "nsw", "nw", "s", "sw", "w"):
    VERIFIED.add(FENCE.format(_joins))
for _joins in ("e", "empty", "es", "ew", "n", "ne", "ns", "nw", "s", "sw", "w"):
    VERIFIED.add(DRYSTONE.format(_joins))
for _joins in ("e", "es", "ew", "n", "ne", "new", "ns", "nw", "s", "sw", "w"):
    VERIFIED.add(WATTLE.format(_joins))

# Vanilla variants of blocks Aaron's exports use, not seen in an export themselves.
CONFIDENT = {chest(s) for s in ("north", "east", "south", "west")}
# Ladders on the other walls, a torch and a lantern standing on the floor.
CONFIDENT.update(f"game:ladder-wood-oak-{s}" for s in ("east", "south", "west"))
CONFIDENT.update({"game:torch-basic-lit-up", "game:lantern-small-up"})
for _joins in ("nesw", "esw", "nes", "new", "nsw"):
    CONFIDENT.update({FENCE.format(_joins), DRYSTONE.format(_joins), WATTLE.format(_joins)})
CONFIDENT -= VERIFIED

# ---- Block entity data ------------------------------------------------------------------------
# A door needs its saved turn, or the mod cannot hang it. These are written like the game does
# (a TreeAttribute in Ascii85), with the same fields as the doors in Aaron's exports.


def _str(s):
    data = s.encode("utf8")
    n, out = len(data), b""
    while True:
        c = n & 0x7F
        n >>= 7
        out += bytes([c | 0x80]) if n else bytes([c])
        if not n:
            return out + data


def _tree(items):
    out = b""
    for kind, key, value in items:
        out += bytes([kind]) + _str(key)
        out += {1: lambda v: struct.pack("<i", v), 4: lambda v: struct.pack("<f", v),
                5: _str, 9: lambda v: bytes([1 if v else 0])}[kind](value)
    return out + b"\x00"


def door_data(code, x, y, z, rot):
    return base64.a85encode(_tree([
        (1, "posx", x), (1, "posy", y), (1, "posz", z), (5, "blockCode", code.split(":")[1]),
        (4, "rotateYRad", rot), (9, "opened", False), (9, "invertHandles", False),
    ])).decode()


# Lanterns as in Aaron's exports: copper, plain lining, quartz glass, turned to face out from the wall.
LANTERN_ANGLE = {"south": 0.0, "west": -math.pi / 2, "north": -math.pi, "east": math.pi / 2, "down": -math.pi / 4, "up": -math.pi / 4}


def lantern_data(x, y, z, facing):
    code = lantern_code(facing)
    return base64.a85encode(_tree([
        (1, "posx", x), (1, "posy", y), (1, "posz", z), (5, "blockCode", code.split(":")[1]),
        (5, "material", "copper"), (5, "lining", "plain"), (5, "glass", "quartz"), (4, "meshAngle", LANTERN_ANGLE[facing]),
    ])).decode()


# A Heraldry wall pole with Aaron's plain pennant, copied as it is from his Guard Tower level 5. The data
# names the banner item by its number in his world (4472), so the export's ItemCodes must say what 4472 is.
# The colony banner (Town Hall window, Banners) replaces the design.
BANNER_ITEMS = {"4472": "heraldrybanners:banner"}
BANNER_DATA = '!<d@[F*LO.#64c-E,TfC(B=F8!<d@[F*_3?#64o6@VfsmCI<#NALD/&Ea`cpEd;,&DJj$+F#n8YCh52-@;]soE\\2KDChtjYDJsQ;BEeW^H#R>9"U\'$fDe&rg@;>iI$#+:#ASuU.Ed7ZiEH>r7FE7H_!!!3,F)5c:EsD_"!!*\'"!-f(U!!*\'"!!WUKH#R>9"U/s\\@;ochFCB-"EbTE(?Z9RoDIIF)"U8dOH"D"N$Y+*kDfT3(Cgh$qz!!'


# ---- The building -------------------------------------------------------------------------------

SIDES = {"north": (0, -1), "south": (0, 1), "east": (1, 0), "west": (-1, 0)}
OPPOSITE = {"north": "south", "south": "north", "east": "west", "west": "east"}


class Build:
    def __init__(self, type, level):
        self.type, self.level = type, level
        self.blocks = {}   # (x, y, z) -> (code, block entity data or None)
        self.items = {}    # ItemCodes needed by saved block entity data
        self.notes = []

    # Basic editing

    def put(self, x, y, z, code, data=None):
        self.blocks[(x, y, z)] = (code, data)

    def get(self, x, y, z):
        b = self.blocks.get((x, y, z))
        return b[0] if b else None

    def rm(self, x, y, z):
        self.blocks.pop((x, y, z), None)

    def fill(self, x0, y0, z0, x1, y1, z1, code):
        for x in range(min(x0, x1), max(x0, x1) + 1):
            for y in range(min(y0, y1), max(y0, y1) + 1):
                for z in range(min(z0, z1), max(z0, z1) + 1):
                    self.put(x, y, z, code)

    def clear(self, x0, y0, z0, x1, y1, z1):
        for x in range(min(x0, x1), max(x0, x1) + 1):
            for y in range(min(y0, y1), max(y0, y1) + 1):
                for z in range(min(z0, z1), max(z0, z1) + 1):
                    self.rm(x, y, z)

    def hut(self, x=0, y=0, z=0):
        self.put(x, y, z, hut_code(self.type))

    # Floors and walls

    def floor(self, x0, z0, x1, z1, y, code=EARTH, edge=None):
        """A floor; edge, when given, is the material under the walls round it."""
        self.fill(x0, y, z0, x1, y, z1, code)
        if edge:
            for x, z in perimeter(x0, z0, x1, z1):
                self.put(x, y, z, edge)

    def timber_walls(self, x0, z0, x1, z1, y0, height, infill=DAUB, plinth=STONE, posts=2):
        """
        Timber-framed walls round a rectangle: corner posts and a post every `posts` blocks, a wall plate
        of logs along the top, wattle and daub (or planks, cob) between, and a stone sill course at the
        bottom when `plinth` is set.
        """
        top = y0 + height - 1
        for x, z in perimeter(x0, z0, x1, z1):
            along_x = z in (z0, z1) and x0 < x < x1
            corner = x in (x0, x1) and z in (z0, z1)
            post = corner or (along_x and (x - x0) % posts == 0) or (not along_x and (z - z0) % posts == 0)
            for y in range(y0, top + 1):
                if y == y0 and plinth:
                    code = plinth
                elif y == top and not corner:
                    code = LOG_WE if along_x else LOG_NS
                elif post:
                    code = LOG_UD
                else:
                    code = infill
                self.put(x, y, z, code)

    def log_walls(self, x0, z0, x1, z1, y0, height, plinth=STONE):
        """A log cabin: logs laid along each wall, upright logs at the corners."""
        for x, z in perimeter(x0, z0, x1, z1):
            corner = x in (x0, x1) and z in (z0, z1)
            for y in range(y0, y0 + height):
                if y == y0 and plinth:
                    self.put(x, y, z, plinth)
                elif corner:
                    self.put(x, y, z, LOG_UD)
                else:
                    self.put(x, y, z, LOG_WE if z in (z0, z1) else LOG_NS)

    def stone_walls(self, x0, z0, x1, z1, y0, height, quoins=True):
        """Rubble stone walls with dressed stone at the corners."""
        for x, z in perimeter(x0, z0, x1, z1):
            corner = x in (x0, x1) and z in (z0, z1)
            for y in range(y0, y0 + height):
                self.put(x, y, z, ASHLAR if corner and quoins else STONE)

    def windows(self, cells, y):
        """Small unglazed window openings in the walls at these (x, z) spots."""
        for x, z in cells:
            self.rm(x, y, z)

    def door(self, x, y, z, kind="crude", wall="ns"):
        """A door at (x, y, z), two blocks tall. wall "ns" = in a north or south wall, "ew" = east or west."""
        code = "game:door-crude" if kind == "crude" else "game:door-solid-oak"
        rot = 0.0 if wall == "ns" else math.pi / 2
        self.put(x, y, z, code, door_data(code, x, y, z, rot))
        self.put(x, y + 1, z, DOOR_TOP)

    def doorway(self, x, y, z, height=2):
        """An open doorway (no door)."""
        for h in range(height):
            self.rm(x, y + h, z)

    def path(self, cells, y=-1):
        for x, z in cells:
            self.put(x, y, z, PATH)

    # Roofs

    def gable_roof(self, x0, z0, x1, z1, y, ridge="x", material="planks", over=1, gable=DAUB):
        """
        A pitched roof over the rectangle, its ridge along x or z, starting one block above the wall
        plate at y - 1 and overhanging the walls by `over`. material "planks" (oak shingles), "stone"
        (stone slates) or "thatch" (hay). Gable ends and the wall tops under the roof are filled with `gable`.
        """
        roof = {}
        if ridge == "x":
            a0, a1, b0, b1 = x0 - over, x1 + over, z0 - over, z1 + over
        else:
            a0, a1, b0, b1 = z0 - over, z1 + over, x0 - over, x1 + over
        k = 0
        while True:
            lo, hi, yy = b0 + k, b1 - k, y + k
            if lo > hi:
                break
            for a in range(a0, a1 + 1):
                if lo == hi:
                    cells = [(lo, "ridge")]
                else:
                    cells = [(lo, "lo"), (hi, "hi")]
                for b, part in cells:
                    x, z = (a, b) if ridge == "x" else (b, a)
                    if part == "ridge":
                        # The ridge: hay on thatch, a ridge beam on shingles, stone on stone slates.
                        code = {"thatch": HAY, "stone": STONE}.get(material, LOG_WE if ridge == "x" else LOG_NS)
                    elif material == "thatch":
                        code = HAY
                    else:
                        rises = ("south" if part == "lo" else "north") if ridge == "x" else ("east" if part == "lo" else "west")
                        code = stairs(material, rises)
                    self.put(x, yy, z, code)
                    roof[(x, z)] = yy
            if lo == hi:
                break
            k += 1
        # Thatch is laid thick: a second layer under each step.
        if material == "thatch":
            for (x, z), yy in list(roof.items()):
                if (x, yy - 1, z) not in self.blocks and yy - 1 >= y:
                    self.put(x, yy - 1, z, HAY)
        # Fill the walls under the roof: the gables and the wall tops between plate and roof.
        for (x, z), yy in roof.items():
            if gable and x0 <= x <= x1 and z0 <= z <= z1 and (x in (x0, x1) or z in (z0, z1)):
                for h in range(y, yy - (1 if material == "thatch" else 0)):
                    if (x, h, z) not in self.blocks:
                        self.put(x, h, z, gable)
        return roof

    def hip_roof(self, x0, z0, x1, z1, y, material="planks", over=1):
        """A roof sloping on all four sides (a pyramid on a square), for towers and hall ends."""
        k = 0
        full = {"planks": PLANKS, "stone": STONE, "thatch": HAY}[material]
        while True:
            ax0, ax1, az0, az1 = x0 - over + k, x1 + over - k, z0 - over + k, z1 + over - k
            if ax0 > ax1 or az0 > az1:
                break
            yy = y + k
            if ax0 == ax1 or az0 == az1:
                self.fill(ax0, yy, az0, ax1, yy, az1, full)
                break
            for x, z in perimeter(ax0, az0, ax1, az1):
                corner = x in (ax0, ax1) and z in (az0, az1)
                if material == "thatch" or corner:
                    code = full
                elif z == az0:
                    code = stairs(material, "south")
                elif z == az1:
                    code = stairs(material, "north")
                elif x == ax0:
                    code = stairs(material, "east")
                else:
                    code = stairs(material, "west")
                self.put(x, yy, z, code)
            k += 1

    def battlements(self, x0, z0, x1, z1, y, material=STONE):
        """A parapet round a flat roof at y, with merlons on every other block above it."""
        for x, z in perimeter(x0, z0, x1, z1):
            self.put(x, y, z, material)
            if (x + z) % 2 == 0:
                self.put(x, y + 1, z, material)

    # Getting about

    def staircase(self, x, z, rises, y0, steps, material="planks", under=PLANKS):
        """A straight stair climbing `steps` blocks towards `rises`, with head room cut above it."""
        dx, dz = SIDES[rises]
        for i in range(steps):
            px, pz, py = x + dx * i, z + dz * i, y0 + i
            self.put(px, py, pz, stairs(material, rises))
            for h in range(y0, py):
                self.put(px, h, pz, under)
            for h in (py + 1, py + 2, py + 3):
                self.rm(px, h, pz)

    def ladder(self, x, z, y0, y1):
        """A ladder from y0 to y1 against the block north of (x, z); floors above it get a hole."""
        for y in range(y0, y1 + 1):
            self.put(x, y, z, ladder())
            # Something to hang on, where a window or nothing was.
            if self.get(x, y, z - 1) in (None, DOOR_TOP):
                self.put(x, y, z - 1, PLANKS)

    # Furniture

    def bed(self, x, y, z, kind="hay", along="x"):
        """A bed: hay beds lie west (head) to east, wooden beds south (head) to north."""
        if kind == "hay":
            self.put(x, y, z, "game:bed-hay-head-east")
            self.put(x + 1, y, z, "game:bed-hay-feet-east")
        else:
            self.put(x, y, z, "game:bed-wood-head-north")
            self.put(x, y, z - 1, "game:bed-wood-feet-north")

    def lantern(self, x, y, z, facing="down"):
        """A lantern hanging from the block above ("down") or on the wall behind it, facing `facing`."""
        self.put(x, y, z, lantern_code(facing), lantern_data(x, y, z, facing))

    def banner(self, x, y, z):
        """A Heraldry : Banners pennant on the wall north of (x, y, z)."""
        self.put(x, y, z, "heraldrybanners:pole-banner-wall2-north", BANNER_DATA)
        self.items.update(BANNER_ITEMS)

    def join_fences(self):
        """Gives every fence, dry stone wall and wattle the joins its neighbours call for."""
        families = [FENCE, DRYSTONE, WATTLE]
        for (x, y, z), (code, data) in list(self.blocks.items()):
            fam = next((f for f in families if code.startswith(f.split("{")[0]) and code.endswith(f.split("}")[1])), None)
            if not fam:
                continue
            joins = ""
            for side, letter in (("north", "n"), ("east", "e"), ("south", "s"), ("west", "w")):
                dx, dz = SIDES[side]
                other = self.get(x + dx, y, z + dz) or ""
                if other.startswith(fam.split("{")[0]) or other in SOLID_FOR_FENCES:
                    joins += letter
            self.blocks[(x, y, z)] = (fam.format(joins or "empty"), data)

    def fence_line(self, cells, y=0, family=FENCE):
        for x, z in cells:
            self.put(x, y, z, family.format("empty"))


SOLID_FOR_FENCES = {LOG_UD, LOG_NS, LOG_WE, DAUB, COB, STONE, ASHLAR, PLANKS, PLANKS_NS, PLANKS_WE, PLANKS_UD}


def perimeter(x0, z0, x1, z1):
    for x in range(x0, x1 + 1):
        for z in range(z0, z1 + 1):
            if x in (x0, x1) or z in (z0, z1):
                yield x, z


def line(a, b):
    """The cells from a to b along x or z."""
    (x0, z0), (x1, z1) = a, b
    if x0 == x1:
        return [(x0, z) for z in range(min(z0, z1), max(z0, z1) + 1)]
    return [(x, z0) for x in range(min(x0, x1), max(x0, x1) + 1)]


def rect(x0, z0, x1, z1):
    return list(perimeter(x0, z0, x1, z1))


# ---- Writing and checking ----------------------------------------------------------------------

def pos_of(index):
    return index & 1023, (index >> 20) & 1023, (index >> 10) & 1023


def index_of(x, y, z):
    return (y << 20) | (z << 10) | x


def export(build):
    """The build as a WorldEdit export (dict), its smallest corner at 0, 0, 0."""
    build.join_fences()
    xs = [p[0] for p in build.blocks]
    ys = [p[1] for p in build.blocks]
    zs = [p[2] for p in build.blocks]
    mx, my, mz = min(xs), min(ys), min(zs)
    codes, number = {}, {}
    indices, ids, entities = [], [], {}
    for (x, y, z), (code, data) in sorted(build.blocks.items(), key=lambda kv: (kv[0][1], kv[0][2], kv[0][0])):
        if code not in number:
            number[code] = len(number) + 1
            codes[str(number[code])] = code
        index = index_of(x - mx, y - my, z - mz)
        indices.append(index)
        ids.append(number[code])
        if data:
            entities[str(index)] = data
    return {
        "GameVersion": "1.22.7",
        "SizeX": max(xs) - mx + 1, "SizeY": max(ys) - my + 1, "SizeZ": max(zs) - mz + 1,
        "BlockCodes": codes, "ItemCodes": dict(build.items),
        "Indices": indices, "BlockIds": ids,
        "DecorIndices": [], "DecorIds": [],
        "BlockEntities": entities, "Entities": [],
        "ReplaceMode": 1, "EntranceRotation": -1,
    }


def blocks_of(bp):
    codes = bp["BlockCodes"]
    return {pos_of(i): codes[str(b)] for i, b in zip(bp["Indices"], bp["BlockIds"])}


def hut_code(type):
    return f"createacolony:{type}" if type in ("townhall", "warehouse", "supplycamp") else HUT.format(type)


def check(bp, type, needs=(), door_needed=True):
    """The rules a blueprint must follow for the mod's builders. Returns a list of problems."""
    at = blocks_of(bp)
    problems = []
    for p, c in at.items():
        if c not in VERIFIED and c not in CONFIDENT and c != hut_code(type):
            problems.append(f"block {c} at {p} is not known to exist in the game")
    huts = [p for p, c in at.items() if c == hut_code(type)]
    if len(huts) != 1:
        problems.append(f"needs exactly one hut block, has {len(huts)}")
    doors = [p for p, c in at.items() if c.startswith("game:door-")]
    if not doors and type != "supplycamp" and door_needed:
        problems.append("no door")
    for (x, y, z) in doors:
        if at.get((x, y + 1, z)) != DOOR_TOP:
            problems.append(f"door at {(x, y, z)} has no top half above it")
        if str(index_of(x, y, z)) not in bp["BlockEntities"]:
            problems.append(f"door at {(x, y, z)} has no saved direction")
    if huts:
        hx, hy, hz = huts[0]
        if at.get((hx, hy - 1, hz)) is None:
            problems.append("nothing under the hut block")
        if at.get((hx, hy + 1, hz)) is not None:
            problems.append("the hut block is covered")
    for (x, y, z), c in at.items():
        if c.startswith("game:ladder-"):
            side = c.rsplit("-", 1)[1]
            dx, dz = SIDES[side]
            behind = at.get((x + dx, y, z + dz))
            if behind in (None, DOOR_TOP) or behind.startswith("game:ladder-"):
                problems.append(f"ladder at {(x, y, z)} has nothing to hang on {side} of it")
        if c == "game:bed-hay-head-east" and at.get((x + 1, y, z)) != "game:bed-hay-feet-east":
            problems.append(f"hay bed at {(x, y, z)} has lost its foot end")
        if c == "game:bed-wood-head-north" and at.get((x, y, z - 1)) != "game:bed-wood-feet-north":
            problems.append(f"wooden bed at {(x, y, z)} has lost its foot end")
        if c == "game:lantern-small-down" and at.get((x, y + 1, z)) is None:
            problems.append(f"hanging lantern at {(x, y, z)} has nothing above it")
        if c in ("game:lantern-small-up", "game:torch-basic-lit-up") and at.get((x, y - 1, z)) is None:
            problems.append(f"{c.split(':')[1]} at {(x, y, z)} has nothing to stand on")
        if c.startswith(("game:lantern-small-", "game:torch-basic-lit-")) and not c.endswith(("-down", "-up")):
            facing = c.rsplit("-", 1)[1]
            dx, dz = SIDES[OPPOSITE[facing]]
            if at.get((x + dx, y, z + dz)) in (None, DOOR_TOP):
                problems.append(f"{c.split(':')[1]} at {(x, y, z)} has no wall behind it")
        if c.startswith("game:door-"):
            # The door is in the wall on the side with more blocks beside it; nothing may stand in front or behind.
            def solid(dx, dz):
                return sum(1 for d in (-1, 1) for dy in (0, 1)
                           if (at.get((x + d * dx, y + dy, z + d * dz)) or DOOR_TOP) != DOOR_TOP
                           and not at[(x + d * dx, y + dy, z + d * dz)].startswith("game:door-"))
            ax, az = (0, 1) if solid(1, 0) >= solid(0, 1) else (1, 0)
            for d in (-1, 1):
                for dy in (0, 1):
                    p = (x + d * ax, y + dy, z + d * az)
                    other = at.get(p)
                    if other and not other.startswith(("game:torch", "heraldry", "game:ladder-", "game:lantern", "game:door-")) and other != DOOR_TOP:
                        problems.append(f"door at {(x, y, z)} is blocked by {other} at {p}")
    for need in needs:
        if not any(need(c) for c in at.values()):
            problems.append(f"missing a work block ({need.__doc__})")
    return problems


def relative(bp, type):
    at = blocks_of(bp)
    hx, hy, hz = next(p for p, c in at.items() if c == hut_code(type))
    return {(x - hx, y - hy, z - hz): c for (x, y, z), c in at.items()}


def kept(lower, upper):
    """How much of the level below stays where it is."""
    return sum(1 for p, c in lower.items() if upper.get(p) == c) / max(1, len(lower))


def materials(bp):
    """Roughly what builders will ask for, one item per block (door tops and the hut block left out)."""
    count = Counter(c for c in blocks_of(bp).values())
    for c in list(count):
        if c == DOOR_TOP or c.startswith("createacolony:"):
            del count[c]
    merged = Counter()
    for c, n in count.items():
        name = c.split(":")[1]
        for prefix in ("plankstairs-oak", "cobblestonestairs-shale", "stonebrickstairs-shale", "woodenfence-oak",
                       "drystonefence-shale", "wattle-sticks", "torch-basic-lit", "lantern-small", "chest", "plankslab-oak", "cobblestoneslab-shale",
                       "roughchairdining", "bed-hay", "bed-wood"):
            if name.startswith(prefix):
                name = prefix + (" (half: 2 blocks = 1 bed)" if prefix.startswith("bed-") else "")
                break
        merged[name] += n
    return merged
