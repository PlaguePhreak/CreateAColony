"""
The placeholder cross section for the Miner's Hut's mineshaft (0.62.0). The entrance, tee and straight in the
same folder are Aaron's own exports (0.63.0). Written to assets/createacolony/config/blueprints/medievaloak/mineshaft/.
Aaron's own pieces in ModConfig/createacolony/blueprints/<pack>/mineshaft/ take their place.

The pieces are solid rock with the way through left empty: the miner leaves natural rock as it is, digs out
every empty spot and builds the rest (the log posts and plank beams). Sections are 7 x 5 x 7: rock floor at
y 0, a way 3 wide and 3 high at y 1 to 3, rock ceiling at y 4. Their open sides have an empty middle on the
edge.

Run: python3 tools/blueprints/mineshaft_pieces.py
"""
import json
import os

from kit import Build, export, LOG_UD, PLANKS, torch

ROCK = "game:rock-granite"
WAYPOINT = "createacolony:waypoint-mine"
OUT = os.path.join(os.path.dirname(__file__), "..", "..", "assets", "createacolony", "config", "blueprints", "medievaloak", "mineshaft")


def section(ways):
    """A 7 x 5 x 7 block of rock with ways from the middle out to these sides (n, e, s, w), and log posts at the crossing."""
    b = Build("mineshaft", 1)
    b.fill(0, 0, 0, 6, 4, 6, ROCK)
    b.clear(2, 1, 2, 4, 3, 4)
    if "n" in ways: b.clear(2, 1, 0, 4, 3, 2)
    if "s" in ways: b.clear(2, 1, 4, 4, 3, 6)
    if "w" in ways: b.clear(0, 1, 2, 2, 3, 4)
    if "e" in ways: b.clear(4, 1, 2, 6, 3, 4)
    # Posts in the rock beside the way, and beams over it, where it is a straight way through.
    if ways in ("ns",):
        for x in (1, 5):
            b.fill(x, 1, 3, x, 3, 3, LOG_UD)
        b.fill(1, 4, 3, 5, 4, 3, PLANKS)
        b.put(2, 2, 3, torch("east"))
    elif ways in ("ew",):
        for z in (1, 5):
            b.fill(3, 1, z, 3, 3, z, LOG_UD)
        b.fill(3, 4, 1, 3, 4, 5, PLANKS)
        b.put(3, 2, 2, torch("south"))
    else:
        # A crossing: posts at its corners where the rock is left.
        for x, z in ((1, 1), (5, 1), (1, 5), (5, 5)):
            if b.get(x, 1, z) == ROCK:
                b.fill(x, 1, z, x, 3, z, LOG_UD)
    return b


def main():
    os.makedirs(OUT, exist_ok=True)
    # Aaron's own entrance, tee and straight ship beside it (0.63.0); the cross stays a placeholder until he makes one.
    pieces = {"cross": section("nesw")}
    for name, build in pieces.items():
        with open(os.path.join(OUT, name + ".json"), "w") as f:
            json.dump(export(build), f, separators=(",", ":"))
        print(name, len(build.blocks), "blocks")


if __name__ == "__main__":
    main()
