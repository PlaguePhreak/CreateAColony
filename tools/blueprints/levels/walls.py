"""Detailed walls for the second Oak & Shale pass: footing courses, posts at corners and beside openings,
sill beams and lintels, window surrounds and shutters, corbel courses under the eave, a different upper
storey, braces, chimneys; and the floor and floating-block checks every file must pass."""
from design import *
from roofs import RoofModule, BY_LEVEL

STAIR_MAT = {'planks': 'planks', 'cobble': 'cobble', 'brick': 'brick'}
SLAB_PLANK = slab('planks', 'down'); SLAB_PLANK_UP = slab('planks', 'up')
SLAB_COBBLE = slab('cobble', 'down'); SLAB_COBBLE_UP = slab('cobble', 'up')
SLAB_BRICK = slab('brick', 'down'); SLAB_BRICK_UP = slab('brick', 'up')
SLAB_POLISHED = 'game:cobblestoneslab-shale-down-free'
TRAP = 'game:trapdoor-window-oak-1'

# The wall language per level: see the revision doc's palette table.
LEVELS = {
    1: dict(low=DIRT, up=DIRT, footing=None, post=LOG, lintel=HAY, sill=None, surround=None, corbel=None, gable=DIRT,
            floor=DIRT, upfloor=DIRT, door='crude', window='wattle', fence='wattle-sticks', bed='hay', chimney=None, light='torch'),
    2: dict(low='daub', up='daub', footing=COBBLE, post=LOG, lintel=LOG_X, sill=SLAB_COBBLE_UP, surround=MUD_DARK, corbel=None, gable='daub',
            floor=DIRT, upfloor=PLANK_X, door='crude', window='wattle', fence='wattle-sticks', bed='rough', chimney=COBBLE, light='torch'),
    3: dict(low=RAMMED, up='daub', footing=COBBLE, post=LOG, lintel=DEBARKED_X, sill=SLAB_PLANK_UP, surround=None, corbel='cobble', gable='daub',
            floor=PLANK_X, upfloor=PLANK_X, door='solid-oak', window='trap', fence='woodenfence-oak', bed='rough', chimney=COBBLE, light='lantern'),
    4: dict(low=COBBLE, up='planks', footing=POLISHED, post=DEBARKED, lintel=DEBARKED_X, sill=SLAB_PLANK_UP, surround=None, corbel='brick', gable='planks',
            floor=COBBLE, upfloor=PLANK_X, door='solid-oak', window='trap', fence='woodenfence-oak', bed='wood', chimney=BRICK, light='lantern'),
    5: dict(low=BRICK, up=MUD, footing=POLISHED, post=DEBARKED, lintel=DEBARKED_X, sill=SLAB_POLISHED, surround=POLISHED, corbel='brick', gable=MUD,
            floor=POLISHED, upfloor=PLANK_X, door='solid-oak', window='trap', fence='drystonefence-shale', bed='rustic', chimney=BRICK, light='lantern'),
}


def infill_code(kind, x, y, z, along_x):
    if kind == 'daub': return DAUB_WATTLE if (x * 7 + z * 3 + y) % 6 == 0 else (DAUB_CRACKED if (x + z * 2 + y) % 9 == 0 else DAUB)
    if kind == 'planks': return PLANK_X if along_x else PLANK_Z
    return kind


def _side_cells(box, side):
    x0, z0, x1, z1 = box
    if side == 'north': return [(x, z0) for x in range(x0, x1 + 1)], True
    if side == 'south': return [(x, z1) for x in range(x0, x1 + 1)], True
    if side == 'west': return [(x0, z) for z in range(z0, z1 + 1)], False
    return [(x1, z) for z in range(z0, z1 + 1)], False


class Walls:
    """Builds one block of a building on box, storey by storey, with openings planned first."""
    def __init__(self, d, box, y0=1, level=None):
        self.d, self.box, self.y0 = d, box, y0
        self.L = LEVELS[level or d.level]
        self.doors = []     # (x, z, side)
        self.windows = []   # (x, z, side, storey)
        self.posts = set()
        self.storey_y = []  # bottom wall layer of each storey
        self.top = y0 - 1

    # ---- planning ----
    def door(self, side, at=None):
        cells, along_x = _side_cells(self.box, side)
        inner = cells[1:-1]
        c = inner[len(inner) // 2] if at is None else (at if isinstance(at, tuple) else inner[at])
        self.doors.append((c[0], c[1], side)); return c

    def window(self, side, at, storey=0):
        cells, _ = _side_cells(self.box, side)
        c = cells[at] if isinstance(at, int) else at
        self.windows.append((c[0], c[1], side, storey)); return c

    def windows_along(self, side, every=3, storey=0, skip=()):
        """Windows spaced `every` apart on a side, placed from the centre out; a door column and the cells
        beside it are left alone, and the pattern shifts by one when the door sits in the middle."""
        cells, _ = _side_cells(self.box, side)
        inner = cells[1:-1]
        n = len(inner)
        doors = {(dx, dz) for dx, dz, _ in self.doors}
        blocked = set()
        for i, c in enumerate(inner):
            if c in doors: blocked |= {i - 1, i, i + 1}
        centre = (n - 1) // 2
        offsets = [0] if centre not in blocked else [-(every - 1), every - 1] if every > 1 else []
        picked = set()
        for o in offsets:
            i = centre + o
            while i >= 0:
                if i not in blocked and i < n: picked.add(i)
                i -= every
            i = centre + o + every
            while i < n:
                if i not in blocked: picked.add(i)
                i += every
        for i in sorted(picked):
            c = inner[i]
            if c in skip: continue
            self.windows.append((c[0], c[1], side, storey))
        return self

    # ---- building ----
    def build(self, storeys=1, height=3, up_from=1, floor=True):
        d, L, box = self.d, self.L, self.box
        x0, z0, x1, z1 = box
        y = self.y0
        if floor: d.floor_inside(box, y - 1, L['floor'])
        if L['footing']:
            for (x, z) in ring(box): d.set((x, y - 1, z), L['footing'])
        opening_cols = {(x, z) for x, z, _ in self.doors} | {(x, z) for x, z, s, st in self.windows}
        for s in range(storeys):
            last = s == storeys - 1
            top = y + height - 1 if last else y + height       # the beam layer carries the next floor
            self.storey_y.append(y)
            infill = L['low'] if s < up_from else L['up']
            # posts: corners, beside every opening, and every 3 or 4 in between
            posts = set()
            for (x, z) in ((x0, z0), (x1, z0), (x0, z1), (x1, z1)): posts.add((x, z))
            for (x, z) in opening_cols:
                for n in ((x - 1, z), (x + 1, z), (x, z - 1), (x, z + 1)):
                    if n in set(ring(box)): posts.add(n)
            self.posts |= posts
            for (x, z) in ring(box):
                along_x = z in (z0, z1)
                for yy in range(y, top + 1):
                    if (x, z) in posts: code = L['post']
                    elif yy == top: code = LOG_X if along_x else LOG_Z          # the beam the floor or roof sits on
                    elif yy == y and s == 0 and L['footing'] and d.level >= 3: code = L['footing'] if (x + z) % 2 == 0 else infill_code(infill, x, yy, z, along_x)
                    else: code = infill_code(infill, x, yy, z, along_x)
                    d.set((x, yy, z), code)
            # openings on this storey
            if s == 0:
                for (x, z, side) in self.doors:
                    d.door((x, y, z), L['door'], side)
                    if L['lintel']: d.set((x, y + 2, z), L['lintel'] if z in (z0, z1) else (LOG_Z if L['lintel'] == LOG_X else DEBARKED_Z if L['lintel'] == DEBARKED_X else L['lintel']))
                    # a step and a light beside the door
                    out = add((x, y - 1, z), side)
                    d.set(out, stairs(L['corbel'] or 'cobble', side) if d.level >= 2 else DIRT) if d.free((out[0], out[1] + 1, out[2])) else None
            for (x, z, side, st) in self.windows:
                if st != s: continue
                wy = y + 1
                d.window((x, wy, z), 'hole' if L['window'] == 'hole' else L['window'])
                if L['surround']:
                    for n in ((x, wy + 1, z),):
                        d.set(n, L['surround'])
                elif L['lintel'] and (x, wy + 1, z) != (x, top, z):
                    d.set((x, wy + 1, z), L['lintel'] if z in (z0, z1) else (DEBARKED_Z if L['lintel'] == DEBARKED_X else LOG_Z))
                if L['sill']:
                    o = add((x, wy - 1, z), side)
                    if d.free(o): d.set(o, L['sill'])
            if not last:
                d.floor_inside(box, top, L['upfloor'])
                y = top + 1
            else:
                y = top
        self.top = y
        d.rooms.append((box, y))
        d.notes.append(('floors', box, [yy - 1 for yy in self.storey_y]))
        return self

    def corbels(self, sides=('north', 'south')):
        """A course of stairs under the eave on the long sides, the way a stone eave is carried."""
        d, L = self.d, self.L
        if not L['corbel']: return self
        x0, z0, x1, z1 = self.box
        for side in sides:
            cells, along_x = _side_cells(self.box, side)
            for (x, z) in cells:
                o = add((x, self.top, z), side)
                if d.free(o): d.set(o, stairs(L['corbel'], OPP[side], up=False))
        return self

    def jetty(self, sides=('north', 'south'), storey=1):
        """The upper storey oversails the lower by one block on `sides`, carried on plank stairs."""
        d = self.d
        if storey >= len(self.storey_y): return self
        y = self.storey_y[storey]
        x0, z0, x1, z1 = self.box
        for side in sides:
            cells, along_x = _side_cells(self.box, side)
            for (x, z) in cells:
                o = add((x, y - 1, z), side)
                d.set(o, stairs('planks', OPP[side], up=False))
                for yy in range(y, self.top + 1):
                    oo = (o[0], yy, o[2])
                    if (x, z) in self.posts and yy < self.top: d.set(oo, self.L['post'])
                    elif yy == self.top: d.set(oo, LOG_X if along_x else LOG_Z)
                    else: d.set(oo, infill_code(self.L['up'], oo[0], yy, oo[2], along_x))
        return self

    def braces(self, sides=('north', 'south'), storey=0):
        """Diagonal-looking braces: a stair block against the post foot and head on the outside face."""
        d = self.d
        y = self.storey_y[storey] if storey < len(self.storey_y) else self.y0
        for side in sides:
            cells, along_x = _side_cells(self.box, side)
            for (x, z) in cells:
                if (x, z) not in self.posts: continue
                o = add((x, y, z), side)
                if d.free(o): d.set(o, stairs('planks', side, up=True))
        return self

    def gable_fill(self, axis, ridge_y, code=None):
        """Fills the triangle of wall under a gable roof at both ends (axis 0: ends on west/east)."""
        d, L = self.d, self.L
        x0, z0, x1, z1 = self.box
        fill = code or L['gable']
        if axis == 1:
            for z in (z0, z1):
                along_x = True
                for y in range(self.top + 1, ridge_y + 1):
                    k = y - self.top
                    for x in range(x0 + k, x1 - k + 1):
                        if d.free((x, y, z)): d.set((x, y, z), infill_code(fill, x, y, z, along_x))
        else:
            for x in (x0, x1):
                for y in range(self.top + 1, ridge_y + 1):
                    k = y - self.top
                    for z in range(z0 + k, z1 - k + 1):
                        if d.free((x, y, z)): d.set((x, y, z), infill_code(fill, x, y, z, False))
        return self


def chimney(d, x, z, y0, y1, code, cap=True):
    for y in range(y0, y1 + 1): d.set((x, y, z), code)
    if cap: d.set((x, y1 + 1, z), slab('cobble' if code == COBBLE else 'brick', 'down'))


def hearth(d, x, z, y, facing, code=COBBLE):
    """A hearth block pattern in the floor and on the wall behind the fire at (x, y, z)."""
    d.place('game:firepit-cold', (x, y, z))
    back = add((x, y, z), OPP[facing])
    for yy in range(y, y + 3): d.set((back[0], yy, back[2]), code)
    for s in ('north', 'south', 'east', 'west'):
        if s in (facing, OPP[facing]): continue
        n = add((x, y - 1, z), s); d.set(n, code)
    d.set((x, y - 1, z), code)


# ---- checks ----
def check_floors(d):
    """Every room has a floor on every storey."""
    problems = []
    for kind, box, ys in d.notes:
        if kind != 'floors': continue
        x0, z0, x1, z1 = box
        for y in ys:
            def ok(x, y, z):
                if d.solid((x, y, z)) or d.solid((x, y - 1, z)): return True
                return any(k in d.get((x, yy, z)) for yy in (y, y - 1, y - 2) for k in ('ladder', 'stairs'))   # a hatch over a ladder or a stair
            missing = [(x, y, z) for x in range(x0 + 1, x1) for z in range(z0 + 1, z1) if not ok(x, y, z)]
            if missing: problems.append(f'room {box} has no floor at y={y}: {len(missing)} cells, e.g. {missing[0]}')
    return problems


def check_floating(d):
    """Every block touches another block or the ground (6 neighbours)."""
    bad = []
    for p, c in d.blocks.items():
        if any(k in c for k in ('multiblock',)): continue
        if not any(add(p, s) in d.blocks for s in D): bad.append(p)
    return [f'{len(bad)} floating blocks, e.g. {bad[:3]}'] if bad else []
