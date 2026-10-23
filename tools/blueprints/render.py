"""
Simple pictures of blueprints: an isometric view from the south-west (the front door side), and the
same with the roof and upper floors cut away, so the rooms show.
"""
from PIL import Image, ImageDraw, ImageFont

COLOURS = [
    ("createacolony:waypoint", (20, 20, 20)), ("createacolony:", (60, 120, 220)),
    ("door", (92, 60, 34)), ("lantern", (255, 190, 80)), ("multiblock", (92, 60, 34)), ("torch", (255, 200, 60)),
    ("log-placed", (106, 76, 44)), ("debarkedlog", (170, 130, 85)), ("plankstairs", (150, 104, 60)), ("plankslab", (176, 128, 78)),
    ("planks", (186, 140, 86)), ("packeddirt", (222, 206, 172)), ("mudbrick", (196, 170, 128)),
    ("rammed", (150, 128, 96)), ("stonebrick", (150, 150, 155)), ("cobblestonestairs", (112, 112, 118)),
    ("cobblestoneslab", (120, 120, 126)), ("cobblestone", (128, 128, 134)), ("rockpolished", (160, 160, 165)),
    ("stonepath", (140, 138, 130)), ("hay", (214, 182, 92)), ("firepit", (70, 50, 40)), ("clayoven", (170, 90, 60)),
    ("barrel", (120, 80, 45)), ("storagevessel", (170, 70, 50)), ("basket", (190, 160, 100)), ("bookshelf", (120, 70, 40)),
    ("chest", (160, 110, 50)), ("bed-hay", (220, 200, 120)), ("bed-wood", (180, 40, 40)), ("ladder", (140, 100, 60)),
    ("woodenfence", (130, 95, 55)), ("drystonefence", (125, 125, 120)), ("wattle", (150, 115, 70)),
    ("w4rd0sfurniture", (120, 80, 50)), ("heraldry", (200, 30, 30)), ("soil", (90, 140, 60)),
]


def colour(code):
    for key, c in COLOURS:
        if key in code:
            return c
    return (220, 0, 220)


def height(code):
    """How tall a block looks: stairs and slabs half, fences and small things less."""
    if "stairs" in code or "slab" in code or "bed-" in code:
        return 0.5
    if any(k in code for k in ("torch", "fence", "wattle", "w4rd0s", "ladder", "heraldry")):
        return 0.6
    return 1.0


def shade(c, f):
    return tuple(max(0, min(255, int(v * f))) for v in c)


def draw(img, blocks, ox, oy, s):
    d = ImageDraw.Draw(img)
    a, b, c = s * 0.866, s * 0.5, s

    def pt(x, y, z):
        return ox + (x + z) * a, oy + (z - x) * b - y * c

    def cube(x0, x1, y0, y1, z0, z1, col):
        top = [pt(x0, y1, z0), pt(x1, y1, z0), pt(x1, y1, z1), pt(x0, y1, z1)]
        south = [pt(x0, y1, z1), pt(x1, y1, z1), pt(x1, y0, z1), pt(x0, y0, z1)]
        west = [pt(x0, y1, z0), pt(x0, y1, z1), pt(x0, y0, z1), pt(x0, y0, z0)]
        d.polygon(west, fill=shade(col, 0.62), outline=shade(col, 0.45))
        d.polygon(south, fill=shade(col, 0.8), outline=shade(col, 0.55))
        d.polygon(top, fill=col, outline=shade(col, 0.7))

    for (x, y, z), code in sorted(blocks.items(), key=lambda kv: (-kv[0][0] + kv[0][2] + kv[0][1], kv[0][1])):
        col = colour(code)
        if "stairs" in code:
            # The low step and the high back, on the side it climbs towards.
            upside = "-down-" in code
            rises = next(s for s in ("north", "east", "south", "west") if f"-{s}-" in code)
            low, high = (y + 0.5, y + 1) if upside else (y, y + 0.5)
            cube(x, x + 1, low, high, z, z + 1, col)
            hx0, hx1, hz0, hz1 = x, x + 1, z, z + 1
            if rises == "north": hz1 = z + 0.5
            if rises == "south": hz0 = z + 0.5
            if rises == "east": hx0 = x + 0.5
            if rises == "west": hx1 = x + 0.5
            # Draw the back half in the right order: a back half nearer the viewer goes last anyway.
            cube(hx0, hx1, y, y + 1, hz0, hz1, col)
            continue
        h = height(code)
        if "slab" in code and "-up-" in code:
            cube(x, x + 1, y + 0.5, y + 1, z, z + 1, col)
            continue
        if h < 0.9 and "stairs" not in code and "slab" not in code and "bed-" not in code:
            cube(x + 0.3, x + 0.7, y, y + h, z + 0.3, z + 0.7, col)
            continue
        cube(x, x + 1, y, y + h, z, z + 1, col)


def bounds(blocks):
    xs = [p[0] for p in blocks]; ys = [p[1] for p in blocks]; zs = [p[2] for p in blocks]
    return min(xs), max(xs) + 1, min(ys), max(ys) + 1, min(zs), max(zs) + 1


def picture_size(blocks, s):
    x0, x1, y0, y1, z0, z1 = bounds(blocks)
    w = ((x1 - x0) + (z1 - z0)) * s * 0.866
    h = ((x1 - x0) + (z1 - z0)) * s * 0.5 + (y1 - y0) * s
    return w, h


def sheet(levels, title, path, cut=2):
    """levels: list of block dicts (relative to the hut). One column per level: whole, then cut away above `cut`."""
    s = 12
    sizes = [picture_size(b, s) for b in levels]
    col_w = int(max(w for w, _ in sizes)) + 30
    row_h = int(max(h for _, h in sizes)) + 30
    img = Image.new("RGB", (col_w * len(levels), row_h * 2 + 40), (238, 242, 246))
    d = ImageDraw.Draw(img)
    try:
        font = ImageFont.truetype("DejaVuSans-Bold.ttf", 16)
    except OSError:
        font = ImageFont.load_default()
    d.text((10, 8), title, fill=(30, 30, 30), font=font)
    gx0 = min(bounds(b)[0] for b in levels); gz0 = min(bounds(b)[4] for b in levels)
    gx1 = max(bounds(b)[1] for b in levels); gy1 = max(bounds(b)[3] for b in levels)
    for i, blocks in enumerate(levels):
        ox = i * col_w + 15 - (gx0 + gz0) * s * 0.866
        # Same ground line for every level, so sizes compare.
        oy_top = 40 + 15 + (gx1 - gz0) * s * 0.5 + gy1 * s
        draw(img, blocks, ox, oy_top, s)
        d.text((i * col_w + 15, 30), f"Level {i + 1}", fill=(30, 30, 30), font=font)
        low = {p: c for p, c in blocks.items() if p[1] < cut}
        draw(img, low, ox, oy_top + row_h, s)
    d.text((10, row_h + 30), f"Cut away above floor level + {cut}", fill=(90, 90, 90), font=font)
    img.save(path)
