"""
Makes the generated asset packs, 12th Century Inspired (designs.py) and KCD2 Inspired (designs_kcd2.py):
levels 1 to 5 of every hut, and the Supply Camp.

Run:  python3 tools/blueprints/make_packs.py [pack ...] [type ...]
      (no pack: both; no type: every building)

The blueprints go to assets/createacolony/config/blueprints/<pack>/<type>/level<N>.json.
A picture of each building's five levels goes to tools/blueprints/previews/<pack>/<type>.png,
and a list of what each level needs (one item per block, roughly) to .../<type>.txt.

A level that breaks a rule (see kit.check) is not written and the run fails. Each level is also
compared with the one below it: how much of the lower level stays as it is.
"""
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import designs  # noqa: E402
import designs_kcd2  # noqa: E402
import kit  # noqa: E402
import render  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", ".."))
BLUEPRINTS = os.path.join(REPO, "assets", "createacolony", "config", "blueprints")

# pack folder: (name shown on the pictures, designs module)
PACKS = {"twelfthcentury": ("12th Century Inspired", designs), "kcd2": ("KCD2 Inspired", designs_kcd2)}

NAMES = {"townhall": "Town Hall", "builder": "Builder's Hut", "farmer": "Farmer's Hut", "forester": "Forester's Hut",
         "cook": "Cook's Hut", "potter": "Potter's Hut", "residence": "Residence", "guardtower": "Guard Tower",
         "tavern": "Tavern", "warehouse": "Warehouse", "prospector": "Prospector's Hut", "miner": "Miner's Hut",
         "quarry": "Quarry", "supplycamp": "Supply Camp"}


def has(prefix, what):
    def test(code):
        return code.startswith(prefix)
    test.__doc__ = what
    return test


BED = has("game:bed-", "a bed")
NEEDS = {
    "cook": [has(kit.FIREPIT, "a fire pit")],
    "residence": [BED],
    "guardtower": [BED, has("game:chest-", "a chest for weapons")],
    "warehouse": [has("game:chest-", "chests")],
    "miner": [has("createacolony:waypoint-mine", "the Mine waypoint")],
    "quarry": [has("createacolony:waypoint-quarry", "the Quarry waypoint")],
    "tavern": [has(kit.FIREPIT, "a hearth")],
}


def main():
    packs = [a for a in sys.argv[1:] if a in PACKS] or list(PACKS)
    only = {a for a in sys.argv[1:] if a not in PACKS}
    failed = False
    for pack in packs:
        failed |= make(pack, only)
    sys.exit(1 if failed else 0)


def make(pack, only):
    title, module = PACKS[pack]
    out, previews = os.path.join(BLUEPRINTS, pack), os.path.join(HERE, "previews", pack)
    os.makedirs(previews, exist_ok=True)
    failed = False
    jobs = [(t, f, range(1, 6)) for t, f in module.DESIGNS.items()] + [("supplycamp", module.supplycamp, range(1, 2))]
    for type, design, levels in jobs:
        if only and type not in only:
            continue
        pictures, report, previous = [], [], None
        for level in levels:
            build = design(kit.Build(type, level))
            bp = kit.export(build)
            problems = kit.check(bp, type, NEEDS.get(type, ()))
            name = f"{pack} {type} level {level}"
            if problems:
                failed = True
                print(f"{name}: NOT written")
                for p in problems:
                    print("   ", p)
                continue
            rel = kit.relative(bp, type)
            line = f"{name}: {bp['SizeX']}x{bp['SizeY']}x{bp['SizeZ']}, {len(rel)} blocks"
            if previous is not None:
                line += f", keeps {kit.kept(previous, rel):.0%} of level {level - 1}"
            beds = sum(1 for c in rel.values() if c.startswith("game:bed-") and "-head-" in c)
            chests = sum(1 for c in rel.values() if c.startswith("game:chest-"))
            line += f", {beds} beds, {chests} chests"
            print(line)
            previous = rel
            os.makedirs(os.path.join(out, type), exist_ok=True)
            with open(os.path.join(out, type, f"level{level}.json"), "w") as f:
                json.dump(bp, f, separators=(",", ":"))
            pictures.append(rel)
            report.append(f"Level {level}: {bp['SizeX']} x {bp['SizeZ']} blocks, {bp['SizeY']} high. {beds} beds, {chests} chests.")
            for item, n in sorted(kit.materials(bp).items(), key=lambda kv: -kv[1]):
                report.append(f"    {n:5d}  {item}")
            report.append("")
        if pictures:
            render.sheet(pictures, f"{NAMES[type]} - {title}", os.path.join(previews, f"{type}.png"))
            with open(os.path.join(previews, f"{type}.txt"), "w") as f:
                f.write(f"{NAMES[type]} ({title}): what each level needs, one item per block\n\n" + "\n".join(report))
    return failed


if __name__ == "__main__":
    main()
