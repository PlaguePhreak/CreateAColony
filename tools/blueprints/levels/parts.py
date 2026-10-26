"""Composite pieces built from the Design kit: a framed house with storeys and a roof, a lean-to, a
porch, a tower, chimneys, yards. Every recipe in the pack is put together from these."""
import os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from design import *


# ---- wall ladders: what the walls of each level are made of ----
# (ground storey infill, upper storey infill, footing, gable fill, floor)
def daub(x, y, z, along_x):
    """Wattle and daub between oak posts: daub, with wattle showing on some panels like his houses."""
    return DAUB_WATTLE if (x + z + y) % 5 == 0 else DAUB

def planks_wall(x, y, z, along_x): return PLANK_X if along_x else PLANK_Z

WALLS = {
    1: dict(low=DIRT, up=DIRT, sill=None, gable=DIRT, floor=DIRT, post_every=3, door='crude', fence='wattle-sticks', bed='hay'),
    2: dict(low=daub, up=daub, sill=None, gable=daub, floor=PLANK_X, post_every=3, door='crude', fence='wattle-sticks', bed='rough'),
    3: dict(low=RAMMED, up=daub, sill=COBBLE, gable=daub, floor=PLANK_X, post_every=3, door='solid-oak', fence='woodenfence-oak', bed='rough'),
    4: dict(low=COBBLE, up=planks_wall, sill=COBBLE, gable=planks_wall, floor=PLANK_X, post_every=4, door='solid-oak', fence='woodenfence-oak', bed='wood'),
    5: dict(low=BRICK, up=MUD, sill=POLISHED, gable=MUD, floor=PLANK_X, post_every=4, door='solid-oak', fence='drystonefence-shale', bed='rustic'),
}


def house(d, box, y0=1, storeys=1, height=3, axis=1, ends='jerkin', walls=None, level=None, roof=True, gable=True,
          hip_layers=2, floor=True, post_every=None, window_sides=('north', 'south', 'east', 'west'), windows='hole',
          window_every=3, no_windows=()):
    """A timber-framed building on box: `storeys` storeys of `height` wall layers each (a floor between),
    a roof with its eave on the top wall layer. walls: a WALLS entry (default by level).
    Returns dict(top=the top wall layer y, roof=roof positions, floors=[y of each storey's floor])."""
    w = walls or WALLS[level or d.level]
    pe = post_every if post_every is not None else w['post_every']
    floors = []
    y = y0
    if floor: d.floor_inside(box, y0 - 1, w['floor'] if d.level > 1 else DIRT)
    for s in range(storeys):
        infill = w['low'] if s == 0 else w['up']
        sill = w['sill'] if s == 0 and d.level >= 3 else None
        top = y + height - 1
        d.frame(box, y, top, infill, post_every=pe, sill=sill)
        floors.append(y)
        # windows: in the middle of each wall panel between posts, one up from the floor
        wy = y + 1
        x0, z0, x1, z1 = box
        for side in window_sides:
            if side in no_windows: continue
            if side in ('north', 'south'):
                z = z0 if side == 'north' else z1
                for x in range(x0 + 1, x1):
                    if pe and (x - x0) % pe == pe // 2 + (pe % 2) and (x - x0) % pe != 0 and x not in (x0 + 1, x1 - 1):
                        d.window((x, wy, z), windows)
            else:
                x = x0 if side == 'west' else x1
                for z in range(z0 + 1, z1):
                    if pe and (z - z0) % pe == pe // 2 + (pe % 2) and (z - z0) % pe != 0 and z not in (z0 + 1, z1 - 1):
                        d.window((x, wy, z), windows)
        if s < storeys - 1:
            d.floor_inside(box, top + 1, w['floor'])     # the next storey's floor, level with its bottom wall layer
            y = top + 1
        else:
            y = top
    r = set()
    if roof: r = d.gable_roof(box, y, axis, ends=ends, hip_layers=hip_layers, gable=w['gable'] if gable else None)
    d.rooms.append((box, y))
    return dict(top=y, roof=r, floors=floors, box=box)


def lean_to(d, box, y0, height, rise, wall=None, open_sides=(), level=None, posts=True, floor=None):
    """A shed-roofed structure on box: the roof rises toward `rise`; its tall wall is on that side.
    open_sides: sides left open between posts (no infill)."""
    w = WALLS[level or d.level]
    infill = wall if wall is not None else w['low']
    x0, z0, x1, z1 = box
    if floor: d.floor_inside(box, y0 - 1, floor)
    top = y0 + height - 1
    def fn(x, y, z, along_x):
        side = 'north' if z == z0 and along_x else 'south' if z == z1 and along_x else 'west' if x == x0 else 'east'
        if side in open_sides: return None
        return infill(x, y, z, along_x) if callable(infill) else infill
    d.frame(box, y0, top, fn, post_every=w['post_every'] if posts else 0, beam=True)
    # the tall side rises with the roof: fill it up under the slope
    r = d.shed_roof(box, top, rise)
    def fill_at(x, yy, z, along_x, side):
        if side not in open_sides and (x, yy, z) not in d.blocks:
            d.set((x, yy, z), infill(x, yy, z, along_x) if callable(infill) else infill)
    if rise in ('east', 'west'):
        hx = x1 if rise == 'east' else x0
        for (x, y, z) in r:   # the flank walls up under the slope
            if z in (z0, z1) and x0 <= x <= x1:
                for yy in range(top + 1, y): fill_at(x, yy, z, True, 'north' if z == z0 else 'south')
        hy = max([p[1] for p in r if p[0] == hx] or [top])   # the high wall
        for z in range(z0, z1 + 1):
            for yy in range(top + 1, hy): fill_at(hx, yy, z, False, 'east' if hx == x1 else 'west')
    else:
        hz = z1 if rise == 'south' else z0
        for (x, y, z) in r:
            if x in (x0, x1) and z0 <= z <= z1:
                for yy in range(top + 1, y): fill_at(x, yy, z, False, 'west' if x == x0 else 'east')
        hy = max([p[1] for p in r if p[2] == hz] or [top])
        for x in range(x0, x1 + 1):
            for yy in range(top + 1, hy): fill_at(x, yy, hz, True, 'south' if hz == z1 else 'north')
    return dict(top=top, roof=r, box=box)


def porch(d, box, y0, height, rise, posts=True):
    """An open shed roof on posts in front of a door (box = the posts' corners)."""
    x0, z0, x1, z1 = box
    top = y0 + height - 1
    if posts:
        for (x, z) in ((x0, z0), (x1, z0), (x0, z1), (x1, z1)):
            # no post against the house wall (the side opposite rise's low side is the house)
            for y in range(y0, top + 1):
                if not d.solid((x, y, z)): d.set((x, y, z), LOG)
    r = d.shed_roof(box, top, rise, overhang=0)
    return dict(top=top, roof=r)


def tower(d, box, y0, height, wall, roof='hip', post_every=0, posts=False, floors_every=0, floor=PLANK_X, ladder=None):
    """A square tower: walls y0..y0+height-1, a hip roof (or 'flat' with a battlement, or None).
    floors_every: a plank floor every n layers, with a ladder up through all of them at ladder=(x, z, facing)."""
    x0, z0, x1, z1 = box
    top = y0 + height - 1
    d.frame(box, y0, top, wall, post_every=post_every, beam=False, posts=posts)
    if floors_every:
        for y in range(y0 + floors_every, top + 1, floors_every):
            d.floor_inside(box, y, floor)
            if ladder: d.set((ladder[0], y, ladder[2]), None)
    if ladder:
        d.ladder(ladder[0], ladder[2], y0, top + (1 if roof == 'flat' else 0), ladder[1])
    r = set()
    if roof == 'hip': r = d.gable_roof(box, top, 0, ends='hip')
    elif roof == 'flat':
        d.floor_inside(box, top + 1, floor)
        # a battlement: a parapet with a merlon every other cell
        for (x, z) in ring(box):
            code = wall(x, top + 1, z, True) if callable(wall) else wall
            d.set((x, top + 1, z), code)
            if (x + z) % 2 == 0: d.set((x, top + 2, z), code)
    return dict(top=top, roof=r, box=box)


def chimney(d, x, z, y0, y1, code=COBBLE):
    for y in range(y0, y1 + 1): d.set((x, y, z), code)
    d.set((x, y1 + 1, z), slab('cobble', 'down') if code == COBBLE else slab('cobble', 'down'))


def yard(d, box, y=-1, fence=None, gaps=(), ground=True):
    if ground: d.ground(box, y)
    if fence: d.fence(box, y + 1, fence, gaps=gaps)


def woodpile(d, cells, y, high=1, axis='ns'):
    for (x, z) in cells:
        for k in range(high): d.set((x, y + k, z), f'game:log-placed-oak-{axis}')


def wall_lights(d, info_box, y, sides=('north', 'south'), spacing=4, outside=False):
    """Lanterns or torches along walls, inside (or outside) at height y."""
    x0, z0, x1, z1 = info_box
    for side in sides:
        if side in ('north', 'south'):
            z = (z0 - 1 if outside else z0 + 1) if side == 'north' else (z1 + 1 if outside else z1 - 1)
            for x in range(x0 + 2, x1 - 1, spacing):
                d.light((x, y, z), ('south' if side == 'north' else 'north') if not outside else ('north' if side == 'north' else 'south'))
        else:
            x = (x0 - 1 if outside else x0 + 1) if side == 'west' else (x1 + 1 if outside else x1 - 1)
            for z in range(z0 + 2, z1 - 1, spacing):
                d.light((x, y, z), ('east' if side == 'west' else 'west') if not outside else ('west' if side == 'west' else 'east'))


def hut_block(d, p):
    """The hut block on the floor; nothing may stand on it."""
    d.set(p, None); d.set_hut(p)
    above = add(p, 'up')
    if d.get(above) != 'game:air' and 'lantern' not in d.get(above) and 'torch' not in d.get(above): d.set(above, None)


def storage(d, n, cells, kinds=('game:chest-south', 'game:crate', 'game:barrel', 'game:trunk-east')):
    made = 0
    for i in range(n):
        code = kinds[i % len(kinds)]
        if code.startswith('game:chest') or code.startswith('game:trunk'):
            p = d.put_against(code.rsplit('-', 1)[0], cells)
        else: p = d.put(code, cells)
        if p: made += 1
    return made


def table_and_chairs(d, cells, style='rough', double=False):
    """A table with a chair either side, where it fits."""
    t = f'w4rd0sfurniture:{style}table{"double" if double else ""}-south'
    if t not in CATALOG:
        t = next((c for c in CATALOG if c.startswith(f'w4rd0sfurniture:{style}table')), None)
        if t is None: return None
    for p in cells:
        parts = CATALOG[t][1]
        need = [p] + [(p[0] + o[0], p[1] + o[1], p[2] + o[2]) for o, _ in parts]
        west = add(p, 'west'); east = add((need[-1] if parts else p), 'east')
        if all(d.standable(c) for c in need) and d.standable(west) and d.standable(east):
            d.place(t, p)
            ch = f'w4rd0sfurniture:{style}chairdining'
            if f'{ch}-east' in CATALOG: d.place(f'{ch}-east', west)
            if f'{ch}-west' in CATALOG: d.place(f'{ch}-west', east)
            return p
    return None


def bench(d, cells):
    return d.put('w4rd0sfurniture:roughbench-west', cells)


def flag(d, p):
    d.place('heraldrybanners:pole-flag-ground1x4-north', p)


def banner(d, p, facing):
    code = f'heraldrybanners:pole-banner-wall2-{facing}'
    if code in CATALOG: d.place(code, p)


def garden(d, box, y, crop=FARMLAND, rows=True):
    """Tilled rows with a path between."""
    x0, z0, x1, z1 = box
    for x in range(x0, x1 + 1):
        for z in range(z0, z1 + 1):
            if rows and (z - z0) % 3 == 2: d.set((x, y, z), PATH)
            else: d.place(crop, (x, y, z)) if crop in CATALOG else d.set((x, y, z), crop)
            d.set((x, y + 1, z), None)


def grass(d, box, y, every=5):
    x0, z0, x1, z1 = box
    i = 0
    for x in range(x0, x1 + 1):
        for z in range(z0, z1 + 1):
            i += 1
            if i % every == 0 and d.get((x, y, z)) == SOIL and d.free((x, y + 1, z)):
                d.set((x, y + 1, z), 'game:tallgrass-medium-free' if i % 2 else 'game:flower-bluebell-free')


def opening(d, cells, y0, y1):
    """Takes the wall out between two parts: cells = (x, z) list, from y0 to y1."""
    for (x, z) in cells:
        for y in range(y0, y1 + 1): d.set((x, y, z), None)


def steps_up(d, p, facing, material='cobble'):
    """A single step block in front of a door that stands a block above the ground."""
    d.set(p, stairs(material, facing))


def stone_tower(d, box, y0, height, roof='hip', floors_every=4, ladder=None, top_layers=3, bars=False):
    """A level-5 tower that reads like Aaron's masonry: two polished-stone footing layers, a stone-brick body
    with oak corner posts, slit windows every third layer alternating sides, a mud-brick top storey under the
    roof (hip, or flat with a battlement). bars: the top storey is an open barred gallery."""
    x0, z0, x1, z1 = box
    top = y0 + height - 1
    xm = (x0 + x1) // 2; zm = (z0 + z1) // 2
    def wall(x, y, z, along_x):
        if y <= y0 + 1: return POLISHED
        if y >= top - top_layers + 1:
            if bars and y < top and (x + z) % 2 == 0: return None
            return MUD
        layer = y - y0
        if layer % 3 == 2:
            if (along_x and x == xm and (layer // 3) % 2 == 0) or (not along_x and z == zm and (layer // 3) % 2 == 1): return None
        return BRICK
    return tower(d, box, y0, height, wall, roof=roof, post_every=0, posts=True, floors_every=floors_every, ladder=ladder)


def store_shed(d, box, y0, height, rise, wall, level, door=None):
    """A roofed stone store leaning on the main building on its `rise` side: stone walls, a shed roof."""
    lean_to(d, box, y0, height, rise, wall=wall, level=level, posts=True)
    if door: d.door(door[0], 'solid-oak', door[1])


def wall_fn(level):
    """The level's low-wall infill as a function, whether the WALLS entry is a code or a function."""
    w = WALLS[level]['low']
    return w if callable(w) else (lambda x, y, z, a: w)
