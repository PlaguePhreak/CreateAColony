"""
Makes Create a Colony building blueprints (WorldEdit .json exports) from Aaron's Builder's Hut.

Run:  python3 tools/blueprints/make_blueprints.py

The blueprints go straight into the mod (assets/createacolony/config/blueprints/<type>/level<N>.json);
pass a folder to write them there instead. Pictures and plans go to tools/blueprints/previews/.

Every building here is cut from blueprints/builder/level1-old-0.18.json (Aaron's own export from Vintage
Story 1.22.7 with VS Roofing). The house is cut into slices across its length: the west gable with
the door, one ordinary middle slice, and the east gable. A longer house repeats the middle slice, so
the thatch roof pieces, the frame and the door are all copied from blocks that work in game. Only
the walls, windows and furniture are changed, and only to blocks listed in VERIFIED below.

Each building gets a picture (<type>-level<N>.png) and a layer-by-layer text plan (<type>-level<N>.txt).
A building that breaks a rule (see check) is not written.
"""
import base64
import json
import os
import sys
from collections import Counter

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", ".."))
KIT = os.path.join(REPO, "assets", "createacolony", "config", "blueprints", "builder", "level1-old-0.18.json")

# Blocks known to exist in 1.22.7: every one comes from Aaron's own exports or from blueprints
# already built in game by the mod's builders. Nothing else may be used.
VERIFIED = {
    "game:log-placed-oak-ud", "game:log-placed-oak-ns", "game:log-placed-oak-we",
    "game:packeddirt", "game:rammed-light-plain", "game:planks-oak-hor",
    "game:door-crude", "game:multiblock-monolithic-0-p1-0", "game:torch-basic-lit-west",
    "game:stonepath-free", "game:stonepathslab-free", "game:stonepathstairs-up-east-free",
    "game:firepit-cold", "game:hay-normal-ud",
    "game:wattle-sticks-n-free", "game:wattle-sticks-s-free", "game:wattle-sticks-w-free",
    "game:wattle-sticks-ne-free", "game:wattle-sticks-es-free", "game:wattle-sticks-ew-free",
    "vsroofing:roof-filled-north", "vsroofing:roof-filled-south", "vsroofing:roof-filled-east",
}
HUTS = {"builder", "forester", "farmer", "potter", "cook", "tavern"}

LOG_UD, LOG_NS, LOG_WE = "game:log-placed-oak-ud", "game:log-placed-oak-ns", "game:log-placed-oak-we"
DAUB, FLOOR, PLANKS = "game:packeddirt", "game:rammed-light-plain", "game:planks-oak-hor"
TORCH, FIREPIT, PATH = "game:torch-basic-lit-west", "game:firepit-cold", "game:stonepath-free"
DOOR, DOOR_TOP = "game:door-crude", "game:multiblock-monolithic-0-p1-0"


def pos_of(index):
    return index & 1023, (index >> 20) & 1023, (index >> 10) & 1023


def index_of(x, y, z):
    return (y << 20) | (z << 10) | x


class Kit:
    """Aaron's Builder's Hut, read as slices across its length (x)."""

    WEST = [4, 5, 6]   # overhang, gable wall with the door, first room slice
    MIDDLE = 7         # an ordinary slice: repeated to make the house longer
    EAST = [10]        # east gable wall

    def __init__(self, path):
        with open(path) as f:
            self.json = json.load(f)
        codes = self.json["BlockCodes"]
        self.blocks = {}
        for index, block_id in zip(self.json["Indices"], self.json["BlockIds"]):
            self.blocks[pos_of(index)] = codes[str(block_id)]
        self.data = {pos_of(int(k)): v for k, v in self.json["BlockEntities"].items()}

    def slice(self, x):
        """Everything in one slice above the ground layer, minus the garden in front of the house."""
        out = {}
        for (bx, y, z), code in self.blocks.items():
            if bx != x or y == 0 or code.startswith("createacolony:") or code == TORCH:
                continue
            if x <= 4 and y == 1:
                continue  # the garden fence, path and step outside
            keep = self.data.get((bx, y, z)) if (code.startswith("vsroofing:") or code == DOOR) else None
            out[(y, z)] = (code, keep)
        return out


class House:
    """A thatched cottage, `middle` slices long between the gables.

    Inside it: x from 3 to east-1, z from 3 to 7, standing height y 2 to 4 (floor at y 1).
    The door is in the west wall at (2, 2, 5). The ground outside is at y 1.
    """

    def __init__(self, kit, middle):
        self.kit = kit
        self.blocks = {}
        slices = kit.WEST + [kit.MIDDLE] * middle + kit.EAST
        for i, sx in enumerate(slices):
            for (y, z), block in kit.slice(sx).items():
                self.blocks[(i + 1, y, z)] = block
        self.east = len(slices)  # x of the east wall
        self.set(0, 1, 5, PATH)
        self.set(1, 1, 5, PATH)

    def set(self, x, y, z, code, data=None):
        self.blocks[(x, y, z)] = (code, data)

    def remove(self, x, y, z):
        self.blocks.pop((x, y, z), None)

    def code(self, x, y, z):
        b = self.blocks.get((x, y, z))
        return b[0] if b else None

    def walls(self):
        """Wall infill blocks (not posts), with the way a log laid in that wall would run."""
        for (x, y, z), (code, _) in list(self.blocks.items()):
            if code != DAUB or not 2 <= y <= 4:
                continue
            yield (x, y, z), (LOG_NS if x in (2, self.east) else LOG_WE)

    def log_walls(self):
        for (x, y, z), log in self.walls():
            self.set(x, y, z, log)

    def plank_walls(self):
        for (x, y, z), _ in self.walls():
            self.set(x, y, z, PLANKS)

    def posts(self, every):
        """Upright posts in the long walls, every few blocks."""
        for x in range(3 + every, self.east - 1, every):
            for z in (2, 8):
                for y in (2, 3, 4):
                    self.set(x, y, z, LOG_UD)

    def windows(self, xs):
        """Window holes in both long walls at head height."""
        for x in xs:
            for z in (2, 8):
                assert self.code(x, 3, z) not in (None, LOG_UD), f"no wall for a window at x {x}"
                self.remove(x, 3, z)

    def hut(self, type, x=None, z=6):
        self.set(self.east - 1 if x is None else x, 2, z, f"createacolony:hut-{type}")

    def torch(self, z):
        """A torch on the east wall, facing in."""
        self.set(self.east - 1, 4, z, TORCH)


def make(kit, type, level):
    """The building designs. Add a new one here."""
    if (type, level) == ("forester", 1):
        h = House(kit, middle=2)
        h.log_walls()
        h.windows([4])
        h.hut("forester")
        h.torch(4)
        return h
    if (type, level) == ("tavern", 1):
        h = House(kit, middle=6)
        h.posts(every=3)
        h.windows([4, 5, 7, 8])
        h.set(6, 2, 5, FIREPIT)  # the hearth in the middle of the room
        h.hut("tavern")
        h.torch(3)
        h.torch(7)
        return h
    raise SystemExit(f"No design for {type} level {level}")


DESIGNS = [("forester", 1), ("tavern", 1)]


# ---- Writing and checking -----------------------------------------------------------------

def write(kit, house, type, out_dir):
    xs = [p[0] for p in house.blocks]; ys = [p[1] for p in house.blocks]; zs = [p[2] for p in house.blocks]
    mx, my, mz = min(xs), min(ys), min(zs)
    codes = dict(kit.json["BlockCodes"])  # keep Aaron's numbers: the roof data refers to them
    number = {c: int(i) for i, c in codes.items()}
    next_id = 20001
    indices, ids, entities = [], [], {}
    for (x, y, z), (code, data) in sorted(house.blocks.items(), key=lambda kv: (kv[0][1], kv[0][2], kv[0][0])):
        if code not in number:
            number[code] = next_id
            codes[str(next_id)] = code
            next_id += 1
        index = index_of(x - mx, y - my, z - mz)
        indices.append(index)
        ids.append(number[code])
        if data:
            entities[str(index)] = data
    out = {
        "GameVersion": kit.json["GameVersion"],
        "SizeX": max(xs) - mx + 1, "SizeY": max(ys) - my + 1, "SizeZ": max(zs) - mz + 1,
        "BlockCodes": codes,  # all of Aaron's: the roof data may name any of them
        "ItemCodes": kit.json["ItemCodes"],
        "Indices": indices, "BlockIds": ids,
        "DecorIndices": [], "DecorIds": [],
        "BlockEntities": entities, "Entities": [],
        "ReplaceMode": kit.json["ReplaceMode"], "EntranceRotation": -1,
    }
    os.makedirs(os.path.join(out_dir, type), exist_ok=True)
    return out


def check(bp, type):
    """The rules a blueprint must follow for the mod's builders."""
    codes = bp["BlockCodes"]
    at = {pos_of(i): codes[str(b)] for i, b in zip(bp["Indices"], bp["BlockIds"])}
    problems = []
    for p, c in at.items():
        if c not in VERIFIED and c != f"createacolony:hut-{type}":
            problems.append(f"unverified block {c} at {p}")
        x, y, z = p
        if not (0 <= x < bp["SizeX"] and 0 <= y < bp["SizeY"] and 0 <= z < bp["SizeZ"]):
            problems.append(f"block outside the size at {p}")
    huts = [p for p, c in at.items() if c == f"createacolony:hut-{type}"]
    if len(huts) != 1:
        problems.append(f"needs exactly one hut block, has {len(huts)}")
    doors = [p for p, c in at.items() if c == DOOR]
    if not doors:
        problems.append("no door")
    for (x, y, z) in doors:
        if at.get((x, y + 1, z)) != DOOR_TOP:
            problems.append(f"door at {(x, y, z)} has no top half above it")
        if str(index_of(x, y, z)) not in bp["BlockEntities"]:
            problems.append(f"door at {(x, y, z)} has no saved direction")
    for p, c in at.items():
        if c.startswith("vsroofing:") and str(index_of(*p)) not in bp["BlockEntities"]:
            problems.append(f"roof block at {p} has no saved roof data")
    if huts:
        hx, hy, hz = huts[0]
        if at.get((hx, hy - 1, hz)) is None:
            problems.append("nothing under the hut block")
        if at.get((hx, hy + 1, hz)) is not None:
            problems.append("the hut block is covered")
    return problems


def cost(bp):
    """What builders will ask for, roughly: one item per block, roofs as in Aaron's hut."""
    codes = bp["BlockCodes"]
    count = Counter(codes[str(b)] for b in bp["BlockIds"])
    roofs = sum(n for c, n in count.items() if c.startswith("vsroofing:"))
    gables = sum(1 for d in bp["BlockEntities"].values() if b"\x0b\tmaterials" in base64.a85decode(d))
    items = Counter()
    for c, n in count.items():
        if c.startswith("vsroofing:") or c in (DOOR_TOP,) or c.startswith("createacolony:"):
            continue
        items[c.split(":")[1]] += n
    items["stick (roof)"] = roofs
    items["flax fibers or other binding (roof)"] = roofs
    items["dry grass (roof)"] = roofs * 4
    items["hay block (roof gable ends)"] = gables
    return items


COLOURS = {
    "log": (122, 88, 52), "packeddirt": (176, 150, 112), "rammed": (150, 128, 96), "planks": (170, 130, 80),
    "door": (95, 65, 40), "multiblock": (95, 65, 40), "torch": (255, 200, 60), "stonepath": (140, 140, 140),
    "firepit": (90, 80, 70), "roof": (205, 175, 90), "hut": (60, 120, 200), "wattle": (130, 100, 60),
}


def colour(code):
    for k, c in COLOURS.items():
        if k in code:
            return c
    return (200, 0, 200)


def picture(bp, path):
    """Two simple isometric pictures seen from the door side: the whole house, and without its roof."""
    from PIL import Image, ImageDraw
    codes = bp["BlockCodes"]
    at = {pos_of(i): codes[str(b)] for i, b in zip(bp["Indices"], bp["BlockIds"])}
    sx, sy, sz = bp["SizeX"], bp["SizeY"], bp["SizeZ"]
    s = 16
    w = (sx + sz) * s + 40
    h = (sx + sz) * s // 2 + sy * s + 40
    img = Image.new("RGB", (w * 2, h), (235, 240, 245))
    d = ImageDraw.Draw(img)

    def shade(c, f):
        return tuple(int(v * f) for v in c)

    def draw(blocks, left):
        ox, oy = left + 20 + sz * s, 20 + sy * s

        def pt(u, y, v):  # u grows towards the viewer's right-front, v towards the left-front
            return ox + (u - v) * s, oy + (u + v) * s // 2 - y * s

        # The viewer stands to the south-west, so the west side (u) and the south side (v) face them.
        cubes = [((sx - 1 - x), y, z, c) for (x, y, z), c in blocks.items()]
        for u, y, v, c in sorted(cubes, key=lambda t: (t[0] + t[2], t[1])):
            col = colour(c)
            top = [pt(u, y + 1, v), pt(u + 1, y + 1, v), pt(u + 1, y + 1, v + 1), pt(u, y + 1, v + 1)]
            right = [pt(u + 1, y + 1, v), pt(u + 1, y + 1, v + 1), pt(u + 1, y, v + 1), pt(u + 1, y, v)]
            front = [pt(u, y + 1, v + 1), pt(u + 1, y + 1, v + 1), pt(u + 1, y, v + 1), pt(u, y, v + 1)]
            d.polygon(top, fill=col, outline=shade(col, 0.6))
            d.polygon(right, fill=shade(col, 0.75), outline=shade(col, 0.5))
            d.polygon(front, fill=shade(col, 0.6), outline=shade(col, 0.45))

    draw(at, 0)
    roof_from = min((y for (x, y, z), c in at.items() if c.startswith("vsroofing:")), default=sy)
    draw({p: c for p, c in at.items() if p[1] < roof_from}, w)
    img.save(path)


def plan(bp):
    codes = bp["BlockCodes"]
    at = {pos_of(i): codes[str(b)] for i, b in zip(bp["Indices"], bp["BlockIds"])}
    letters = {}
    for c in sorted(set(at.values())):
        letters[c] = "abcdefghijklmnopqrstuvwxyz0123456789"[len(letters)]
    lines = ["Key:"] + [f"  {l} = {c}" for c, l in letters.items()]
    for y in range(bp["SizeY"]):
        lines.append(f"\nLayer {y} (north at the top, west on the left)")
        for z in range(bp["SizeZ"]):
            lines.append("".join(letters.get(at.get((x, y, z)), ".") for x in range(bp["SizeX"])))
    return "\n".join(lines) + "\n"


def main():
    out_dir = sys.argv[1] if len(sys.argv) > 1 else os.path.join(REPO, "assets", "createacolony", "config", "blueprints")
    previews = os.path.join(HERE, "previews")
    os.makedirs(previews, exist_ok=True)
    kit = Kit(KIT)
    failed = False
    for type, level in DESIGNS:
        bp = write(kit, make(kit, type, level), type, out_dir)
        problems = check(bp, type)
        name = f"{type}-level{level}"
        if problems:
            failed = True
            print(f"{name}: NOT written")
            for p in problems:
                print("   ", p)
            continue
        with open(os.path.join(out_dir, type, f"level{level}.json"), "w") as f:
            json.dump(bp, f, separators=(",", ":"))
        with open(os.path.join(previews, f"{name}.txt"), "w") as f:
            f.write(plan(bp))
        picture(bp, os.path.join(previews, f"{name}.png"))
        print(f"{name}: {bp['SizeX']}x{bp['SizeY']}x{bp['SizeZ']}, {len(bp['Indices'])} blocks")
        for item, n in sorted(cost(bp).items()):
            print(f"    {n:4d}  {item}")
    sys.exit(1 if failed else 0)


if __name__ == "__main__":
    main()
