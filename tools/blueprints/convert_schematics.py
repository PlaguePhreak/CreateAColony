"""
Turns Minecraft schematics (WorldEdit .schem, Litematica .litematic) into Vintage Story WorldEdit exports.

Run:  python3 tools/blueprints/convert_schematics.py <out folder> <file> [<file> ...]

Each file becomes <out folder>/<name>.json, for /we import, and <name>.png, a picture of it from the
south-west: whole, and cut away a few blocks above the floor so the inside shows. Blocks are mapped like
convert_minecolonies.py does (only block codes seen in Aaron's own exports): oak for every wood, shale for
every stone, light mud brick for pale bricks and plaster. Glass, iron bars, plants, redstone and other
things with nothing close in the game are left out.
"""
import json
import os
import sys
from collections import Counter

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import convert_minecolonies as mc  # noqa: E402
import kit  # noqa: E402
import render  # noqa: E402
import schematic_readers  # noqa: E402
from PIL import Image, ImageDraw, ImageFont  # noqa: E402

# Vanilla blocks the MineColonies converter never met: left out, as they are light, snow, or tiny bits.
DROP = {"light", "barrier", "snow", "snow_block", "glow_lichen", "pink_petals", "amethyst_cluster",
        "small_amethyst_bud", "medium_amethyst_bud", "large_amethyst_bud", "pointed_dripstone", "nether_wart",
        "end_gateway", "end_portal", "nether_portal", "dragon_egg", "observer", "white_banner", "black_banner",
        "slime_block", "honey_block", "frogspawn", "sculk_vein", "moving_piston", "spore_blossom"}

# Blocks builders use as walls and roofs that the converter would leave out as plants.
BUILT_WITH = {"brown_mushroom_block": "minecraft:spruce_planks", "red_mushroom_block": "minecraft:spruce_planks",
              "mushroom_stem": "minecraft:white_terracotta"}


def convert(blocks):
    """Minecraft names the converter would get wrong, set right before it sees them."""
    out = {}
    for pos, (name, props, te) in blocks.items():
        n = name.split(":", 1)[1]
        if n in DROP or n.endswith(("_banner",)):
            continue
        if n.endswith("_lantern") and n != "sea_lantern":  # copper lanterns
            name = "minecraft:lantern"
        name = BUILT_WITH.get(n, name)
        out[pos] = (name, props, te)
    return out


FULL_CUBE = 1.0


def visible(blocks):
    """Only the blocks something could see: a full block with full blocks on all six sides is hidden."""
    solid = {p for p, c in blocks.items() if render.height(c) == FULL_CUBE and "door" not in c}
    out = {}
    for (x, y, z), c in blocks.items():
        if (x, y, z) in solid and all(q in solid for q in ((x + 1, y, z), (x - 1, y, z), (x, y + 1, z),
                                                          (x, y - 1, z), (x, y, z + 1), (x, y, z - 1))):
            continue
        out[(x, y, z)] = c
    return out


def floor_of(blocks):
    """The top of the solid base: the highest layer near the bottom that is still mostly filled."""
    counts = Counter(y for _, y, _ in blocks)
    low = min(counts)
    full = max(counts.get(y, 0) for y in range(low, low + 12))
    y = low
    while y < low + 12 and counts.get(y + 1, 0) >= 0.6 * full:
        y += 1
    return y


def picture(blocks, title, path, cut):
    x0, x1, y0, y1, z0, z1 = render.bounds(blocks)
    span = (x1 - x0) + (z1 - z0)
    s = max(2, min(12, int(2400 / (span * 0.866))))
    whole = visible(blocks)
    low = visible({p: c for p, c in blocks.items() if p[1] <= cut})
    w, h = render.picture_size(blocks, s)
    pad = 20
    img = Image.new("RGB", (int(w) * 2 + pad * 3, int(h) + pad * 2 + 40), (238, 242, 246))
    d = ImageDraw.Draw(img)
    try:
        font = ImageFont.truetype("DejaVuSans-Bold.ttf", 18)
    except OSError:
        font = ImageFont.load_default()
    d.text((pad, 8), title, fill=(30, 30, 30), font=font)
    oy = 40 + pad + (x1 - z0) * s * 0.5 + y1 * s
    for i, part in enumerate((whole, low)):
        ox = pad + i * (int(w) + pad) - (x0 + z0) * s * 0.866
        render.draw(img, part, ox, oy, s)
    d.text((int(w) + pad * 2, 30), f"Cut away above floor + {cut - floor_of(blocks)}", fill=(90, 90, 90), font=font)
    img.save(path)


def main():
    out, files = sys.argv[1], sys.argv[2:]
    os.makedirs(out, exist_ok=True)
    for f in files:
        name = os.path.splitext(os.path.basename(f))[0]
        raw = schematic_readers.read_any(f)
        b, _, notes = mc.build(f, None, blocks=convert(raw))
        mc.tidy(b)
        bp = kit.export(b)
        problems = [c for c in bp["BlockCodes"].values() if c not in kit.VERIFIED and c not in kit.CONFIDENT]
        with open(os.path.join(out, name + ".json"), "w") as fh:
            json.dump(bp, fh, separators=(",", ":"))
        at = kit.blocks_of(bp)
        cut = floor_of(at) + 3
        picture(at, f"{name}  ({bp['SizeX']} x {bp['SizeY']} x {bp['SizeZ']}, {len(at)} blocks)",
                os.path.join(out, name + ".png"), cut)
        print(f"{name}: {len(raw)} Minecraft blocks -> {len(at)} Vintage Story blocks, "
              f"{bp['SizeX']}x{bp['SizeY']}x{bp['SizeZ']}")
        for n in notes:
            print("  " + n)
        if problems:
            print("  NOT VERIFIED: " + ", ".join(sorted(set(problems))))
        print("  materials: " + ", ".join(f"{c.split(':')[1]} {n}" for c, n in Counter(at.values()).most_common(10)))


if __name__ == "__main__":
    main()
