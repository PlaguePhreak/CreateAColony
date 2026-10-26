"""Mining and the wider land: Miner, Quarry, Prospector, Explorer."""
from parts import *
from recipes_homes import recipe, RECIPES

MINE_WP = 'createacolony:waypoint-mine'
QUARRY_WP = 'createacolony:waypoint-quarry'
ROCK = 'game:rock-shale'


def shaft(d, x, z, bottom, top, facing='north', lining=COBBLE, mouth=None):
    """A one-block shaft at (x, z) from `top` (the floor level) down to `bottom`, lined, a ladder down it,
    the Mine waypoint on the shaft floor. mouth: the floor code round the shaft top."""
    for y in range(bottom, top + 1):
        d.set((x, y, z), None)
        for (dx, dz) in ((1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (-1, -1), (1, -1), (-1, 1)):
            if y < top: d.set((x + dx, y, z + dz), lining)
    d.set((x, bottom - 1, z), lining)
    side = {'north': (0, -1), 'south': (0, 1), 'east': (1, 0), 'west': (-1, 0)}[facing]
    d.ladder(x, z, bottom, top, facing)
    d.set((x, bottom, z), MINE_WP)
    if mouth:
        for (dx, dz) in ((1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (-1, -1), (1, -1), (-1, 1)): d.set((x + dx, top - 1, z + dz), mouth)


def headframe(d, x, z, y0, height, beam_axis='x'):
    """An open timber headframe over a shaft at (x, z): four posts round it and a beam across the top."""
    for (dx, dz) in ((-2, -2), (2, -2), (-2, 2), (2, 2)):
        for y in range(y0, y0 + height): d.set((x + dx, y, z + dz), LOG)
    top = y0 + height
    if beam_axis == 'x':
        for dz in (-2, 2):
            for dx in range(-2, 3): d.set((x + dx, top, z + dz), LOG_X)
        for dz in range(-2, 3): d.set((x, top, z + dz), LOG_Z)
    else:
        for dx in (-2, 2):
            for dz in range(-2, 3): d.set((x + dx, top, z + dz), LOG_Z)
        for dx in range(-2, 3): d.set((x + dx, top, z), LOG_X)


def rock_piles(d, cells, y, high=1):
    for (x, z) in cells:
        for yy in range(y, y + high): d.set((x, yy, z), ROCK)


def quarry_point(d, hy=0):
    """The Quarry waypoint 8 blocks beyond the north edge, on a path block, so the pit misses the building."""
    xs = [p[0] for p in d.blocks]; zs = [p[2] for p in d.blocks]
    x = (min(xs) + max(xs)) // 2; z = min(zs) - 8
    d.set((x, hy - 1, z), PATH); d.set((x, hy, z), QUARRY_WP)


def crane(d, x, z, y0, height, reach, axis='x', base=COBBLE):
    """A timber crane: a stone base, a post, a jib beam out along axis."""
    d.set((x, y0 - 1, z), base)
    for y in range(y0, y0 + height): d.set((x, y, z), LOG)
    if axis == 'x':
        for i in range(0, reach + 1): d.set((x + i, y0 + height, z), LOG_X)
        d.set((x + reach, y0 + height - 1, z), fence_code('woodenfence-oak', 'ns'))
    else:
        for i in range(0, reach + 1): d.set((x, y0 + height, z + i), LOG_Z)
        d.set((x, y0 + height - 1, z + reach), fence_code('woodenfence-oak', 'ew'))


# ---------------------------------------------------------------- Miner
@recipe('miner')
def miner(d):
    L = d.level
    if L == 1:
        # A pithead: a timber headframe over the first shaft, a wattle tool shelter with a hay bed to one side,
        # a tally basket, a torch on the frame.
        yard(d, (0, 0, 12, 10))
        d.floor((2, 2, 8, 8), 0, DIRT)
        shaft(d, 5, 5, -8, 0, 'north', DIRT, mouth=COBBLE)
        headframe(d, 5, 5, 1, 4, 'x')
        box = (9, 2, 12, 7)
        lean_to(d, box, 1, 3, 'east', open_sides=('west',), level=1, floor=DIRT)
        hut_block(d, (11, 1, 6))
        d.bed((11, 1, 3), 'hay', 'south') or d.bed((10, 1, 3), 'hay')
        d.place('game:toolrack-west', (12, 2, 4)) if d.free((12, 2, 4)) else None
        d.put('game:stationarybasket-north', [(2, 1, 8)])
        rock_piles(d, [(1, 1), (2, 1), (1, 2)], 0)
        d.torch((7, 3, 3), 'south'); d.torch((5, -7, 6), 'south')
        grass(d, (0, 0, 12, 10), -1, every=6)
    elif L == 2:
        # A pit house: a daub house built right over the shaft with a windlass beam under the roof, an ore
        # shed of cobble against one end, the miners' two beds, a wattle fence.
        box = (2, 1, 10, 9)
        yard(d, (0, 0, 18, 12))
        house(d, box, storeys=1, height=4, axis=1, level=2, no_windows=('east',))
        d.door((6, 1, 9), 'crude', 'north')
        shaft(d, 6, 5, -12, 0, 'north', COBBLE, mouth=COBBLE)
        for z in range(3, 8): d.set((6, 4, z), LOG_Z)
        hut_block(d, (9, 1, 8))
        d.beds(2, [(3, 1, 2), (9, 1, 2)], 'rough', 'south'); d.put_against('game:chest', [(3, 1, 8)]); d.place('game:toolrack-west', (3, 2, 5))
        store_shed(d, (10, 3, 15, 8), 1, 3, 'west', COBBLE, 2, door=((15, 1, 5), 'west'))
        rock_piles(d, [(11, 4), (13, 4), (11, 7), (13, 7)], 1)
        d.torch((6, 3, 8), 'south'); d.torch((6, -3, 6), 'north'); d.torch((6, -11, 6), 'north')
        d.fence((1, 0, 17, 11), 0, 'wattle-sticks', gaps=[(6, 11), (7, 11)])
        grass(d, (0, 0, 18, 12), -1, every=8)
    elif L == 3:
        # A mine house: a rammed-earth hall with a stone-lined shaft room at one end under a tall timber
        # headframe that breaks the roof, a cross lean-to bunk room, a tally desk, ore bins and an oak fence.
        hall = (2, 1, 14, 9); bunk = (14, 2, 20, 8)
        yard(d, (0, 0, 24, 14))
        house(d, hall, storeys=1, height=4, axis=0, level=3, no_windows=('east',))
        d.door((8, 1, 9), 'solid-oak', 'north')
        for x in range(3, 8):
            for z in range(2, 9): d.set((x, 0, z), COBBLE)
        shaft(d, 5, 5, -16, 0, 'north', COBBLE, mouth=COBBLE)
        for z in (3, 7):
            for y in range(1, 9): d.set((3, y, z), LOG); d.set((7, y, z), LOG)
        for z in range(3, 8): d.set((3, 9, z), LOG_Z); d.set((7, 9, z), LOG_Z)
        for x in range(3, 8): d.set((x, 9, 5), LOG_X)
        for y in range(5, 9):
            for (x, z) in ((3, 5), (7, 5), (5, 3), (5, 7)): d.set((x, y, z), None)
        hut_block(d, (13, 1, 8))
        d.put('w4rd0sfurniture:roughtable-north', [(10, 1, 2)]); d.put('w4rd0sfurniture:roughstool-west', [(10, 1, 3)]); d.place('game:toolrack-west', (13, 2, 3)); d.place('game:toolrack-west', (13, 2, 4))
        d.put('game:crate', [(10, 1, 7), (12, 1, 7)])
        lean_to(d, bunk, 1, 3, 'west', level=3, floor=PLANK_X)
        opening(d, [(14, 5)], 1, 2)
        d.beds(3, [(16, 1, 3), (18, 1, 3), (19, 1, 6)], 'rough', 'south'); d.put_against('game:chest', [(15, 1, 7)])
        rock_piles(d, [(x, z) for x in (3, 5, 7) for z in (11, 12)], 0)
        d.lantern((10, 3, 5)); d.lantern((17, 3, 5)); d.lantern((8, 3, 10), 'south'); d.lantern((5, -15, 6), 'north'); d.lantern((5, -7, 6), 'north')
        d.fence((1, 0, 23, 13), 0, 'woodenfence-oak', gaps=[(8, 13), (9, 13)])
        d.path([(x, z) for x in (8, 9) for z in range(10, 14)], -1)
        grass(d, (0, 0, 24, 14), -1, every=10)
    elif L == 4:
        # A colliery: a two-storey cobble-and-plank mine office (the miners sleep above), a square timber
        # headframe tower over a cobble-lined shaft, a cart shed, a lamp room, ore yard and oak fence.
        office = (2, 1, 12, 11); tow = (14, 3, 20, 9); carts = (22, 2, 28, 10)
        yard(d, (0, 0, 30, 16))
        house(d, office, storeys=2, height=3, axis=1, level=4, no_windows=('east',))
        d.door((7, 1, 11), 'solid-oak', 'north')
        hut_block(d, (11, 1, 10))
        d.put('w4rd0sfurniture:roughtable-north', [(3, 1, 3), (3, 1, 5)]); d.put('w4rd0sfurniture:roughstool-west', [(4, 1, 3)]); d.put('game:bookshelf', [(3, 1, 9)]); d.put_against('game:chest', [(11, 1, 3), (11, 1, 5)])
        for z in (3, 4, 5, 6): d.place('game:toolrack-west', (11, 2, z + 3)) if z < 5 else None
        d.stair_run((10, 1, 9), 'west', 3, 'planks')
        for x in (7, 8, 9, 10): d.set((x, 4, 9), None)
        d.beds(5, [(3, 5, 3), (3, 5, 6), (11, 5, 3), (11, 5, 6), (3, 5, 9)], 'wood', 'east'); d.put_against('game:chest', [(11, 5, 9)])
        d.floor((14, 3, 20, 9), 0, COBBLE)
        tower(d, tow, 1, 10, lambda x, y, z, a: (COBBLE if y <= 2 else PLANK_Z if a else PLANK_X) if (y <= 2 or y >= 8) else None, roof='hip', post_every=3, posts=True)
        shaft(d, 17, 6, -22, 0, 'north', COBBLE, mouth=COBBLE)
        for z in range(4, 9): d.set((17, 8, z), LOG_Z)
        opening(d, [(12, 6)], 1, 2); opening(d, [(14, 6)], 1, 2)
        for x in (13,): d.set((x, 0, 6), COBBLE)
        store_shed(d, carts, 1, 3, 'east', COBBLE, 4)
        opening(d, [(22, 5), (22, 6), (22, 7)], 1, 2)
        for x in (24, 26): d.put('game:crate', [(x, 1, 4), (x, 1, 8)])
        rock_piles(d, [(x, z) for x in range(23, 28) for z in (12, 13)], 0)
        d.lantern((7, 3, 6)); d.lantern((7, 7, 6)); d.lantern((17, 3, 4), 'north'); d.lantern((25, 3, 6)); d.lantern((7, 3, 12), 'south'); banner(d, (9, 3, 12), 'south')
        for y in (-20, -12, -4): d.lantern((17, y, 7), 'south')
        d.ground((2, 12, 28, 14), -1, COBBLE)
        d.fence((1, 0, 29, 15), 0, 'woodenfence-oak', gaps=[(7, 15), (8, 15), (29, 12), (29, 13)])
        d.path([(x, z) for x in (7, 8) for z in (13, 14, 15)], -1)
        grass(d, (0, 0, 30, 16), -1, every=11)
    else:
        # A deep mine: a stone-brick pithead hall with the winding gear in a masonry headframe tower, a mud-
        # brick upper floor of bunk rooms, a smithy wing for the picks, a lamp store, a walled ore yard with
        # a crane and a flag.
        hall = (2, 1, 16, 11); tow = (16, 2, 24, 10); smithy = (26, 2, 32, 10)
        yard(d, (0, 0, 36, 20))
        house(d, hall, storeys=2, height=3, axis=0, level=5, no_windows=('east',))
        d.door((9, 1, 11), 'solid-oak', 'north')
        d.floor_inside(hall, 0, POLISHED)
        hut_block(d, (15, 1, 10))
        d.put('w4rd0sfurniture:rusticsturdytabledouble-east', [(4, 1, 3)]) or d.put('w4rd0sfurniture:roughtable-north', [(4, 1, 3)]); d.put('game:bookshelf', [(3, 1, 8), (3, 1, 9)]); d.put('game:crate', [(13, 1, 3), (15, 1, 3), (13, 1, 5)])
        d.place('game:toolrack-west', (3, 2, 5)); d.place('game:toolrack-west', (3, 2, 6))
        d.stair_run((14, 1, 9), 'west', 3, 'brick')
        for x in (11, 12, 13, 14): d.set((x, 4, 9), None)
        d.beds(8, [(3, 5, 3), (3, 5, 6), (3, 5, 9), (7, 5, 3), (11, 5, 3), (15, 5, 3), (15, 5, 6), (7, 5, 9)], 'rustic', 'east'); d.put('w4rd0sfurniture:rusticwardrobe-east', [(11, 5, 9)])
        stone_tower(d, tow, 1, 13, roof='hip', floors_every=0, top_layers=3)
        d.floor_inside(tow, 0, POLISHED)
        shaft(d, 20, 6, -30, 0, 'north', BRICK, mouth=POLISHED)
        for z in range(4, 9): d.set((20, 11, z), LOG_Z)
        for x in range(18, 23): d.set((x, 11, 6), LOG_X) if x != 20 else None
        opening(d, [(16, 5), (16, 6), (16, 7)], 1, 2)
        house(d, smithy, storeys=1, height=3, axis=1, level=5, no_windows=('west',))
        opening(d, [(24, 6), (26, 6)], 1, 2); d.set((25, 0, 6), POLISHED)
        d.place('game:forge', (28, 1, 3)); d.place('game:anvil-copper', (29, 1, 5)); d.put('game:barrel', [(31, 1, 4)]); d.place('game:toolrack-west', (31, 2, 8)); chimney(d, 28, 2, 4, 8, BRICK)
        for y in (1, 2, 3): d.set((28, y, 2), BRICK)
        crane(d, 8, 15, 0, 6, 5, 'x', POLISHED)
        rock_piles(d, [(x, z) for x in range(10, 16) for z in (14, 15, 16)], 0)
        d.put('game:crate', [(18, 0, 14), (20, 0, 14), (22, 0, 14)])
        d.ground((2, 12, 34, 17), -1, COBBLE); flag(d, (33, 0, 13))
        for p in ((9, 3, 6), (9, 7, 6), (20, 3, 4), (20, 9, 4), (29, 3, 6)): d.lantern(p)
        for y in (-28, -22, -16, -10, -4): d.lantern((20, y, 7), 'south')
        d.lantern((9, 2, 12), 'south'); banner(d, (6, 3, 12), 'south'); banner(d, (12, 3, 12), 'south')
        d.fence((1, 0, 35, 19), 0, 'drystonefence-shale', gaps=[(9, 19), (10, 19), (35, 14), (35, 15)])
        d.path([(x, z) for x in (9, 10) for z in (18, 19)], -1)
        grass(d, (0, 0, 36, 20), -1, every=12)
    return d


# ---------------------------------------------------------------- Quarry
@recipe('quarry')
def quarry(d):
    L = d.level
    if L == 1:
        # A splitting camp: an open lean-to with a splitting bench, stone piles, a hay bed under the back wall,
        # a firepit; the pit is marked out beyond the north side.
        box = (1, 1, 8, 5)
        yard(d, (0, 0, 12, 9))
        lean_to(d, box, 1, 3, 'south', open_sides=('north',), level=1, floor=DIRT)
        hut_block(d, (7, 1, 4))
        d.bed((2, 1, 4), 'hay', 'east') or d.bed((2, 1, 3), 'hay')
        d.put('w4rd0sfurniture:roughtable-north', [(5, 1, 4)]); d.place('game:toolrack-west', (7, 2, 2)) if d.free((7, 2, 2)) else None
        rock_piles(d, [(10, 2), (11, 2), (10, 3), (11, 4), (10, 7)], 0); rock_piles(d, [(10, 2)], 1)
        d.place('game:firepit-cold', (4, 0, 7))
        d.torch((4, 2, 5), 'north')
        grass(d, (0, 0, 12, 9), -1, every=6)
        quarry_point(d)
    elif L == 2:
        # A quarry hut: a daub cottage for the quarrymen with a cobble sill and two beds, an open dressing
        # shed on posts, a timber crane on a stone base, dressed-stone stacks, a wattle fence.
        box = (2, 1, 8, 7); shed = (11, 1, 17, 7)
        yard(d, (0, 0, 20, 12))
        house(d, box, storeys=1, height=3, axis=1, level=2)
        d.door((5, 1, 7), 'crude', 'north')
        hut_block(d, (7, 1, 6))
        d.beds(2, [(3, 1, 2), (7, 1, 2)], 'rough', 'south'); d.put_against('game:chest', [(3, 1, 6)]); d.place('game:toolrack-west', (3, 2, 4))
        lean_to(d, shed, 1, 3, 'north', open_sides=('south', 'east'), level=2, wall=None, floor=COBBLE)
        d.put('w4rd0sfurniture:roughtable-north', [(13, 1, 2), (15, 1, 2)])
        rock_piles(d, [(12, 5), (14, 5), (16, 5)], 1)
        crane(d, 18, 9, 0, 5, 3, 'x')
        rock_piles(d, [(x, z) for x in range(10, 16) for z in (9, 10)], 0)
        d.torch((5, 2, 8), 'south'); d.torch((14, 2, 1), 'south')
        d.fence((1, 0, 19, 11), 0, 'wattle-sticks', gaps=[(5, 11), (6, 11), (8, 0), (9, 0)])
        grass(d, (0, 0, 20, 12), -1, every=8)
        quarry_point(d)
    elif L == 3:
        # A quarry lodge: a rammed-earth lodge with a cross-wing bunk room, a long open dressing shed on posts
        # with a slate rack, two cranes, a stone-flagged loading bay, oak fence and path.
        lodge = (2, 1, 12, 9); wing = (12, 3, 18, 9); shed = (2, 12, 18, 16)
        yard(d, (0, 0, 26, 18))
        house(d, lodge, storeys=1, height=4, axis=0, level=3, no_windows=('east',))
        house(d, wing, storeys=1, height=3, axis=0, level=3, no_windows=('west',))
        opening(d, [(12, 6)], 1, 2)
        d.door((7, 1, 9), 'solid-oak', 'north')
        hut_block(d, (11, 1, 8))
        d.put('w4rd0sfurniture:roughtable-north', [(4, 1, 2)]); d.put('w4rd0sfurniture:roughstool-west', [(4, 1, 3)]); d.put('game:bookshelf', [(3, 1, 5)]); d.place('game:toolrack-west', (11, 2, 3)); d.place('game:toolrack-west', (11, 2, 4)); d.put('game:crate', [(3, 1, 8)])
        d.beds(3, [(14, 1, 4), (16, 1, 4), (17, 1, 7)], 'rough', 'south'); d.put_against('game:chest', [(13, 1, 8)])
        lean_to(d, shed, 1, 3, 'south', open_sides=('north',), level=3, wall=PLANK_X, floor=COBBLE)
        for x in range(3, 18, 2): rock_piles(d, [(x, 15)], 1)
        d.put('w4rd0sfurniture:roughtable-north', [(5, 1, 13), (10, 1, 13), (15, 1, 13)])
        crane(d, 21, 4, 0, 6, 4, 'z'); crane(d, 21, 14, 0, 6, 4, 'z')
        d.ground((19, 2, 25, 16), -1, COBBLE)
        rock_piles(d, [(x, z) for x in (22, 23, 24) for z in (6, 7, 8, 10, 11, 12)], 0)
        d.lantern((7, 3, 5)); d.lantern((15, 3, 6)); d.lantern((10, 3, 14)); d.lantern((7, 3, 10), 'south')
        d.fence((1, 0, 25, 17), 0, 'woodenfence-oak', gaps=[(7, 0), (8, 0), (25, 8), (25, 9)])
        d.path([(x, z) for x in (7, 8) for z in (10, 11)] + [(x, 11) for x in range(9, 20)], -1)
        grass(d, (0, 0, 26, 18), -1, every=10)
        quarry_point(d)
    elif L == 4:
        # A stone works: a two-storey cobble-and-plank quarry house, a long dressing barn with a saw bench and
        # a lifting beam down its length, a banked stone store, three cranes, a flagged yard and oak fence.
        home = (2, 1, 12, 11); barn = (14, 1, 30, 9)
        yard(d, (0, 0, 34, 20))
        house(d, home, storeys=2, height=3, axis=1, level=4, no_windows=('east',))
        d.door((7, 1, 11), 'solid-oak', 'north')
        hut_block(d, (11, 1, 10))
        d.put('w4rd0sfurniture:roughtable-north', [(3, 1, 3)]); d.put('w4rd0sfurniture:roughstool-west', [(4, 1, 3)]); d.put('game:bookshelf', [(3, 1, 7)]); d.put_against('game:chest', [(11, 1, 3)]); d.place('game:toolrack-west', (11, 2, 5)); d.place('game:toolrack-west', (11, 2, 6))
        d.stair_run((10, 1, 9), 'west', 3, 'planks')
        for x in (7, 8, 9, 10): d.set((x, 4, 9), None)
        d.beds(5, [(3, 5, 3), (3, 5, 6), (11, 5, 3), (11, 5, 6), (3, 5, 9)], 'wood', 'east')
        house(d, barn, storeys=1, height=4, axis=0, level=4, walls=dict(WALLS[4], low=lambda x, y, z, a: COBBLE if y == 1 else None, gable=PLANK_Z), post_every=4, window_sides=())
        d.floor_inside(barn, 0, COBBLE)
        for x in range(16, 29): d.set((x, 4, 5), LOG_X)
        d.put('w4rd0sfurniture:roughtable-north', [(x, 1, 3) for x in (16, 20, 24, 28)])
        for x in range(16, 29, 3): rock_piles(d, [(x, 7)], 1)
        for (x, z) in ((4, 15), (14, 15), (26, 15)): crane(d, x, z, 0, 6, 4, 'x')
        d.ground((2, 12, 32, 18), -1, COBBLE)
        rock_piles(d, [(x, z) for x in range(6, 12) for z in (16, 17)] + [(x, z) for x in range(16, 24) for z in (16, 17)], 0)
        rock_piles(d, [(x, z) for x in range(8, 11) for z in (16, 17)], 1)
        d.lantern((7, 3, 6)); d.lantern((7, 7, 6)); d.lantern((18, 3, 5)); d.lantern((26, 3, 5)); d.lantern((7, 3, 12), 'south'); banner(d, (9, 3, 12), 'south')
        d.fence((1, 0, 33, 19), 0, 'woodenfence-oak', gaps=[(7, 0), (8, 0), (33, 14), (33, 15)])
        d.path([(x, z) for x in (7, 8) for z in range(12, 14)] + [(x, 13) for x in range(9, 33)], -1)
        grass(d, (0, 0, 34, 20), -1, every=11)
        quarry_point(d)
    else:
        # A master quarry: a stone-brick quarry hall with the master's office and a mud-brick upper floor for
        # the men, a masonry crane tower with the lifting beam out of its top, a long dressing barn, a stone
        # store on a polished footing, drystone walls, flags and lanterns.
        hall = (2, 1, 16, 11); tow = (16, 3, 22, 9); barn = (24, 1, 40, 9); store = (26, 12, 36, 16)
        yard(d, (0, 0, 42, 22))
        house(d, hall, storeys=2, height=3, axis=0, level=5, no_windows=('east',))
        d.door((9, 1, 11), 'solid-oak', 'north')
        d.floor_inside(hall, 0, POLISHED)
        hut_block(d, (15, 1, 10))
        d.put('w4rd0sfurniture:rusticsturdytabledouble-east', [(4, 1, 3)]) or d.put('w4rd0sfurniture:roughtable-north', [(4, 1, 3)]); d.put('game:bookshelf', [(3, 1, 7), (3, 1, 8)]); d.put('w4rd0sfurniture:rusticcabinetlarge-oak-east', [(15, 1, 3)]); d.put('game:crate', [(13, 1, 3), (15, 1, 5)])
        d.stair_run((14, 1, 9), 'west', 3, 'brick')
        for x in (11, 12, 13, 14): d.set((x, 4, 9), None)
        d.beds(7, [(3, 5, 3), (3, 5, 6), (3, 5, 9), (7, 5, 3), (11, 5, 3), (15, 5, 3), (15, 5, 6)], 'rustic', 'east'); d.put('w4rd0sfurniture:rusticwardrobe-east', [(9, 5, 9)])
        stone_tower(d, tow, 1, 11, roof='flat', floors_every=4, ladder=(21, 'north', 8))
        opening(d, [(16, 6)], 1, 2)
        for x in range(19, 28): d.set((x, 12, 6), LOG_X)
        d.set((27, 11, 6), fence_code('woodenfence-oak', 'ns'))
        house(d, barn, storeys=1, height=4, axis=0, level=5, walls=dict(WALLS[5], low=lambda x, y, z, a: BRICK if y == 1 else None, gable=PLANK_Z), post_every=4, window_sides=())
        d.floor_inside(barn, 0, COBBLE)
        for x in range(26, 39): d.set((x, 4, 5), LOG_X)
        d.put('w4rd0sfurniture:roughtable-north', [(x, 1, 3) for x in (26, 30, 34, 38)])
        for x in range(26, 39, 3): rock_piles(d, [(x, 7)], 1)
        store_shed(d, store, 1, 3, 'south', BRICK, 5)
        opening(d, [(28, 12), (30, 12), (32, 12), (34, 12)], 1, 2)
        rock_piles(d, [(x, z) for x in range(27, 36) for z in (14, 15)], 1)
        for (x, z) in ((6, 16), (18, 16)): crane(d, x, z, 0, 7, 5, 'x', POLISHED)
        d.ground((2, 12, 40, 19), -1, COBBLE); flag(d, (39, 0, 20)); flag(d, (3, 0, 20))
        rock_piles(d, [(x, z) for x in range(8, 16) for z in (17, 18)], 0); rock_piles(d, [(x, z) for x in range(10, 14) for z in (17, 18)], 1)
        for p in ((9, 3, 6), (9, 7, 6), (19, 3, 6), (19, 7, 6), (28, 3, 5), (36, 3, 5), (31, 3, 14)): d.lantern(p)
        d.lantern((9, 2, 12), 'south'); banner(d, (6, 3, 12), 'south'); banner(d, (12, 3, 12), 'south')
        d.fence((1, 0, 41, 21), 0, 'drystonefence-shale', gaps=[(9, 0), (10, 0), (41, 14), (41, 15)])
        d.path([(x, z) for x in (9, 10) for z in (12, 13)] + [(x, 13) for x in range(11, 41)], -1)
        grass(d, (0, 0, 42, 22), -1, every=12)
        quarry_point(d)
    return d


# ---------------------------------------------------------------- Prospector
@recipe('prospector')
def prospector(d):
    L = d.level
    if L == 1:
        # A prospector's camp: a wattle lean-to with a hay bed, a firepit, sample baskets, a pan rack and a
        # marker post with a torch.
        box = (1, 1, 5, 5)
        yard(d, (0, 0, 10, 8))
        lean_to(d, box, 1, 3, 'north', open_sides=('south',), level=1, floor=DIRT)
        hut_block(d, (4, 1, 4))
        d.bed((2, 1, 2), 'hay', 'east') or d.bed((2, 1, 3), 'hay')
        d.put('game:stationarybasket-north', [(4, 1, 2)]); d.place('game:toolrack-west', (2, 2, 4)) if d.free((2, 2, 4)) else None
        d.place('game:firepit-cold', (3, 0, 7))
        for y in (0, 1, 2): d.set((8, y, 2), LOG)
        d.torch((8, 3, 2))
        rock_piles(d, [(7, 6), (8, 6)], 0)
        grass(d, (0, 0, 10, 8), -1, every=6)
    elif L == 2:
        # A prospector's cabin: a daub cabin with a map table under the window, a sample shelf, a rough bed, a
        # cobble hearth, a wattle-fenced plot with sample pits.
        box = (2, 1, 8, 7)
        yard(d, (0, 0, 16, 10))
        house(d, box, storeys=1, height=3, axis=1, level=2)
        d.door((5, 1, 7), 'crude', 'north')
        hut_block(d, (7, 1, 6))
        d.bed((3, 1, 2), 'rough', 'south'); d.put('w4rd0sfurniture:roughtable-north', [(7, 1, 2)]); d.put('w4rd0sfurniture:roughstool-west', [(6, 1, 2)]); d.put('w4rd0sfurniture:roughstandingshelf-north', [(3, 1, 5)]) or d.put_against('game:chest', [(3, 1, 5)])
        for y in (1, 2): d.set((2, y, 4), COBBLE)
        chimney(d, 2, 4, 3, 6, COBBLE); d.place('game:firepit-cold', (3, 1, 4))
        for (x, z) in ((11, 2), (14, 3), (12, 6), (14, 8)):
            d.set((x, -1, z), None); d.set((x, -2, z), ROCK)
        rock_piles(d, [(11, 4), (13, 5)], 0)
        d.torch((5, 2, 8), 'south')
        d.fence((1, 0, 15, 9), 0, 'wattle-sticks', gaps=[(5, 9), (6, 9)])
        grass(d, (0, 0, 16, 10), -1, every=8)
    elif L == 3:
        # An assay house: a rammed-earth house with a sample room and a small assay furnace under a stone
        # chimney, a cross lean-to for the prospectors' beds, a sample yard of marked pits, oak fence.
        box = (2, 1, 12, 9); bunk = (12, 2, 18, 8)
        yard(d, (0, 0, 26, 14))
        house(d, box, storeys=1, height=4, axis=0, level=3, no_windows=('east',))
        d.door((7, 1, 9), 'solid-oak', 'north')
        hut_block(d, (11, 1, 8))
        for y in (1, 2, 3): d.set((3, y, 2), COBBLE); d.set((4, y, 2), COBBLE)
        chimney(d, 3, 2, 4, 9, COBBLE); d.place('game:forge', (4, 1, 3)); d.put('game:barrel', [(6, 1, 2)])
        d.put('w4rd0sfurniture:roughtable-north', [(9, 1, 2)]); d.put('w4rd0sfurniture:roughstool-west', [(9, 1, 3)]); d.put('game:bookshelf', [(11, 1, 2)]); d.put('w4rd0sfurniture:roughstandingshelf-north', [(3, 1, 7), (5, 1, 7)]) or d.put('game:crate', [(3, 1, 7)])
        lean_to(d, bunk, 1, 3, 'west', level=3, floor=PLANK_X)
        opening(d, [(12, 5)], 1, 2)
        d.beds(3, [(14, 1, 3), (16, 1, 3), (17, 1, 6)], 'rough', 'south'); d.put_against('game:chest', [(13, 1, 7)])
        for (x, z) in ((20, 2), (23, 4), (21, 7), (24, 9), (20, 11)):
            d.set((x, -1, z), None); d.set((x, -2, z), ROCK); d.set((x + 1, 0, z), fence_code('woodenfence-oak', 'ns'))
        d.lantern((7, 3, 5)); d.lantern((15, 3, 5)); d.lantern((7, 3, 10), 'south')
        d.fence((1, 0, 25, 13), 0, 'woodenfence-oak', gaps=[(7, 13), (8, 13)])
        d.path([(x, z) for x in (7, 8) for z in range(10, 14)], -1)
        grass(d, (0, 0, 26, 14), -1, every=10)
    elif L == 4:
        # An assay office: two storeys of cobble and plank with the furnace room and sample vault below and
        # the prospectors' quarters above, a timber lookout post on the yard, a core store, oak fence.
        office = (2, 1, 12, 11); core = (14, 2, 20, 8)
        yard(d, (0, 0, 28, 16))
        house(d, office, storeys=2, height=3, axis=1, level=4, no_windows=('east',))
        d.door((7, 1, 11), 'solid-oak', 'north')
        hut_block(d, (11, 1, 10))
        for y in (1, 2, 3): d.set((3, y, 2), BRICK); d.set((4, y, 2), BRICK)
        chimney(d, 3, 2, 4, 11, BRICK); d.place('game:forge', (4, 1, 3)); d.put('game:barrel', [(6, 1, 2)])
        d.put('w4rd0sfurniture:roughtable-north', [(9, 1, 2), (3, 1, 6)]); d.put('w4rd0sfurniture:roughstool-west', [(9, 1, 3)]); d.put('game:bookshelf', [(11, 1, 2), (11, 1, 4)]); d.put('game:crate', [(3, 1, 9)])
        d.stair_run((10, 1, 9), 'west', 3, 'planks')
        for x in (7, 8, 9, 10): d.set((x, 4, 9), None)
        d.beds(4, [(3, 5, 3), (3, 5, 6), (11, 5, 3), (11, 5, 6)], 'wood', 'east'); d.put_against('game:chest', [(3, 5, 9)]); d.put('w4rd0sfurniture:roughtable-north', [(7, 5, 3)])
        store_shed(d, core, 1, 3, 'west', COBBLE, 4, door=((14, 1, 5), 'east'))
        d.put('game:crate', [(16, 1, 3), (18, 1, 3), (16, 1, 7), (18, 1, 7)]); d.put('game:stationarybasket-north', [(19, 1, 5)])
        tower(d, (22, 10, 26, 14), 1, 7, lambda x, y, z, a: None if y > 1 else COBBLE, roof=None, posts=True, floors_every=0)
        d.floor_inside((22, 10, 26, 14), 7, PLANK_X)
        for (x, z) in ring((22, 10, 26, 14)): d.set((x, 8, z), fence_code('woodenfence-oak', 'ew' if z in (10, 14) else 'ns'))
        d.ladder(24, 10, 1, 7, 'north'); d.set((24, 7, 10), None); d.set((24, 8, 10), None)
        d.torch((22, 9, 10)); d.torch((26, 9, 14))
        d.lantern((7, 3, 6)); d.lantern((7, 7, 6)); d.lantern((17, 3, 5)); d.lantern((7, 3, 12), 'south'); banner(d, (9, 3, 12), 'south')
        d.fence((1, 0, 27, 15), 0, 'woodenfence-oak', gaps=[(7, 15), (8, 15)])
        d.path([(x, z) for x in (7, 8) for z in range(12, 16)], -1)
        grass(d, (0, 0, 28, 16), -1, every=11)
    else:
        # A prospectors' guild: a stone-brick assay hall with two furnaces, a library of survey books and a
        # mud-brick upper floor of chambers, a masonry survey tower with an open top for sighting, a core
        # vault, drystone walls, flags.
        hall = (2, 1, 16, 11); tow = (16, 3, 22, 9); vault = (24, 2, 30, 10)
        yard(d, (0, 0, 34, 18))
        house(d, hall, storeys=2, height=3, axis=0, level=5, no_windows=('east',))
        d.door((9, 1, 11), 'solid-oak', 'north')
        d.floor_inside(hall, 0, POLISHED)
        hut_block(d, (15, 1, 10))
        for x in range(3, 8):
            for y in (1, 2, 3): d.set((x, y, 2), POLISHED)
        chimney(d, 4, 2, 4, 11, BRICK); chimney(d, 6, 2, 4, 11, BRICK); d.place('game:forge', (4, 1, 3)); d.place('game:forge', (6, 1, 3)); d.put('game:barrel', [(8, 1, 2)])
        d.put('game:bookshelf', [(11, 1, 2), (13, 1, 2), (15, 1, 2), (15, 1, 4)]); d.put('w4rd0sfurniture:rusticsturdytabledouble-east', [(10, 1, 6)]) or d.put('w4rd0sfurniture:roughtable-north', [(10, 1, 6)]); d.put('w4rd0sfurniture:rusticcabinetlarge-oak-east', [(3, 1, 8)]); d.put('game:crate', [(3, 1, 6)])
        d.stair_run((14, 1, 9), 'west', 3, 'brick')
        for x in (11, 12, 13, 14): d.set((x, 4, 9), None)
        d.beds(6, [(3, 5, 3), (3, 5, 6), (3, 5, 9), (15, 5, 3), (15, 5, 6), (9, 5, 3)], 'rustic', 'east'); d.put('w4rd0sfurniture:rusticwardrobe-east', [(11, 5, 9)]); d.put('game:bookshelf', [(7, 5, 9)])
        stone_tower(d, tow, 1, 13, roof='flat', floors_every=4, ladder=(21, 'north', 8), top_layers=2)
        opening(d, [(16, 6)], 1, 2); opening(d, [(16, 6)], 5, 6)
        d.place('game:firepit-cold', (19, 14, 6))
        store_shed(d, vault, 1, 3, 'west', BRICK, 5, door=((24, 1, 6), 'east'))
        d.put('game:crate', [(26, 1, 3), (28, 1, 3), (26, 1, 9), (28, 1, 9)]); d.put('w4rd0sfurniture:roughstandingshelf-north', [(29, 1, 6)]) or None
        for (x, z) in ((5, 14), (11, 15), (20, 14), (27, 15)):
            d.set((x, -1, z), None); d.set((x, -2, z), ROCK); d.set((x + 1, 0, z), fence_code('woodenfence-oak', 'ns'))
        d.ground((2, 12, 32, 13), -1, COBBLE); flag(d, (31, 0, 16))
        for p in ((9, 3, 6), (9, 7, 6), (19, 3, 6), (19, 7, 6), (19, 11, 6), (27, 3, 6)): d.lantern(p)
        d.lantern((9, 2, 12), 'south'); banner(d, (6, 3, 12), 'south'); banner(d, (12, 3, 12), 'south')
        d.fence((1, 0, 33, 17), 0, 'drystonefence-shale', gaps=[(9, 17), (10, 17)])
        d.path([(x, z) for x in (9, 10) for z in range(12, 18)], -1)
        grass(d, (0, 0, 34, 18), -1, every=12)
    return d


# ---------------------------------------------------------------- Explorer
@recipe('explorer')
def explorer(d):
    L = d.level
    if L == 1:
        # A lookout: a plank platform on four tall posts with a wattle parapet and a ladder, a torch on top,
        # the explorer's hay bed in a lean-to between the legs.
        yard(d, (0, 0, 10, 10))
        box = (3, 3, 7, 7)
        for (x, z) in ((3, 3), (7, 3), (3, 7), (7, 7)):
            for y in range(0, 7): d.set((x, y, z), LOG)
        d.floor(box, 7, PLANK_X)
        for (x, z) in ring(box):
            if (x, z) not in ((3, 3), (7, 3), (3, 7), (7, 7)): d.set((x, 8, z), fence_code('wattle-sticks', 'ew' if z in (3, 7) else 'ns'))
        for (x, z) in ((3, 3), (7, 3), (3, 7), (7, 7)): d.set((x, 8, z), LOG)
        d.ladder(5, 3, 0, 7, 'north'); d.set((5, 7, 3), None); d.set((5, 8, 3), None)
        d.torch((7, 9, 7))
        d.floor((3, 3, 7, 7), 0, DIRT)
        for z in range(4, 7): d.set((3, 1, z), fence_code('wattle-sticks', 'ns')); d.set((3, 2, z), fence_code('wattle-sticks', 'ns'))
        hut_block(d, (6, 1, 6))
        d.bed((4, 1, 4), 'hay', 'south') or d.bed((4, 1, 5), 'hay')
        d.put('game:stationarybasket-north', [(6, 1, 4)])
        d.place('game:firepit-cold', (5, 0, 9))
        grass(d, (0, 0, 10, 10), -1, every=6)
    elif L == 2:
        # An explorer's cabin: a daub cabin with a map table, a rough bed and a cobble hearth, a timber
        # watchtower rising from its yard corner with a plank deck and a torch, a wattle fence.
        box = (2, 1, 8, 7)
        yard(d, (0, 0, 16, 10))
        house(d, box, storeys=1, height=3, axis=1, level=2)
        d.door((5, 1, 7), 'crude', 'north')
        hut_block(d, (7, 1, 6))
        d.bed((3, 1, 2), 'rough', 'south'); d.put('w4rd0sfurniture:roughtable-north', [(7, 1, 2)]); d.put('w4rd0sfurniture:roughstool-west', [(6, 1, 2)]); d.put_against('game:chest', [(3, 1, 5)])
        for y in (1, 2): d.set((2, y, 4), COBBLE)
        chimney(d, 2, 4, 3, 6, COBBLE); d.place('game:firepit-cold', (3, 1, 4))
        tb = (10, 2, 14, 6)
        tower(d, tb, 1, 8, lambda x, y, z, a: None if y > 1 else COBBLE, roof=None, posts=True, floors_every=0)
        d.floor_inside(tb, 8, PLANK_X); d.floor(tb, 0, COBBLE)
        for (x, z) in ring(tb):
            if (x, z) not in ((10, 2), (14, 2), (10, 6), (14, 6)): d.set((x, 9, z), fence_code('wattle-sticks', 'ew' if z in (2, 6) else 'ns'))
        for (x, z) in ((10, 2), (14, 2), (10, 6), (14, 6)): d.set((x, 9, z), LOG)
        d.ladder(12, 2, 1, 8, 'north'); d.set((12, 8, 2), None); d.set((12, 9, 2), None)
        d.torch((14, 10, 6)); d.torch((5, 2, 8), 'south')
        d.fence((1, 0, 15, 9), 0, 'wattle-sticks', gaps=[(5, 9), (6, 9)])
        grass(d, (0, 0, 16, 10), -1, every=8)
    elif L == 3:
        # An explorers' lodge: a rammed-earth lodge with a map room, three beds in a cross lean-to, and a
        # square timber watchtower with daub panels and a hip roof over an open sighting deck, oak fence.
        lodge = (2, 1, 12, 9); bunk = (12, 2, 18, 8); tb = (20, 2, 26, 8)
        yard(d, (0, 0, 28, 14))
        house(d, lodge, storeys=1, height=4, axis=0, level=3, no_windows=('east',))
        d.door((7, 1, 9), 'solid-oak', 'north')
        hut_block(d, (11, 1, 8))
        d.put('w4rd0sfurniture:roughtable-north', [(5, 1, 2), (7, 1, 2)]); d.put('w4rd0sfurniture:roughstool-west', [(6, 1, 3)]); d.put('game:bookshelf', [(3, 1, 2), (3, 1, 4)]); d.put('game:crate', [(10, 1, 2)]); d.place('game:toolrack-west', (11, 2, 4))
        for y in (1, 2, 3): d.set((3, y, 6), COBBLE)
        chimney(d, 3, 6, 4, 9, COBBLE); d.place('game:firepit-cold', (4, 1, 6))
        lean_to(d, bunk, 1, 3, 'west', level=3, floor=PLANK_X)
        opening(d, [(12, 5)], 1, 2)
        d.beds(3, [(14, 1, 3), (16, 1, 3), (17, 1, 6)], 'rough', 'south'); d.put_against('game:chest', [(13, 1, 7)])
        w = wall_fn(3)
        tower(d, tb, 1, 9, lambda x, y, z, a: COBBLE if y == 1 else (w(x, y, z, a) if y <= 6 else None), roof='hip', post_every=3, posts=True, floors_every=3, ladder=(23, 'north', 2))
        d.floor(tb, 0, COBBLE)
        for (x, z) in ring(tb):
            if d.free((x, 7, z)): d.set((x, 7, z), fence_code('woodenfence-oak', 'ew' if z in (2, 8) else 'ns'))
        d.lantern((7, 3, 5)); d.lantern((15, 3, 5)); d.lantern((23, 5, 5)); d.lantern((23, 8, 5)); d.lantern((7, 3, 10), 'south')
        d.fence((1, 0, 27, 13), 0, 'woodenfence-oak', gaps=[(7, 13), (8, 13)])
        d.path([(x, z) for x in (7, 8) for z in range(10, 14)], -1)
        grass(d, (0, 0, 28, 14), -1, every=10)
    elif L == 4:
        # An expedition house: two storeys of cobble and plank with a map hall and the explorers' quarters, a
        # cobble watchtower with a battlement, a store for packs and ropes, a flag, oak fence.
        home = (2, 1, 12, 11); tb = (14, 4, 20, 10); store = (22, 2, 28, 8)
        yard(d, (0, 0, 30, 16))
        house(d, home, storeys=2, height=3, axis=1, level=4, no_windows=('east',))
        d.door((7, 1, 11), 'solid-oak', 'north')
        hut_block(d, (11, 1, 10))
        d.put('w4rd0sfurniture:roughtable-north', [(5, 1, 3), (7, 1, 3)]); d.put('w4rd0sfurniture:roughstool-west', [(6, 1, 4)]); d.put('game:bookshelf', [(3, 1, 3), (3, 1, 5), (3, 1, 7)]); d.put_against('game:chest', [(11, 1, 3), (11, 1, 5)])
        for y in (1, 2, 3): d.set((3, y, 9), BRICK); d.set((4, y, 9), BRICK)
        chimney(d, 3, 9, 4, 11, BRICK); d.place('game:firepit-cold', (4, 1, 8))
        d.stair_run((10, 1, 9), 'west', 3, 'planks')
        for x in (7, 8, 9, 10): d.set((x, 4, 9), None)
        d.beds(4, [(3, 5, 3), (3, 5, 6), (11, 5, 3), (11, 5, 6)], 'wood', 'east'); d.put('w4rd0sfurniture:roughtable-north', [(7, 5, 3)]); d.put_against('game:chest', [(3, 5, 9)])
        tower(d, tb, 1, 11, lambda x, y, z, a: COBBLE if y <= 8 else PLANK_Z if a else PLANK_X, roof='flat', post_every=3, posts=True, floors_every=4, ladder=(19, 'north', 9))
        for y in (3, 6):
            for (x, z) in ((17, 4), (17, 10), (14, 7), (20, 7)): d.set((x, y, z), None)
        opening(d, [(12, 7), (14, 7)], 1, 2); d.set((13, 0, 7), COBBLE)
        store_shed(d, store, 1, 3, 'west', COBBLE, 4, door=((22, 1, 5), 'east'))
        d.put('game:crate', [(24, 1, 3), (26, 1, 3), (24, 1, 7)]); d.put('game:barrel', [(26, 1, 7)]); d.place('game:toolrack-west', (27, 2, 5))
        flag(d, (17, 12, 7))
        d.lantern((7, 3, 6)); d.lantern((7, 7, 6)); d.lantern((17, 3, 7)); d.lantern((17, 7, 7)); d.lantern((25, 3, 5)); d.lantern((7, 3, 12), 'south'); banner(d, (9, 3, 12), 'south')
        d.fence((1, 0, 29, 15), 0, 'woodenfence-oak', gaps=[(7, 15), (8, 15)])
        d.path([(x, z) for x in (7, 8) for z in range(12, 16)], -1)
        grass(d, (0, 0, 30, 16), -1, every=11)
    else:
        # An explorers' hall: a stone-brick hall with a great map room and library, a mud-brick upper floor
        # of chambers, a tall masonry beacon tower with a fire on its open top and a barred gallery, a pack
        # store, a forecourt with two flags inside drystone walls.
        hall = (2, 1, 16, 11); tb = (16, 3, 22, 9); store = (24, 2, 30, 10)
        yard(d, (0, 0, 34, 18))
        house(d, hall, storeys=2, height=3, axis=0, level=5, no_windows=('east',))
        d.door((9, 1, 11), 'solid-oak', 'north')
        d.floor_inside(hall, 0, POLISHED)
        hut_block(d, (15, 1, 10))
        d.put('game:bookshelf', [(3, 1, 3), (3, 1, 5), (3, 1, 7), (5, 1, 2), (7, 1, 2), (9, 1, 2), (11, 1, 2)]); d.put('w4rd0sfurniture:rusticsturdytabledouble-east', [(7, 1, 6)]) or d.put('w4rd0sfurniture:roughtable-north', [(7, 1, 6)]); d.put('w4rd0sfurniture:rusticcabinetlarge-oak-east', [(15, 1, 3)]); d.put('game:crate', [(15, 1, 5)])
        for y in (1, 2, 3): d.set((3, y, 9), POLISHED); d.set((4, y, 9), POLISHED)
        chimney(d, 3, 9, 4, 11, BRICK); d.place('game:firepit-cold', (4, 1, 8))
        d.stair_run((14, 1, 9), 'west', 3, 'brick')
        for x in (11, 12, 13, 14): d.set((x, 4, 9), None)
        d.beds(6, [(3, 5, 3), (3, 5, 6), (15, 5, 3), (15, 5, 6), (7, 5, 3), (11, 5, 3)], 'rustic', 'east'); d.put('w4rd0sfurniture:rusticwardrobe-east', [(9, 5, 9)]); d.put('game:bookshelf', [(5, 5, 9), (13, 5, 9)])
        stone_tower(d, tb, 1, 15, roof='flat', floors_every=4, ladder=(21, 'north', 8), top_layers=3, bars=True)
        opening(d, [(16, 6)], 1, 2); opening(d, [(16, 6)], 5, 6)
        d.place('game:firepit-cold', (19, 16, 6))
        store_shed(d, store, 1, 3, 'west', BRICK, 5, door=((24, 1, 6), 'east'))
        d.put('game:crate', [(26, 1, 3), (28, 1, 3), (26, 1, 9), (28, 1, 9)]); d.put('game:barrel', [(28, 1, 6)]); d.place('game:toolrack-west', (29, 2, 5))
        d.ground((2, 12, 32, 14), -1, COBBLE); flag(d, (4, 0, 15)); flag(d, (14, 0, 15))
        for p in ((9, 3, 6), (9, 7, 6), (19, 3, 6), (19, 7, 6), (19, 11, 6), (27, 3, 6)): d.lantern(p)
        d.lantern((9, 2, 12), 'south'); banner(d, (6, 3, 12), 'south'); banner(d, (12, 3, 12), 'south')
        d.fence((1, 0, 33, 17), 0, 'drystonefence-shale', gaps=[(9, 17), (10, 17)])
        d.path([(x, z) for x in (9, 10) for z in range(12, 18)], -1)
        grass(d, (0, 0, 34, 18), -1, every=12)
    return d
