"""Small building moves used by the level recipes, on top of kit2.Build."""
import sys, os, json, base64, io
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import kit2
from kit2 import Build, CATALOG

D = {'east': (1, 0, 0), 'west': (-1, 0, 0), 'north': (0, 0, -1), 'south': (0, 0, 1), 'up': (0, 1, 0), 'down': (0, -1, 0)}
OPP = {'east': 'west', 'west': 'east', 'north': 'south', 'south': 'north', 'up': 'down', 'down': 'up'}
AIRY = ('multiblock', 'fence', 'torch', 'lantern', 'ladder', 'tallgrass', 'flower', 'wattle-sticks')

# wall materials up the ladder, by level
WALL = {1: 'game:packeddirt', 2: 'game:rammed-light-plain', 3: 'game:mudbrick-light', 4: 'game:cobblestone-shale', 5: 'game:stonebricks-shale'}


def add(p, d, n=1):
    dx, dy, dz = D[d]
    return (p[0] + dx * n, p[1] + dy * n, p[2] + dz * n)


def free(b, p):
    return b.get(p) == 'game:air'


def solid(b, p):
    c = b.get(p)
    return c != 'game:air' and not any(k in c for k in AIRY)


def standable(b, p):
    """Empty with something solid under it: where furniture can go."""
    return free(b, p) and solid(b, add(p, 'down'))


def first_free(b, cells, need=((0, 0, 0),)):
    for p in cells:
        if all(standable(b, (p[0] + o[0], p[1] + o[1], p[2] + o[2])) for o in need): return p
    return None


def lantern(b, p, facing=None):
    """A small lantern at p, hung on the wall beside it (facing = the side the wall is NOT on), or
    under the block above when nothing is beside it."""
    if facing is None:
        for d in ('south', 'north', 'east', 'west'):
            if solid(b, add(p, OPP[d])): facing = d; break
        else: facing = 'down' if solid(b, add(p, 'up')) else None
    if facing is None or not free(b, p): return False
    b.place(f'game:lantern-small-{facing}', p)
    return True


def lanterns_for_torches(b):
    """Every torch becomes a lantern the same way round; a burned-out one too."""
    b.swap_codes({'game:torch-basic-lit': 'game:lantern-small', 'game:torch-basic-burnedout': 'game:lantern-small'})
    for p, c in list(b.blocks.items()):
        if c.startswith('game:lantern-small') and p not in b.ents: b.ents[p] = CATALOG[c][0]


def bed(b, p, kind='rough', facing=None):
    """A bed with its head at p. kind: 'rough' (roughbed), 'rustic' (rusticbedsingle), 'hay', 'wood'."""
    codes = {'rough': ['w4rd0sfurniture:roughbed-north', 'w4rd0sfurniture:roughbed-south', 'w4rd0sfurniture:roughbed-east'],
             'rustic': ['w4rd0sfurniture:rusticbedsingle-east', 'w4rd0sfurniture:rusticbedsingle-west'],
             'hay': ['game:bed-hay-head-south', 'game:bed-hay-head-east'],
             'wood': ['game:bed-wood-head-east', 'game:bed-wood-head-north']}[kind]
    for code in codes:
        f = code.split('-')[-1]
        if facing and f != facing: continue
        if kind in ('hay', 'wood'):
            q = add(p, f)
            if standable(b, p) and standable(b, q):
                b.place(code, p); b.place(code.replace('head', 'feet'), q); return True
        else:
            parts = CATALOG[code][1]
            cells = [p] + [(p[0] + o[0], p[1] + o[1], p[2] + o[2]) for o, _ in parts]
            if all(standable(b, c) for c in cells):
                b.place(code, p); return True
    return False


def put(b, code, cells):
    """The first of the cells where the fitting (with its parts) fits. Returns the spot or None."""
    ent, parts = CATALOG.get(code, (None, []))
    for p in cells:
        need = [p] + [(p[0] + o[0], p[1] + o[1], p[2] + o[2]) for o, _ in parts]
        if all(standable(b, c) for c in need):
            b.place(code, p); return p
    return None


def box_cells(box, y=None):
    x0, y0, z0, x1, y1, z1 = box
    return [(x, yy, z) for yy in ([y] if y is not None else range(y0, y1 + 1)) for z in range(z0, z1 + 1) for x in range(x0, x1 + 1)]


def bbox(b, part, box=None):
    ps = [p for p, c in b.blocks.items() if part in c and (box is None or kit2.inside(p, box))]
    xs = [p[0] for p in ps]; ys = [p[1] for p in ps]; zs = [p[2] for p in ps]
    return min(xs), min(ys), min(zs), max(xs), max(ys), max(zs)


def perimeter(b, box, y, code, skip=('door', 'multiblock', 'log-placed', 'createacolony')):
    """Sets the outer ring of the box at height y to code, except posts, doors and the hut block."""
    x0, _, z0, x1, _, z1 = box
    for x in range(x0, x1 + 1):
        for z in range(z0, z1 + 1):
            if x in (x0, x1) or z in (z0, z1):
                c = b.get((x, y, z))
                if any(k in c for k in skip): continue
                b.set((x, y, z), code)


def fix_doors(b, wall):
    """After a layer with doors was repeated: a door standing on a door becomes wall, every door gets its
    top half right above it and nothing else above that."""
    for p, c in sorted(b.blocks.items(), key=lambda kv: kv[0][1]):
        if c.startswith('game:door-') and 'trapdoor' not in c:
            below = b.get(add(p, 'down'))
            if below.startswith('game:door-') or below.startswith('game:multiblock'):
                b.set(p, wall)
    for p, c in list(b.blocks.items()):
        if c == 'game:multiblock-monolithic-0-p1-0':
            m = add(p, 'down')
            if not b.get(m).startswith('game:door-') or 'trapdoor' in b.get(m): b.set(p, wall)
    for p, c in list(b.blocks.items()):
        if c.startswith('game:door-') and 'trapdoor' not in c:
            b.set(add(p, 'up'), 'game:multiblock-monolithic-0-p1-0')
            # the wall above the door, where repeating the layer left a gap
            q = add(p, 'up', 2)
            while free(b, q) and q[1] < p[1] + 6 and any(solid(b, add(q, d)) for d in ('north', 'south', 'east', 'west')):
                b.set(q, wall); q = add(q, 'up')


def cellar(b, box, depth, wall, floor, ladder_at=None, stairs=None):
    """A cellar under the floor layer: box = (x0, z0, x1, z1) outer walls, from floor_y - 1 down `depth`
    layers of room, then the floor. ladder_at = (x, z, facing) puts an oak ladder from the cellar floor to the
    room above, through a hole in the floor. Everything there is dug out first."""
    x0, z0, x1, z1 = box
    fy = b.hut()[1] - 1   # the building's floor layer
    top = fy - 1; bottom = fy - depth
    b.clear((x0, bottom - 1, z0, x1, top, z1))
    for y in range(bottom, top + 1):
        b.walls((x0, y, z0, x1, y, z1), wall)
    b.fill((x0, bottom - 1, z0, x1, bottom - 1, z1), floor)
    if ladder_at:
        x, z, facing = ladder_at
        for y in range(bottom, fy + 1): b.set((x, y, z), f'game:ladder-wood-oak-{facing}')
    if stairs:
        # a straight run: (x, z, direction) from the floor hole, one step down per block
        x, z, d = stairs
        dx, _, dz = D[d]
        for k in range(depth + 1):
            p = (x + dx * k, fy - k, z + dz * k)
            if k == 0: b.set(p, None)
            else: b.set(p, f'game:cobblestonestairs-shale-up-{OPP[d]}-free' if 'cobble' in wall else f'game:stonebrickstairs-shale-up-{OPP[d]}-free')
            for h in range(1, 3): b.set((p[0], p[1] + h, p[2]), None) if p[1] + h < fy else None
        for k in range(1, depth + 1): b.set((x + dx * k, fy, z + dz * k), None)


def ground(b, box, y, code='game:soil-medium-normal'):
    b.ground(box, y, code)


def fence_ring(b, box, y, kind='woodenfence-oak', gap=None):
    """A fence round the box edge at height y with the right joins; gap = (x, z) left open."""
    x0, z0, x1, z1 = box
    ring = {(x, z) for x in range(x0, x1 + 1) for z in range(z0, z1 + 1) if x in (x0, x1) or z in (z0, z1)}
    if gap: ring.discard(gap)
    for (x, z) in ring:
        if not free(b, (x, y, z)) or not solid(b, (x, y - 1, z)): continue
        joins = ''.join(d[0] for d in ('north', 'east', 'south', 'west') if (x + D[d][0], z + D[d][2]) in ring)
        order = 'nesw'
        joins = ''.join(c for c in order if c in joins) or 'empty'
        b.set((x, y, z), f'game:{kind}-{joins}-free')


def path(b, cells, y, code='game:stonepath-free'):
    for (x, z) in cells:
        if solid(b, (x, y, z)) and free(b, (x, y + 1, z)): b.set((x, y, z), code)


def remove_hut(b, replacement='game:planks-oak-ud'):
    for p, c in list(b.blocks.items()):
        if c.startswith('createacolony:'): b.set(p, replacement)


def set_hut(b, type):
    """Swaps the hut block for another type's (keeps its spot)."""
    p = b.hut()
    b.set(p, kit2.hut_code_of(type), None)


# ---- previews ----
import render
render.COLOURS.insert(0, ("roof", (150, 60, 45)))
render.COLOURS += [("rock-", (140, 130, 120)), ("daub", (215, 195, 150)), ("farmland", (100, 70, 40)), ("tallgrass", (90, 150, 60)), ("flower", (90, 150, 60)), ("supportbeam", (106, 76, 44)), ("stonebricks", (120, 125, 135)), ("stonebrickstairs", (120, 125, 135))]
from PIL import Image


def render_build(b, s=9):
    hut = b.hut()
    bl = {(x - hut[0], y - hut[1], z - hut[2]): c for (x, y, z), c in b.blocks.items()
          if 'multiblock' not in c and (not c.startswith('game:soil') and 'claystone' not in c or y >= hut[1])}
    w, h = render.picture_size(bl, s)
    img = Image.new('RGBA', (int(w) + 20, int(h) + 20), (238, 242, 246, 255))
    x0, x1, y0, y1, z0, z1 = render.bounds(bl)
    ox = 10 - (x0 + z0) * s * 0.866; oy = 10 + (x1 - z0) * s * 0.5 + y1 * s
    render.draw(img, bl, ox, oy, s)
    return img.crop(img.getbbox())


def sheet(builds, titles, path):
    imgs = [render_build(b) for b in builds]
    from PIL import ImageDraw, ImageFont
    try: font = ImageFont.truetype("DejaVuSans-Bold.ttf", 14)
    except OSError: font = ImageFont.load_default()
    w = sum(i.width + 20 for i in imgs) + 20; h = max(i.height for i in imgs) + 60
    out = Image.new('RGB', (w, h), (238, 242, 246)); d = ImageDraw.Draw(out)
    x = 20
    for i, t in zip(imgs, titles):
        out.paste(i, (x, h - 20 - i.height)); d.text((x, 10), t, fill=(30, 30, 30), font=font); x += i.width + 20
    out.save(path)
