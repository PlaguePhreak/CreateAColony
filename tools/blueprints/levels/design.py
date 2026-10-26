"""
A kit for drawing buildings new, in Aaron's Dev's Builds language: oak posts and beams, infill walls that
climb his ladder (packed dirt, wattle, daub, rammed earth, mud brick, oak planks, shale cobble, shale
brick), red shingle roofs from VS Roofing at 1:1 pitch with a one-block eave, his furniture mods.

Only block codes that appear in his own exports are used (see kit2.BLOCK_IDS); roof blocks get his
saved shape data through roofdata. Coordinates: x east, y up, z south. A wall box is
(x0, z0, x1, z1) = the outer wall columns; a build's ground level is y = 0 (the floor blocks), so
walls start at y = 1.
"""
import os, sys, collections, random
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import kit2, roofdata
from kit2 import Build, CATALOG, DOORS, inside

D = {'east': (1, 0, 0), 'west': (-1, 0, 0), 'north': (0, 0, -1), 'south': (0, 0, 1), 'up': (0, 1, 0), 'down': (0, -1, 0)}
OPP = {'east': 'west', 'west': 'east', 'north': 'south', 'south': 'north', 'up': 'down', 'down': 'up'}
DIRS = ['north', 'east', 'south', 'west']
AIRY = ('multiblock', 'fence', 'torch', 'lantern', 'ladder', 'tallgrass', 'flower', 'wattle-sticks', 'stonepath', 'farmland')

# ---- his materials ----
LOG = 'game:log-placed-oak-ud'
LOG_X = 'game:log-placed-oak-we'     # a beam that runs east-west
LOG_Z = 'game:log-placed-oak-ns'
DEBARKED = 'game:debarkedlog-oak-ud'
DEBARKED_X = 'game:debarkedlog-oak-we'   # not yet seen in an export: on the palette list for Aaron
DEBARKED_Z = 'game:debarkedlog-oak-ns'
PLANK = 'game:planks-oak-ud'
PLANK_X = 'game:planks-oak-we'       # wall planks on a wall that runs east-west, and floors
PLANK_Z = 'game:planks-oak-ns'
DIRT = 'game:packeddirt'
RAMMED = 'game:rammed-light-plain'
MUD = 'game:mudbrick-light'
MUD_DARK = 'game:mudbrick-dark'
DAUB = 'game:daub-tan-normal'
DAUB_WATTLE = 'game:daub-tan-wattle'
DAUB_CRACKED = 'game:daub-tan-cracked'
COBBLE = 'game:cobblestone-shale'
BRICK = 'game:stonebricks-shale'
POLISHED = 'game:rockpolished-shale'
SOIL = 'game:soil-medium-normal'
PATH = 'game:stonepath-free'
HAY = 'game:hay-normal-ud'
FARMLAND = 'game:farmland-moist-medium'


def add(p, d, n=1):
    dx, dy, dz = D[d]
    return (p[0] + dx * n, p[1] + dy * n, p[2] + dz * n)


def stairs(material, facing, up=True):
    """material: 'planks' | 'cobble' | 'brick'; facing = the side the step rises toward."""
    base = {'planks': 'game:plankstairs-oak', 'cobble': 'game:cobblestonestairs-shale', 'brick': 'game:stonebrickstairs-shale'}[material]
    return f'{base}-{"up" if up else "down"}-{facing}-free'


def slab(material, side='down'):
    base = {'planks': 'game:plankslab-oak', 'cobble': 'game:cobblestoneslab-shale', 'brick': 'game:stonebrickslab-shale'}[material]
    return f'{base}-{side}-free'


def fence_code(kind, joins):
    return f'game:{kind}-{joins or "empty"}-free'


class Design(Build):
    def __init__(self, type, variant='', level=1):
        super().__init__()
        self.type, self.variant, self.level = type, variant, level
        self.name = f'{type}{"/" + variant if variant else ""} level {level}'
        self.roof_wants = {}
        self.rooms = []      # (box, top wall y) of every roofed part, for clipping roofs out of rooms
        self.notes = []

    # ---- looking ----
    def free(self, p): return self.get(p) == 'game:air'
    def solid(self, p):
        c = self.get(p)
        return c != 'game:air' and not any(k in c for k in AIRY)
    def standable(self, p): return self.free(p) and self.solid(add(p, 'down'))

    # ---- ground and floors ----
    def ground(self, box, y=-1, code=SOIL, depth=1):
        """Earth under the plot: box (x0, z0, x1, z1), from y down `depth` layers, only where nothing is."""
        x0, z0, x1, z1 = box
        for x in range(x0, x1 + 1):
            for z in range(z0, z1 + 1):
                for k in range(depth):
                    if (x, y - k, z) not in self.blocks: self.set((x, y - k, z), code)

    def floor(self, box, y, code=PLANK_X):
        x0, z0, x1, z1 = box
        for x in range(x0, x1 + 1):
            for z in range(z0, z1 + 1): self.set((x, y, z), code)

    def floor_inside(self, box, y, code=PLANK_X):
        x0, z0, x1, z1 = box
        self.floor((x0 + 1, z0 + 1, x1 - 1, z1 - 1), y, code)

    def box_fill(self, box, y0, y1, code):
        x0, z0, x1, z1 = box
        for x in range(x0, x1 + 1):
            for z in range(z0, z1 + 1):
                for y in range(y0, y1 + 1): self.set((x, y, z), code)

    def hollow(self, box, y0, y1):
        x0, z0, x1, z1 = box
        for x in range(x0 + 1, x1):
            for z in range(z0 + 1, z1):
                for y in range(y0, y1 + 1): self.set((x, y, z), None)

    # ---- walls ----
    def wall_cells(self, box):
        x0, z0, x1, z1 = box
        return [(x, z) for x in range(x0, x1 + 1) for z in range(z0, z1 + 1) if x in (x0, x1) or z in (z0, z1)]

    def frame(self, box, y0, y1, infill, post_every=3, beam=True, post=LOG, posts=True, sill=None):
        """Timber-framed walls round box from y0 to y1: oak posts at the corners and every post_every blocks,
        a beam along the top (y1), infill between. infill may be a code or a function (x, y, z, along_x) -> code.
        sill: a code for the whole y0 layer (a stone footing)."""
        x0, z0, x1, z1 = box
        post_at = set()
        if posts:
            for x in (x0, x1):
                for z in (z0, z1): post_at.add((x, z))
            if post_every:
                for x in range(x0, x1 + 1):
                    if (x - x0) % post_every == 0: post_at.add((x, z0)); post_at.add((x, z1))
                for z in range(z0, z1 + 1):
                    if (z - z0) % post_every == 0: post_at.add((x0, z)); post_at.add((x1, z))
        for (x, z) in self.wall_cells(box):
            along_x = z in (z0, z1)
            for y in range(y0, y1 + 1):
                if (x, z) in post_at: code = post
                elif beam and y == y1 and not (x, z) in post_at: code = LOG_X if along_x else LOG_Z
                elif sill and y == y0: code = sill
                else: code = infill(x, y, z, along_x) if callable(infill) else infill
                self.set((x, y, z), code)
        return post_at

    def plain_walls(self, box, y0, y1, code):
        self.frame(box, y0, y1, code, post_every=0, beam=False, posts=False)

    def window(self, p, kind='hole', facing=None):
        """A window at p: 'hole' (open), 'wattle' (wattle sticks in the hole), 'trap' (an oak window trapdoor)."""
        p = tuple(p)
        if kind == 'hole': self.set(p, None); return
        if kind == 'wattle':
            along_x = self.get(add(p, 'east')) != 'game:air' or self.get(add(p, 'west')) != 'game:air'
            self.set(p, fence_code('wattle-sticks', 'ew' if along_x else 'ns')); return
        if kind == 'trap':
            self.place('game:trapdoor-window-oak-1', p); return

    def windows(self, cells, kind='hole'):
        for p in cells: self.window(p, kind)

    def door_in(self, p, kind, facing):
        """A door at p in a wall; the wall above it is kept (the door's top half replaces the block above)."""
        self.door(p, kind, facing)

    def gate(self, p, facing, wide=False):
        code = 'game:door-2x3gate-oak' if wide else 'game:door-1x3gate-oak'
        ent = dict(CATALOG.get(code, (DOORS[facing], []))[0] or DOORS[facing])
        ent['rotateYRad'] = DOORS[facing]['rotateYRad']
        self.set(p, code, ent)
        parts = CATALOG.get(code, (None, []))[1]
        for off, pcode in parts: self.set((p[0] + off[0], p[1] + off[1], p[2] + off[2]), pcode)

    # ---- roofs ----
    def gable_roof(self, box, yt, axis, ends='jerkin', overhang=1, hip_layers=2, gable=None, skip=None, code='vsroofing:roof-filled'):
        """A red shingle roof over box (outer wall columns), eave at yt (the top wall layer), 1:1 pitch, the
        way his roofs go: the eave row flush with the end walls, every row above it one block past them.
        axis 0: the ridge runs east-west; axis 1: north-south. ends: 'gable' (vertical end walls, filled
        with `gable`), 'hip' (sloped all round, like his Guard Tower), 'jerkin' (gable below, the top
        hip_layers rows hipped, like his houses). The wall width across the ridge must be odd.
        Returns the set of roof positions."""
        x0, z0, x1, z1 = box
        midx, midz = (x0 + x1) / 2, (z0 + z1) / 2
        def P(a, y, c):   # a along the ridge, c across it
            return (a, y, c) if axis == 0 else (c, y, a)
        lo_face, hi_face = ('south', 'north') if axis == 0 else ('east', 'west')   # slopes rise toward the ridge
        end_lo, end_hi = ('east', 'west') if axis == 0 else ('south', 'north')     # hipped ends rise inward
        if axis == 0: a0, a1, c0, c1 = x0, x1, z0, z1
        else: a0, a1, c0, c1 = z0, z1, x0, x1
        co0, co1 = c0 - overhang, c1 + overhang
        span = co1 - co0 + 1
        assert span % 2 == 1, f'roof span across must be odd, got {span} for {box}'
        K = span // 2
        out = set()
        corner_face = {'nw': 'east', 'ne': 'south', 'se': 'west', 'sw': 'north'}
        for k in range(K + 1):
            y = yt + k
            cl, cr = co0 + k, co1 - k
            if ends == 'hip': hipped, j = True, k
            elif ends == 'jerkin' and k >= K - hip_layers: hipped, j = True, k - (K - hip_layers)
            else: hipped, j = False, 0
            if hipped: al, ar = a0 - overhang + j, a1 + overhang - j
            elif k == 0: al, ar = a0, a1
            else: al, ar = a0 - overhang, a1 + overhang
            if cl == cr:   # the ridge
                for a in range(al, ar + 1):
                    face = lo_face
                    if ends == 'hip' and a == al: face = 'east' if axis == 0 else 'south'
                    if ends == 'hip' and a == ar: face = 'south' if axis == 0 else 'west'
                    p = P(a, y, cl); out.add(p); self.set(p, f'{code}-{face}'); self.roof_wants[p] = 'ridge'
                continue
            for a in range(al, ar + 1):
                for c, face in ((cl, lo_face), (cr, hi_face)):
                    p = P(a, y, c)
                    if hipped and ends == 'hip' and a in (al, ar):
                        if k == 0: continue   # his eave rings leave the diagonal corner out
                        face = corner_face[('n' if p[2] < midz else 's') + ('w' if p[0] < midx else 'e')]
                    out.add(p); self.set(p, f'{code}-{face}')
                    if k == 0: self.roof_wants[p] = 'eave'
            if hipped:
                for c in range(cl + 1, cr):
                    for a, face in ((al, end_lo), (ar, end_hi)):
                        p = P(a, y, c); out.add(p); self.set(p, f'{code}-{face}')
        if gable:
            # the end walls under the roof: every empty cell on the wall line with roof above it
            for a in (a0, a1):
                for c in range(c0 + 1, c1):
                    col = [p for p in out if (p[0] if axis == 0 else p[2]) == a and (p[2] if axis == 0 else p[0]) == c]
                    top = max(p[1] for p in col) if col else yt
                    for y in range(yt + 1, top):
                        p = P(a, y, c)
                        if p not in self.blocks: self.set(p, gable(p[0], p[1], p[2], axis == 0) if callable(gable) else gable)
        if skip:
            for p in list(out):
                if p in skip: self.set(p, None); out.discard(p)
        return out

    def shed_roof(self, box, yt, rise, overhang=1, code='vsroofing:roof-filled'):
        """A single slope over box, rising toward `rise` (a direction) at 1:1, eave at yt on the low side.
        The high side is the wall it leans on. Returns the roof positions."""
        x0, z0, x1, z1 = box
        out = set()
        if rise in ('east', 'west'):
            cols = list(range(x0 - overhang, x1 + 1)) if rise == 'east' else list(range(x1 + overhang, x0 - 1, -1))
            for k, x in enumerate(cols):
                for z in range(z0 - overhang, z1 + overhang + 1):
                    p = (x, yt + k, z); out.add(p); self.set(p, f'{code}-{rise}')
                    if k == 0: self.roof_wants[p] = 'eave'
        else:
            rows = list(range(z0 - overhang, z1 + 1)) if rise == 'south' else list(range(z1 + overhang, z0 - 1, -1))
            for k, z in enumerate(rows):
                for x in range(x0 - overhang, x1 + overhang + 1):
                    p = (x, yt + k, z); out.add(p); self.set(p, f'{code}-{rise}')
                    if k == 0: self.roof_wants[p] = 'eave'
        return out

    def clip_roofs(self):
        """Roof blocks of one part that fall inside another part's rooms are taken out."""
        for p, c in list(self.blocks.items()):
            if not c.startswith('vsroofing'): continue
            for (x0, z0, x1, z1), top in self.rooms:
                if x0 < p[0] < x1 and z0 < p[2] < z1 and p[1] <= top:
                    self.set(p, None); self.roof_wants.pop(p, None); break

    def finish_roofs(self):
        """Gives every new roof block his shape data."""
        self.clip_roofs()
        for p, c in self.blocks.items():
            if c.startswith('vsroofing') and p not in self.ents: self.ents[p] = {'_new': ('bool', True)}
        n, ex = roofdata.attach(self, self.roof_wants)
        for p in list(self.ents):
            self.ents[p].pop('_new', None)
        return n, ex

    # ---- fittings ----
    def lantern(self, p, facing=None, large=False):
        """A small lantern at p hung on the wall beside it, or from the block above."""
        if facing is None:
            for d in ('south', 'north', 'east', 'west'):
                if self.solid(add(p, OPP[d])): facing = d; break
            else: facing = 'down' if self.solid(add(p, 'up')) else None
        if facing is None or not self.free(p): return False
        self.place(f'game:lantern-small-{facing}', p)
        return True

    def torch(self, p, facing=None):
        if facing is None:
            for d in ('south', 'north', 'east', 'west'):
                if self.solid(add(p, OPP[d])): facing = d; break
            else: return False
        if not self.free(p): return False
        self.place(f'game:torch-basic-lit-{facing}', p)
        return True

    def light(self, p, facing=None):
        return self.lantern(p, facing) if self.level >= 3 else self.torch(p, facing)

    def put(self, code, cells, parts_ok=True):
        """The first of the cells where the fitting (with its parts) fits on a floor. Returns the spot."""
        ent, parts = CATALOG.get(code, (None, []))
        for p in cells:
            need = [p] + [(p[0] + o[0], p[1] + o[1], p[2] + o[2]) for o, _ in parts]
            if all(self.free(c) for c in need) and self.solid(add(p, 'down')):
                self.place(code, p); return p
        return None

    def bed(self, p, kind='rough', facing=None):
        """A bed with its head at p. kind: 'hay', 'rough', 'wood', 'rustic', 'double'."""
        codes = {'rough': ['w4rd0sfurniture:roughbed-north', 'w4rd0sfurniture:roughbed-south', 'w4rd0sfurniture:roughbed-east'],
                 'double': ['w4rd0sfurniture:roughbeddouble-south'],
                 'rustic': ['w4rd0sfurniture:rusticbedsingle-east', 'w4rd0sfurniture:rusticbedsingle-west'],
                 'hay': ['game:bed-hay-head-south', 'game:bed-hay-head-east'],
                 'wood': ['game:bed-wood-head-east', 'game:bed-wood-head-north']}[kind]
        for code in codes:
            f = code.split('-')[-1]
            if facing and f != facing: continue
            if kind in ('hay', 'wood'):
                q = add(p, f)
                if self.standable(p) and self.standable(q):
                    self.place(code, p); self.place(code.replace('head', 'feet'), q); return True
            else:
                parts = CATALOG[code][1]
                cells = [p] + [(p[0] + o[0], p[1] + o[1], p[2] + o[2]) for o, _ in parts]
                if all(self.standable(c) for c in cells):
                    self.place(code, p); return True
        return False

    def beds(self, n, cells, kind='rough', facing=None):
        made = 0
        for p in cells:
            if made >= n: break
            if self.bed(p, kind, facing): made += 1
        return made

    def ladder(self, x, z, y0, y1, facing):
        for y in range(y0, y1 + 1): self.set((x, y, z), f'game:ladder-wood-oak-{facing}')

    def stair_run(self, start, direction, steps, material='planks', clear_above=2):
        """A straight stair of `steps` steps from `start` (the lowest step's spot) climbing toward direction."""
        for i in range(steps):
            p = add(start, direction, i); p = (p[0], p[1] + i, p[2])
            self.set(p, stairs(material, direction))
            for h in range(1, clear_above + 1): self.set((p[0], p[1] + h, p[2]), None)

    def fence(self, box, y, kind='woodenfence-oak', gaps=(), only_on_ground=True):
        """A fence round the box (x0, z0, x1, z1) edge at y with the right joins; gaps: (x, z) cells left open."""
        x0, z0, x1, z1 = box
        ring = {(x, z) for x in range(x0, x1 + 1) for z in range(z0, z1 + 1) if x in (x0, x1) or z in (z0, z1)}
        for g in gaps: ring.discard(tuple(g))
        for (x, z) in ring:
            if not self.free((x, y, z)): continue
            if only_on_ground and not self.solid((x, y - 1, z)): continue
            joins = ''.join(d[0] for d in ('north', 'east', 'south', 'west') if (x + D[d][0], z + D[d][2]) in ring)
            self.set((x, y, z), fence_code(kind, joins))

    def fence_line(self, cells, y, kind='woodenfence-oak'):
        cells = [tuple(c) for c in cells]; ring = set(cells)
        for (x, z) in cells:
            if not self.free((x, y, z)) or not self.solid((x, y - 1, z)): continue
            joins = ''.join(d[0] for d in ('north', 'east', 'south', 'west') if (x + D[d][0], z + D[d][2]) in ring)
            self.set((x, y, z), fence_code(kind, joins))

    def path(self, cells, y=0, code=PATH):
        for (x, z) in cells:
            if self.free((x, y + 1, z)) and (self.get((x, y, z)) in (SOIL, 'game:air') or self.get((x, y, z)).startswith('game:soil')):
                self.set((x, y, z), code)

    def hut(self):
        return next(p for p, c in self.blocks.items() if c.startswith('createacolony:') and not c.startswith('createacolony:waypoint'))

    def set_hut(self, p):
        self.place(kit2.hut_code_of(self.type), p)

    def inside_cells(self, box, y, margin=1):
        """Floor cells inside the walls at y, from the walls inward, in reading order."""
        x0, z0, x1, z1 = box
        return [(x, y, z) for z in range(z0 + margin, z1 - margin + 1) for x in range(x0 + margin, x1 - margin + 1)]

    def along_wall(self, box, y, side, margin=1):
        """Cells just inside one wall at y (for things that stand against a wall)."""
        x0, z0, x1, z1 = box
        if side == 'north': return [(x, y, z0 + 1) for x in range(x0 + margin, x1 - margin + 1)]
        if side == 'south': return [(x, y, z1 - 1) for x in range(x0 + margin, x1 - margin + 1)]
        if side == 'west': return [(x0 + 1, y, z) for z in range(z0 + margin, z1 - margin + 1)]
        return [(x1 - 1, y, z) for z in range(z0 + margin, z1 - margin + 1)]

    def facing_wall(self, p):
        """The direction from p to the nearest solid neighbour (what a wall-hung thing should face away from)."""
        for d in ('north', 'east', 'south', 'west'):
            if self.solid(add(p, d)): return OPP[d]
        return 'north'

    def put_against(self, code_base, cells):
        """Furniture that faces away from the wall it stands at: code_base-<facing>."""
        for p in cells:
            f = self.facing_wall(p)
            code = f'{code_base}-{f}'
            if code not in CATALOG:
                alts = [c for c in CATALOG if c.startswith(code_base + '-')]
                if not alts: return None
                code = alts[0]
            if self.put(code, [p]): return p
        return None

    # ---- out ----
    def finish(self):
        self.finish_roofs()
        return self.check(self.type)

    def save_to(self, folder):
        path = os.path.join(folder, self.type, self.variant, f'level{self.level}.json') if self.variant else os.path.join(folder, self.type, f'level{self.level}.json')
        self.finish_roofs()
        return self.save(path, self.type)


def rect(x0, z0, x1, z1): return (x0, z0, x1, z1)
def cells(box, y):
    x0, z0, x1, z1 = box
    return [(x, y, z) for z in range(z0, z1 + 1) for x in range(x0, x1 + 1)]
def ring(box):
    x0, z0, x1, z1 = box
    return [(x, z) for x in range(x0, x1 + 1) for z in range(z0, z1 + 1) if x in (x0, x1) or z in (z0, z1)]
def line(a, b):
    """Cells (x, z) from a to b along one axis."""
    (xa, za), (xb, zb) = a, b
    if xa == xb: return [(xa, z) for z in range(min(za, zb), max(za, zb) + 1)]
    return [(x, za) for x in range(min(xa, xb), max(xa, xb) + 1)]
