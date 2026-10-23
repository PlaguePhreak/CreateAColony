"""
Copies Aaron's WorldEdit exports into the Dev's Builds asset pack, after checking them.

Run:  python3 tools/blueprints/import_devsbuilds.py <folder with the exports>

Files are matched by name (TownHallLevel1.json, BuildersHutLevel3.json, GuardTowerLevel5.json,
CooksHutLevel2.json, HouseLevel1Small.json, ...; anything before the name, like an upload id, is ignored) and
written to assets/createacolony/config/blueprints/devsbuilds/<type>/level<N>.json unchanged, apart
from a missing hut block (see HUT_SPOTS). A word after the level number names a variant, one of
several buildings of the same type that players pick between on the wand (HouseLevel1Small.json goes
to residence/small/level1.json).

Checks, each printed: exactly one hut block of the right type, every door has its saved direction and
its top half, every VS Roofing block has its roof data, the hut block stands on something and is not
covered (a lantern or torch on top is fine). Each level is also compared with the level below it, relative to the hut block, so a level
that sits a few blocks off (the offset bug of the first Town Hall 4 export) shows up as a better match
at a shift other than 0. A file with problems is not written.
"""
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, "..", "..", "assets", "createacolony", "config", "blueprints", "devsbuilds"))

NAMES = {"townhall": "townhall", "buildershut": "builder", "guardtower": "guardtower", "chef": "cook",
         "cook": "cook", "cookshut": "cook", "home01": "residence", "residence": "residence", "house": "residence"}

# Exports saved without their hut block get one here: (type, level) -> spot inside the export.
HUT_SPOTS = {}  # e.g. {("cook", "", 1): (3, 1, 7)}: inside, in the corner beside the doorway


def hut_code(type):
    return "createacolony:townhall" if type == "townhall" else f"createacolony:hut-{type}"


def pos_of(index):
    return index & 1023, (index >> 20) & 1023, (index >> 10) & 1023


def index_of(x, y, z):
    return (y << 20) | (z << 10) | x


def blocks(bp):
    codes = bp["BlockCodes"]
    return {pos_of(i): codes[str(b)] for i, b in zip(bp["Indices"], bp["BlockIds"]) if codes[str(b)] != "game:air"}


def add_hut(bp, type, spot):
    codes = bp["BlockCodes"]
    code = hut_code(type)
    ids = [int(k) for k in codes]
    new_id = next((int(k) for k, c in codes.items() if c == code), max(ids) + 1)
    codes[str(new_id)] = code
    index = index_of(*spot)
    assert index not in bp["Indices"], f"something is already at {spot}"
    bp["Indices"].append(index)
    bp["BlockIds"].append(new_id)


def check(bp, type):
    at = blocks(bp)
    problems = []
    huts = [p for p, c in at.items() if c == hut_code(type)]
    others = [c for c in set(at.values()) if c.startswith("createacolony:") and c != hut_code(type)]
    if len(huts) != 1:
        problems.append(f"needs exactly one {hut_code(type)}, has {len(huts)}")
    if others:
        problems.append(f"other colony blocks in it: {others}")
    entities = {pos_of(int(k)) for k in bp["BlockEntities"]}
    for (x, y, z), c in at.items():
        if c.startswith("game:door-") and "trapdoor" not in c:
            if (x, y, z) not in entities:
                problems.append(f"door at {(x, y, z)} has no saved direction")
            if not at.get((x, y + 1, z), "").startswith("game:multiblock-"):
                problems.append(f"door at {(x, y, z)} has no top half above it")
        if c.startswith("vsroofing:") and (x, y, z) not in entities:
            problems.append(f"roof block at {(x, y, z)} has no saved roof data")
    if len(huts) == 1:
        hx, hy, hz = huts[0]
        if (hx, hy - 1, hz) not in at:
            problems.append("nothing under the hut block")
        above = at.get((hx, hy + 1, hz), "")
        # A lantern or torch standing on it is fine; anything else would hide it.
        if above and not any(k in above for k in ("lantern", "torch")):
            problems.append(f"the hut block is covered by {above}")
    return problems


def relative(bp, type):
    at = blocks(bp)
    hx, hy, hz = next(p for p, c in at.items() if c == hut_code(type))
    return {(x - hx, y - hy, z - hz): c for (x, y, z), c in at.items()}


def match(lower, upper, shift):
    dx, dy, dz = shift
    return sum(1 for (x, y, z), c in lower.items() if upper.get((x + dx, y + dy, z + dz)) == c) / max(1, len(lower))


def main():
    src = sys.argv[1]
    found = {}
    for name in sorted(os.listdir(src)):
        m = re.search(r"([A-Za-z0-9]+?)Level(\d+)([A-Za-z0-9]*)\.json$", name)
        if not m or m.group(1).lower() not in NAMES:
            continue
        found[(NAMES[m.group(1).lower()], m.group(3).lower(), int(m.group(2)))] = os.path.join(src, name)
    failed = False
    previous = {}
    for (type, variant, level), path in sorted(found.items()):
        with open(path) as f:
            bp = json.load(f)
        if (type, variant, level) in HUT_SPOTS:
            add_hut(bp, type, HUT_SPOTS[(type, variant, level)])
        problems = check(bp, type)
        label = f"{type}{' ' + variant if variant else ''} level {level} ({os.path.basename(path)})"
        if problems:
            failed = True
            print(f"{label}: NOT written")
            for p in problems:
                print("   ", p)
            continue
        rel = relative(bp, type)
        line = f"{label}: {bp['SizeX']}x{bp['SizeY']}x{bp['SizeZ']}, {len(rel)} blocks"
        if (type, variant, level - 1) in previous:
            lower = previous[(type, variant, level - 1)]
            at0 = match(lower, rel, (0, 0, 0))
            best = max((match(lower, rel, (x, y, z)), (x, y, z)) for x in range(-6, 7) for y in range(-3, 4) for z in range(-6, 7))
            line += f"; keeps {at0:.0%} of level {level - 1} in place"
            if best[0] > at0 + 0.05:
                line += f" (WARNING: {best[0]:.0%} if moved by {best[1]}, check the hut block)"
        print(line)
        previous[(type, variant, level)] = rel
        folder = os.path.join(OUT, type, variant) if variant else os.path.join(OUT, type)
        os.makedirs(folder, exist_ok=True)
        with open(os.path.join(folder, f"level{level}.json"), "w") as f:
            json.dump(bp, f, separators=(",", ":"))
    sys.exit(1 if failed else 0)


if __name__ == "__main__":
    main()
