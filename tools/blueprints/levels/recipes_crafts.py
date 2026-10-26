"""Crafts: Sawmill, Cook, Potter, Craftsman, Tailor, Jeweller."""
from parts import *
from recipes_homes import recipe, RECIPES


def sawpit(d, box, depth=2, wall=COBBLE):
    """A pit with a log laid across it, under whatever roof is over box."""
    x0, z0, x1, z1 = box
    d.box_fill((x0 + 1, z0 + 1, x1 - 1, z1 - 1), -depth, 0, None)
    d.plain_walls((x0 + 1, z0 + 1, x1 - 1, z1 - 1), -depth, -1, wall); d.floor((x0 + 1, z0 + 1, x1 - 1, z1 - 1), -depth - 1, wall)
    zm = (z0 + z1) // 2
    for x in range(x0 + 1, x1): d.set((x, 1, zm), LOG_X)


# ---------------------------------------------------------------- Sawmill
@recipe('sawmill')
def sawmill(d):
    L = d.level
    if L == 1:
        # A sawyer's pit: a sawpit under an open roof on posts, a trestle, plank and log stacks, a hay bed under
        # a wattle screen at one end.
        box = (1, 1, 7, 7)
        yard(d, (0, 0, 14, 10))
        lean_to(d, box, 1, 3, 'north', open_sides=('south', 'east', 'west'), level=1, wall=None, floor=DIRT)
        d.floor(box, 0, DIRT); sawpit(d, box, 2, DIRT)
        hut_block(d, (7, 1, 7))
        for y in (1, 2): d.set((1, y, 6), fence_code('wattle-sticks', 'ns')); d.set((1, y, 5), fence_code('wattle-sticks', 'ns'))
        d.bed((2, 1, 6), 'hay', 'east') or d.bed((2, 1, 5), 'hay')
        woodpile(d, [(10, 2), (11, 2), (12, 2), (10, 3), (11, 3), (12, 3)], 0, 2, 'ns')
        for x in (10, 11, 12): d.set((x, 0, 7), PLANK_X); d.set((x, 1, 7), PLANK_X)
        d.place('game:firepit-cold', (4, 0, 9))
        d.torch((4, 2, 1), 'south')
        grass(d, (0, 0, 14, 10), -1, every=6)
    elif L == 2:
        # A saw shed: a long open-sided timber shed over the sawpit with a closed bunk room at one end, log
        # stacks along the open side, plank racks, a wattle fence.
        box = (1, 1, 13, 7)
        yard(d, (0, 0, 18, 12))
        house(d, box, storeys=1, height=3, axis=0, level=2, walls=dict(WALLS[2], low=lambda x, y, z, a: (daub(x, y, z, a) if x <= 4 or z == 1 else None), gable=PLANK_Z), post_every=3, window_sides=())
        for y in (1, 2): d.set((4, y, 3), daub(4, y, 3, False)); d.set((4, y, 5), daub(4, y, 5, False))
        d.door((4, 1, 4), 'crude', 'east')
        sawpit(d, (6, 2, 12, 6), 2, COBBLE)
        hut_block(d, (12, 1, 6))
        d.bed((2, 1, 2), 'rough', 'south'); d.put_against('game:chest', [(2, 1, 6)])
        d.place('game:toolrack-west', (12, 2, 2))
        woodpile(d, [(x, 9) for x in range(6, 13)], 0, 2, 'we')
        for x in range(2, 9): d.set((x, 0, 11), PLANK_X)
        d.torch((3, 2, 4), 'east'); d.torch((9, 2, 2), 'south')
        d.fence((0, 0, 18, 12), 0, 'wattle-sticks', gaps=[(15, 12), (16, 12)])
        grass(d, (0, 0, 18, 12), -1, every=8)
    elif L == 3:
        # A sawmill: a rammed-earth mill house with the sawpit hall open to the yard, a cross-wing for the
        # sawyers' beds, a roofed timber store, a crane beam on posts, oak fence and stone path.
        hall = (2, 1, 14, 9); wing = (14, 3, 20, 9); store = (2, 13, 14, 17)
        yard(d, (0, 0, 26, 20))
        house(d, hall, storeys=1, height=4, axis=0, level=3, no_windows=('east', 'south'))
        for x in range(4, 13):
            for y in (1, 2):
                if (x - 2) % 3 != 0: d.set((x, y, 9), None)
        house(d, wing, storeys=1, height=3, axis=0, level=3, no_windows=('west',))
        opening(d, [(14, 6)], 1, 2)
        sawpit(d, (3, 2, 13, 8), 2, COBBLE)
        hut_block(d, (12, 1, 8))
        d.beds(3, [(16, 1, 4), (18, 1, 4), (19, 1, 7)], 'rough', 'south'); d.put_against('game:chest', [(15, 1, 8)])
        d.place('game:toolrack-west', (13, 2, 2)); d.place('game:toolrack-west', (13, 2, 3))
        lean_to(d, store, 1, 3, 'south', open_sides=('north',), level=3, wall=PLANK_X)
        woodpile(d, [(x, 16) for x in range(3, 14)], 1, 2, 'we');
        for x in range(3, 14): d.set((x, 1, 14), PLANK_X)
        for (x, z) in ((17, 12), (23, 12)):
            for y in range(0, 6): d.set((x, y, z), LOG)
        for x in range(17, 24): d.set((x, 6, 12), LOG_X)
        woodpile(d, [(x, z) for x in range(18, 23) for z in (13, 14)], 0, 2, 'ns')
        d.lantern((8, 4, 5)); d.lantern((17, 3, 6)); d.lantern((8, 3, 10), 'south')
        d.fence((1, 0, 25, 19), 0, 'woodenfence-oak', gaps=[(25, 10), (25, 11)])
        d.path([(x, z) for x in range(3, 25) for z in (10, 11)], -1)
        grass(d, (0, 0, 26, 20), -1, every=10)
    elif L == 4:
        # A big sawmill: a two-storey cobble-and-plank mill (two sawpits below, the plank loft above with a
        # hoist gable), a long drying barn, a crane over the log yard, a stone-paved yard behind an oak fence.
        mill = (2, 1, 14, 13); barn = (18, 1, 30, 7)
        yard(d, (0, 0, 34, 22))
        house(d, mill, storeys=2, height=3, axis=1, level=4, no_windows=('south',))
        for x in range(4, 13):
            for y in (1, 2):
                if (x - 2) % 4 != 0: d.set((x, y, 13), None)
        sawpit(d, (3, 2, 13, 6), 2, COBBLE); sawpit(d, (3, 7, 13, 11), 2, COBBLE)
        hut_block(d, (13, 1, 12))
        d.set((13, 1, 12), None); hut_block(d, (3, 1, 12))
        d.ladder(13, 12, 1, 4, 'north'); d.set((13, 4, 12), None)
        for z in range(3, 12, 2): d.set((4, 5, z), PLANK_X); d.set((5, 5, z), PLANK_X); d.set((10, 5, z), PLANK_X); d.set((11, 5, z), PLANK_X)
        for z in (14, 15, 16): d.set((8, 8, z), LOG_Z)
        d.door((8, 5, 13), 'solid-oak', 'north'); d.set((8, 6, 13), 'game:multiblock-monolithic-0-p1-0')
        d.place('game:toolrack-west', (13, 2, 3)); d.place('game:toolrack-west', (13, 2, 4)); d.put_against('game:chest', [(3, 5, 12)])
        d.lantern((8, 3, 6)); d.lantern((8, 7, 6)); d.lantern((8, 3, 11))
        house(d, barn, storeys=1, height=3, axis=0, level=4, walls=dict(WALLS[4], low=None, gable=PLANK_Z), post_every=3, window_sides=())
        for (x, z) in ring(barn):
            for y in (1, 2):
                if d.get((x, y, z)) == COBBLE: d.set((x, y, z), None)
        d.floor_inside(barn, 0, COBBLE)
        woodpile(d, [(x, 2) for x in range(19, 30)], 1, 2, 'we'); woodpile(d, [(x, 6) for x in range(19, 30)], 1, 2, 'we')
        for x in range(19, 30): d.set((x, 1, 4), PLANK_X)
        d.lantern((24, 3, 4))
        for (x, z) in ((19, 12), (27, 12)):
            for y in range(0, 7): d.set((x, y, z), LOG)
        for x in range(19, 28): d.set((x, 7, 12), LOG_X)
        woodpile(d, [(x, z) for x in range(20, 27) for z in (13, 14, 15)], 0, 2, 'ns')
        d.ground((2, 14, 32, 20), -1, COBBLE)
        d.fence((1, 0, 33, 21), 0, 'woodenfence-oak', gaps=[(33, 17), (33, 18)])
        d.path([(x, z) for x in range(15, 18) for z in range(2, 14)] + [(x, 9) for x in range(18, 33)], -1)
        grass(d, (0, 0, 34, 22), -1, every=11)
    else:
        # A timber mill: a stone-brick mill hall with three sawpits and a mud-brick plank loft, a hip-roofed
        # tower for the master sawyer, two drying barns on stone footings, a stone-based crane, a cobbled yard
        # in a drystone wall.
        mill = (2, 1, 16, 15); tow = (16, 9, 22, 15)
        yard(d, (0, 0, 42, 28))
        house(d, mill, storeys=2, height=3, axis=1, level=5, no_windows=('south', 'east'))
        for x in range(4, 15):
            for y in (1, 2):
                if (x - 2) % 4 != 0: d.set((x, y, 15), None)
        stone_tower(d, tow, 1, 10, roof='flat', floors_every=4, ladder=(21, 'north', 14))
        for z in range(16, 20): d.set((19, 11, z), LOG_Z)
        opening(d, [(16, 12), (16, 13)], 1, 2); opening(d, [(16, 12), (16, 13)], 5, 6)
        for z in (12, 13): d.set((16, 4, z), PLANK_X); d.set((16, 8, z), None); d.set((16, 9, z), None)
        for z0 in (2, 6, 10): sawpit(d, (3, z0, 15, z0 + 3), 2, BRICK)
        hut_block(d, (3, 1, 14))
        d.ladder(15, 14, 1, 4, 'north'); d.set((15, 4, 14), None)
        for z in range(3, 14, 2): d.set((4, 5, z), PLANK_X); d.set((5, 5, z), PLANK_X); d.set((12, 5, z), PLANK_X); d.set((13, 5, z), PLANK_X)
        for z in (16, 17, 18): d.set((9, 8, z), LOG_Z)
        d.door((9, 5, 15), 'solid-oak', 'north'); d.set((9, 6, 15), 'game:multiblock-monolithic-0-p1-0')
        d.place('game:toolrack-west', (15, 2, 3)); d.place('game:toolrack-west', (15, 2, 4))
        d.beds(3, [(18, 5, 11), (20, 5, 11), (18, 9, 11)], 'rustic', 'east'); d.put('w4rd0sfurniture:rusticcabinetlarge-oak-east', [(20, 1, 11)]); d.put('game:crate', [(18, 1, 11), (18, 1, 13)])
        for y in (3, 7): d.lantern((9, y, 7)); d.lantern((19, y, 13))
        for z0 in (1, 9):
            b = (26, z0, 40, z0 + 6)
            house(d, b, storeys=1, height=3, axis=0, level=5, walls=dict(WALLS[5], low=lambda x, y, z, a: BRICK if y == 1 else None, gable=PLANK_Z), post_every=3, window_sides=())
            d.floor_inside(b, 0, COBBLE)
            woodpile(d, [(x, z0 + 1) for x in range(27, 40)], 2, 1, 'we'); woodpile(d, [(x, z0 + 5) for x in range(27, 40)], 2, 1, 'we')
            for x in range(27, 40): d.set((x, 2, z0 + 3), PLANK_X)
            d.lantern((33, 3, z0 + 3))
        for p in ((26, 0, 20), (27, 0, 20), (26, 0, 21), (27, 0, 21)): d.set(p, BRICK)
        for y in range(1, 8): d.set((26, y, 20), LOG)
        for x in range(26, 36): d.set((x, 8, 20), LOG_X)
        woodpile(d, [(x, z) for x in range(28, 35) for z in (21, 22, 23)], 0, 2, 'ns')
        d.ground((2, 16, 24, 26), -1, COBBLE)
        d.fence((1, 0, 41, 27), 0, 'drystonefence-shale', gaps=[(41, 18), (41, 19)])
        d.path([(x, z) for x in (24, 25) for z in range(2, 17)] + [(x, 18) for x in range(25, 41)], -1)
        grass(d, (0, 0, 42, 28), -1, every=12)
    return d


# ---------------------------------------------------------------- Cook
@recipe('cook')
def cook(d):
    L = d.level
    if L == 1:
        # A cookfire: a lean-to kitchen open to the front with a clay oven in the back wall, a firepit, a table
        # for chopping, baskets of roots, a hay bed in the corner.
        box = (1, 1, 7, 5)
        yard(d, (0, 0, 10, 9))
        lean_to(d, box, 1, 3, 'north', open_sides=('south',), level=1, floor=DIRT)
        hut_block(d, (6, 1, 4))
        d.put('game:clayoven-east', [(2, 1, 2)]); d.place('game:firepit-cold', (4, 1, 2))
        d.put('w4rd0sfurniture:roughtable-north', [(5, 1, 2)]); d.put('game:stationarybasket-north', [(2, 1, 4)])
        d.bed((6, 1, 2), 'hay', 'south') or d.bed((5, 1, 3), 'hay')
        d.torch((4, 2, 1), 'south')
        d.place('game:firepit-cold', (4, 0, 7))
        grass(d, (0, 0, 10, 9), -1, every=7)
    elif L == 2:
        # A cookhouse: daub kitchen with a big cobble hearth and two clay ovens, a work table, shelves, a food
        # store corner, the cook's bed in a wattle-screened nook, a herb patch.
        box = (1, 1, 9, 9)
        yard(d, (0, 0, 14, 13))
        house(d, box, storeys=1, height=3, axis=0, level=2)
        d.door((5, 1, 9), 'crude', 'north')
        hut_block(d, (8, 1, 8))
        for x in (3, 4, 5, 6):
            for y in (1, 2): d.set((x, y, 1), COBBLE)
        d.put('game:clayoven-east', [(3, 1, 2)]); d.put('game:clayoven-east', [(6, 1, 2)]); d.place('game:firepit-cold', (4, 1, 2))
        chimney(d, 4, 1, 3, 7, COBBLE)
        d.put('w4rd0sfurniture:roughtabledouble-south', [(4, 1, 5)]) or d.put('w4rd0sfurniture:roughtable-north', [(4, 1, 5)])
        d.put('foodshelves:doubleshelf-normal-east', [(2, 1, 4)]); d.put('game:barrel', [(2, 1, 6)]); d.put('game:stationarybasket-north', [(2, 1, 7), (3, 1, 7)])
        for y in (1, 2): d.set((7, y, 5), fence_code('wattle-sticks', 'ns'))
        d.bed((8, 1, 3), 'rough', 'south') or d.bed((8, 1, 4), 'rough')
        d.torch((5, 2, 7)); d.torch((5, 2, 10), 'south')
        garden(d, (11, 2, 13, 7), -1, rows=False)
        d.fence((0, 0, 14, 12), 0, 'wattle-sticks', gaps=[(5, 12), (6, 12)])
        d.path([(5, 10), (5, 11), (5, 12), (6, 10), (6, 11), (6, 12)], -1)
        grass(d, (0, 0, 14, 13), -1, every=9)
    elif L == 3:
        # A kitchen house: a rammed-earth kitchen with a stone bake oven, three clay ovens, a cross-wing larder
        # with shelves and a cold cellar under it, a well-stocked root store, a kitchen garden in rows.
        kit = (2, 1, 10, 9); larder = (10, 1, 16, 7)
        yard(d, (0, 0, 20, 16))
        house(d, kit, storeys=1, height=3, axis=0, level=3, no_windows=('east',))
        house(d, larder, storeys=1, height=3, axis=1, level=3, no_windows=('west', 'north'))
        opening(d, [(10, 4)], 1, 2)
        d.door((6, 1, 9), 'solid-oak', 'north')
        hut_block(d, (9, 1, 8))
        for x in range(3, 9):
            for y in (1, 2): d.set((x, y, 1), COBBLE)
        for x in (3, 5, 7): d.put('game:clayoven-east', [(x, 1, 2)])
        d.place('game:firepit-cold', (4, 1, 4))
        if 'stonebakeoven:ovencontrollerbrick-shale-cold-south' in CATALOG:
            d.place('stonebakeoven:ovencontrollerbrick-shale-cold-south', (8, 1, 2)); d.place('stonebakeoven:ovenbakingtopbrick-shale', (8, 2, 2))
        chimney(d, 5, 1, 3, 8, COBBLE)
        d.put('w4rd0sfurniture:roughtabledouble-south', [(5, 1, 6)]) or d.put('w4rd0sfurniture:roughtable-north', [(5, 1, 6)])
        d.put('foodshelves:doubleshelf-normal-east', [(3, 1, 7)]); d.put('foodshelves:breadshelf-normal-west', [(9, 1, 5)]) or d.put('foodshelves:breadshelf-normal-north', [(9, 1, 5)])
        d.put('game:barrel', [(11, 1, 2), (15, 1, 2)]); d.put('foodshelves:doubleshelf-normal-east', [(11, 1, 5)]); d.put('foodshelves:floursack-normal-east', [(15, 1, 5)]); d.put('game:stationarybasket-north', [(13, 1, 2)])
        d.box_fill((11, 2, 15, 6), -3, -1, None); d.plain_walls((11, 2, 15, 6), -3, -1, COBBLE); d.floor((11, 2, 15, 6), -4, COBBLE)
        d.set((15, 0, 6), None); d.ladder(15, 6, -3, 0, 'north')
        d.put('foodshelves:barrelrack-normal-north', [(12, -3, 3)]); d.put('game:crate', [(12, -3, 5)]); d.put('foodshelves:coolingcabinet-normal-west', [(14, -3, 3)]) or d.put('game:barrel', [(14, -3, 3)])
        d.bed((3, 1, 5), 'rough', 'south') or d.bed((3, 1, 4), 'rough')
        d.lantern((6, 3, 5)); d.lantern((13, 3, 4)); d.lantern((6, 2, 10), 'south'); d.lantern((13, -1, 4))
        garden(d, (3, 12, 13, 14), -1)
        d.fence((1, 0, 19, 15), 0, 'woodenfence-oak', gaps=[(6, 15), (7, 15)])
        d.path([(6, 10), (6, 11), (7, 10), (7, 11)] + [(x, 11) for x in range(8, 19)] + [(6, 15), (7, 15)], -1)
        grass(d, (0, 0, 20, 16), -1, every=10)
    elif L == 4:
        # A great kitchen: two storeys, a cobble kitchen hall below with two hearths, four ovens, a stone bake
        # oven and long tables, the cooks' rooms above, a larder lean-to, a stone cellar, a porch, a walled garden.
        hall = (2, 1, 12, 13); larder = (12, 3, 17, 11)
        yard(d, (0, 0, 24, 18))
        house(d, hall, storeys=2, height=3, axis=1, level=4, no_windows=('east',))
        lean_to(d, larder, 1, 3, 'west', level=4, wall=COBBLE)
        opening(d, [(12, 6), (12, 8)], 1, 2)
        d.door((7, 1, 13), 'solid-oak', 'north')
        hut_block(d, (6, 1, 12))
        for x in range(3, 12):
            for y in (1, 2, 3): d.set((x, y, 1), BRICK)
        for x in (3, 5, 9, 11): d.put('game:clayoven-east', [(x, 1, 2)])
        if 'stonebakeoven:ovencontrollerbrick-shale-cold-south' in CATALOG:
            d.place('stonebakeoven:ovencontrollerbrick-shale-cold-south', (7, 1, 2)); d.place('stonebakeoven:ovenbakingtopbrick-shale', (7, 2, 2))
        d.place('game:firepit-cold', (4, 1, 5)); d.place('game:firepit-cold', (10, 1, 5))
        chimney(d, 4, 1, 4, 10, COBBLE); chimney(d, 10, 1, 4, 10, COBBLE)
        d.put('w4rd0sfurniture:roughtabledouble-south', [(5, 1, 8)]) or d.put('w4rd0sfurniture:roughtable-north', [(5, 1, 8)]); d.put('w4rd0sfurniture:roughtabledouble-south', [(9, 1, 8)]) or None
        d.put('foodshelves:doubleshelf-normal-east', [(3, 1, 10)]); d.put('foodshelves:breadshelf-normal-north', [(9, 1, 11)]); d.put('game:barrel', [(3, 1, 7)]); d.put('foodshelves:ceilingrack-normal', [(7, 3, 8)])
        d.put('game:barrel', [(14, 1, 4), (16, 1, 4), (14, 1, 10)]); d.put('foodshelves:doubleshelf-normal-east', [(13, 1, 7)]); d.put('foodshelves:floursack-normal-east', [(16, 1, 7), (16, 1, 9)]); d.put('foodshelves:coolingcabinet-normal-west', [(16, 1, 10)]) or None
        d.stair_run((11, 1, 11), 'north', 3, 'planks')
        for z in (8, 9, 10, 11): d.set((11, 4, z), None)
        d.beds(3, [(3, 5, 3), (3, 5, 6), (9, 5, 3)], 'wood', 'south'); d.put_against('game:chest', [(3, 5, 11)]); table_and_chairs(d, [(7, 5, 8)], 'rough')
        d.box_fill((3, 2, 11, 12), -3, -1, None); d.plain_walls((3, 2, 11, 12), -3, -1, COBBLE); d.floor((3, 2, 11, 12), -4, COBBLE)
        d.set((3, 0, 11), None); d.ladder(3, 11, -3, 0, 'north')
        d.put('foodshelves:barrelrack-normal-north', [(5, -3, 3), (7, -3, 3), (9, -3, 3)]); d.put('game:crate', [(4, -3, 7), (6, -3, 7)]); d.put('foodshelves:meatfreezer-normal-west', [(10, -3, 7)]) or d.put('game:barrel', [(10, -3, 7)]); d.put('foodshelves:icekeeper-normal-south', [(10, -3, 9)]) or None
        d.lantern((7, 3, 6)); d.lantern((7, 3, 10)); d.lantern((7, 7, 6)); d.lantern((14, 3, 7)); d.lantern((7, 2, 14), 'south'); d.lantern((7, -1, 7))
        porch(d, (5, 14, 9, 15), 1, 3, 'north')
        garden(d, (19, 2, 23, 14), -1)
        d.fence((1, 0, 24, 17), 0, 'woodenfence-oak', gaps=[(7, 17), (8, 17)])
        d.path([(x, z) for x in (7, 8) for z in (16, 17)] + [(x, 14) for x in range(10, 19)] + [(18, z) for z in range(2, 15)], -1)
        grass(d, (0, 0, 24, 18), -1, every=11)
    else:
        # A hall kitchen: a stone-brick kitchen hall with a great double hearth, six ovens and two stone bake
        # ovens, a mud-brick upper storey for the cooks, a hip-roofed larder tower over a deep cellar, a
        # scullery wing, a walled kitchen garden with paths, lanterns and a banner.
        hall = (2, 1, 14, 15); tow = (14, 9, 20, 15); scul = (14, 2, 20, 7)
        yard(d, (0, 0, 28, 22))
        house(d, hall, storeys=2, height=3, axis=1, level=5, no_windows=('east',))
        stone_tower(d, tow, 1, 9, roof='hip', floors_every=4, ladder=(19, 'north', 14), top_layers=2)
        lean_to(d, scul, 1, 3, 'west', level=5, wall=BRICK)
        opening(d, [(14, 11), (14, 12), (14, 13)], 1, 2); opening(d, [(14, 4), (14, 5)], 1, 2)
        for z in (11, 12, 13): d.set((14, 4, z), PLANK_X); d.set((14, 5, z), None); d.set((14, 6, z), None)
        d.door((8, 1, 15), 'solid-oak', 'north')
        hut_block(d, (7, 1, 14))
        d.floor_inside(hall, 0, POLISHED)
        for x in range(3, 14):
            for y in (1, 2, 3): d.set((x, y, 1), POLISHED)
        for x in (3, 5, 11, 13): d.put('game:clayoven-east', [(x, 1, 2)])
        for x in (4, 12): d.put('game:clayoven-east', [(x, 1, 3)]) if False else None
        if 'stonebakeoven:ovencontrollerbrick-shale-cold-south' in CATALOG:
            for x in (7, 9):
                d.place('stonebakeoven:ovencontrollerbrick-shale-cold-south', (x, 1, 2)); d.place('stonebakeoven:ovenbakingtopbrick-shale', (x, 2, 2))
        d.place('game:firepit-cold', (5, 1, 6)); d.place('game:firepit-cold', (11, 1, 6))
        chimney(d, 5, 1, 4, 11, BRICK); chimney(d, 11, 1, 4, 11, BRICK)
        d.put('w4rd0sfurniture:rusticsturdytabledouble-east', [(5, 1, 9)]) or d.put('w4rd0sfurniture:roughtable-north', [(5, 1, 9)]); d.put('w4rd0sfurniture:rusticsturdytabledouble-east', [(10, 1, 9)]) or None
        d.put('foodshelves:doubleshelf-normal-east', [(3, 1, 11)]); d.put('foodshelves:breadshelf-normal-north', [(11, 1, 13)]); d.put('foodshelves:ceilingrack-normal', [(8, 3, 9)]); d.put('foodshelves:pieshelf-normal-west', [(13, 1, 11)]) or None
        d.put('game:barrel', [(16, 1, 3), (18, 1, 3)]); d.put('foodshelves:floursack-normal-east', [(19, 1, 5), (19, 1, 6)]); d.put('foodshelves:coolingcabinet-normal-west', [(16, 1, 6)]) or None
        d.put('foodshelves:doubleshelf-normal-east', [(16, 1, 11), (16, 1, 13)]); d.put('game:crate', [(18, 1, 11), (18, 1, 13)])
        d.put('foodshelves:doubleshelf-normal-east', [(16, 5, 11), (16, 5, 13)]); d.put('foodshelves:floursack-normal-east', [(18, 5, 11), (18, 5, 13)])
        d.stair_run((13, 1, 13), 'north', 3, 'brick')
        for z in (10, 11, 12, 13): d.set((13, 4, z), None)
        d.beds(4, [(3, 5, 3), (3, 5, 6), (11, 5, 3), (11, 5, 6)], 'rustic', 'east'); d.put('w4rd0sfurniture:rusticwardrobe-east', [(3, 5, 12)]); table_and_chairs(d, [(7, 5, 10)], 'rustic', double=True)
        d.box_fill((15, 10, 19, 14), -4, -1, None); d.plain_walls((15, 10, 19, 14), -4, -1, BRICK); d.floor((15, 10, 19, 14), -5, BRICK)
        d.set((19, 0, 14), None); d.ladder(19, 14, -4, 0, 'north')
        d.put('foodshelves:barrelrack-normal-north', [(16, -4, 11), (18, -4, 11)]); d.put('foodshelves:icekeeper-normal-south', [(16, -4, 13)]) or d.put('game:crate', [(16, -4, 13)]); d.put('foodshelves:meatfreezer-normal-west', [(18, -4, 13)]) or None
        for p in ((8, 3, 7), (8, 3, 12), (8, 7, 7), (17, 3, 12), (17, 7, 12), (17, -2, 12), (17, 3, 4)): d.lantern(p)
        d.lantern((8, 2, 16), 'south'); banner(d, (10, 3, 16), 'south')
        garden(d, (22, 2, 27, 16), -1)
        d.fence((1, 0, 28, 21), 0, 'drystonefence-shale', gaps=[(8, 21), (9, 21)])
        d.path([(x, z) for x in (8, 9) for z in range(16, 22)] + [(x, 18) for x in range(10, 22)] + [(21, z) for z in range(2, 19)], -1)
        grass(d, (0, 0, 28, 22), -1, every=12)
    return d


# ---------------------------------------------------------------- Potter
@recipe('potter')
def potter(d):
    L = d.level
    if L == 1:
        # A potter's pitch: a wattle lean-to over the wheel table and clay store, a firing ground of dug earth
        # with stone surrounds for the pit kilns, drying shelves in the sun.
        box = (1, 1, 6, 5)
        yard(d, (0, 0, 14, 10))
        lean_to(d, box, 1, 3, 'north', open_sides=('south',), level=1, floor=DIRT, wall=lambda x, y, z, a: fence_code('wattle-sticks', 'ew' if a else 'ns') if y == 2 else DIRT)
        hut_block(d, (5, 1, 4))
        d.put('w4rd0sfurniture:roughtable-north', [(3, 1, 2)]); d.put('w4rd0sfurniture:roughstool-west', [(3, 1, 3)])
        d.place('game:groundstorage', (2, 0, 7)); d.place('game:groundstorage', (4, 0, 7))
        d.bed((2, 1, 2), 'hay', 'east') or d.bed((2, 1, 4), 'hay')
        for (x, z) in ((9, 2), (12, 2), (9, 6), (12, 6)):
            for (dx, dz) in ((-1, 0), (1, 0), (0, -1), (0, 1)): d.set((x + dx, 0, z + dz), COBBLE)
            d.set((x, -1, z), DIRT)
        d.put('game:shelf-normal-south', [(8, 1, 9)]) or d.put('game:shelf-normal-west', [(8, 1, 9)])
        d.torch((3, 2, 1), 'south')
        grass(d, (0, 0, 14, 10), -1, every=7)
    elif L == 2:
        # A pottery: a daub workshop with the wheel, a drying room of shelves, the potter's bed, a clay pit at
        # the yard edge, four stone-ringed pit kilns, a wattle fence.
        box = (1, 1, 9, 7)
        yard(d, (0, 0, 20, 14))
        house(d, box, storeys=1, height=3, axis=0, level=2)
        d.door((5, 1, 7), 'crude', 'north')
        hut_block(d, (8, 1, 6))
        d.put('w4rd0sfurniture:roughtable-north', [(3, 1, 3)]); d.put('w4rd0sfurniture:roughstool-west', [(3, 1, 4)])
        d.put('game:shelf-normal-west', [(2, 1, 2)]); d.put('game:shelf-normal-west', [(2, 1, 5)]); d.put('w4rd0sfurniture:roughstandingshelf-north', [(6, 1, 2), (7, 1, 2)])
        d.bed((7, 1, 5), 'rough', 'north') or d.bed((7, 1, 4), 'rough')
        d.place('game:groundstorage', (4, 1, 5)); d.torch((5, 2, 5)); d.torch((5, 2, 8), 'south')
        for (x, z) in ((13, 3), (17, 3), (13, 8), (17, 8)):
            for (dx, dz) in ((-1, 0), (1, 0), (0, -1), (0, 1)): d.set((x + dx, 0, z + dz), COBBLE)
            d.set((x, -1, z), DIRT)
        d.box_fill((2, 10, 7, 12), -2, -1, None); d.ground((2, 10, 7, 12), -2, 'game:rock-claystone'); d.set((4, -1, 11), 'game:rock-claystone')
        d.place('game:groundstorage', (9, 0, 11)); d.place('game:groundstorage', (11, 0, 11))
        d.fence((0, 0, 20, 13), 0, 'wattle-sticks', gaps=[(5, 13), (6, 13)])
        d.path([(5, 8), (5, 9), (6, 8), (6, 9)], -1)
        grass(d, (0, 0, 20, 14), -1, every=9)
    elif L == 3:
        # A potter's works: rammed-earth workshop with two wheels, a drying wing with racks, a mud-brick kiln
        # house in the yard with a chimney, a clay pit with a ramp, a store, an oak fence.
        shop = (2, 1, 10, 9); dry = (10, 1, 16, 7); kiln = (19, 2, 25, 8)
        yard(d, (0, 0, 28, 18))
        house(d, shop, storeys=1, height=3, axis=0, level=3, no_windows=('east',))
        house(d, dry, storeys=1, height=3, axis=1, level=3, no_windows=('west',), windows='wattle')
        opening(d, [(10, 4)], 1, 2)
        d.door((6, 1, 9), 'solid-oak', 'north')
        hut_block(d, (9, 1, 8))
        d.put('w4rd0sfurniture:roughtable-north', [(4, 1, 3)]); d.put('w4rd0sfurniture:roughstool-west', [(4, 1, 4)]); d.put('w4rd0sfurniture:roughtable-north', [(7, 1, 3)]); d.put('w4rd0sfurniture:roughstool-west', [(7, 1, 4)])
        d.put('game:shelf-normal-west', [(3, 1, 6)]); d.put('w4rd0sfurniture:roughstandingshelf-north', [(5, 1, 7), (6, 1, 7)])
        d.bed((3, 1, 7), 'rough', 'north') if False else d.bed((8, 1, 6), 'rough', 'south')
        d.place('game:groundstorage', (3, 1, 2)); d.place('game:groundstorage', (9, 1, 2))
        d.put('w4rd0sfurniture:roughstandingshelf-north', [(11, 1, 2), (13, 1, 2), (15, 1, 2)]); d.put('game:shelf-normal-west', [(11, 1, 5)]); d.put('w4rd0sfurniture:roughstandingshelf-north', [(13, 1, 6), (15, 1, 6)])
        d.lantern((6, 3, 6)); d.lantern((13, 3, 4)); d.lantern((6, 2, 10), 'south')
        # kiln house: mud brick with a domed-looking top of stairs
        d.plain_walls(kiln, 1, 3, MUD_DARK); d.hollow(kiln, 1, 3); d.floor(kiln, 4, MUD_DARK); d.rooms.append((kiln, 3))
        for (x, z) in ring((20, 3, 24, 7)): d.set((x, 4, z), stairs('cobble', 'north' if z == 3 else 'south' if z == 7 else 'west' if x == 20 else 'east'))
        chimney(d, 22, 5, 4, 8, MUD_DARK)
        d.door((22, 1, 8), 'solid-oak', 'north'); d.place('game:firepit-cold', (22, 1, 4)); d.place('game:firepit-cold', (20, 1, 6)); d.place('game:firepit-cold', (24, 1, 6))
        d.box_fill((3, 12, 9, 16), -3, -1, None); d.ground((3, 12, 9, 16), -3, 'game:rock-claystone')
        for k in range(3): d.set((10 - k, -k - 1, 14), DIRT) if False else None
        d.set((10, -1, 14), stairs('cobble', 'east')); d.set((9, -2, 14), None); d.set((9, -2, 14), stairs('cobble', 'east')) if False else None
        d.place('game:groundstorage', (12, 0, 13)); d.place('game:groundstorage', (14, 0, 13)); d.place('game:groundstorage', (12, 0, 15))
        d.fence((1, 0, 27, 17), 0, 'woodenfence-oak', gaps=[(6, 17), (7, 17)])
        d.path([(x, z) for x in (6, 7) for z in range(10, 18)] + [(x, 11) for x in range(8, 26)], -1)
        grass(d, (0, 0, 28, 18), -1, every=10)
    elif L == 4:
        # A potters' hall: two storeys, cobble workshop below with four wheels and glaze shelves, planks above for
        # the potters and the finished wares, a big mud-brick kiln house with two fire mouths, a clay store shed,
        # a stone-walled clay pit, lanterns.
        hall = (2, 1, 12, 11); kiln = (16, 1, 24, 9); shed = (16, 12, 22, 16)
        yard(d, (0, 0, 30, 22))
        house(d, hall, storeys=2, height=3, axis=0, level=4)
        d.door((7, 1, 11), 'solid-oak', 'north')
        hut_block(d, (11, 1, 10))
        for x in (3, 5, 9, 11): d.put('w4rd0sfurniture:roughtable-north', [(x, 1, 3)]); d.put('w4rd0sfurniture:roughstool-west', [(x, 1, 4)])
        d.put('w4rd0sfurniture:roughstandingshelf-north', [(3, 1, 9), (4, 1, 9), (5, 1, 9)]); d.put('game:shelf-normal-west', [(3, 1, 6)]); d.put('game:barrel', [(11, 1, 6)])
        d.place('game:groundstorage', (7, 1, 6)); d.place('game:groundstorage', (9, 1, 7))
        d.stair_run((11, 1, 9), 'west', 3, 'planks') if False else d.stair_run((10, 1, 9), 'west', 3, 'planks')
        for x in (7, 8, 9, 10): d.set((x, 4, 9), None)
        d.beds(3, [(3, 5, 3), (5, 5, 3), (11, 5, 3)], 'wood', 'south'); d.put('w4rd0sfurniture:roughstandingshelf-north', [(3, 5, 9), (4, 5, 9), (5, 5, 9), (6, 5, 9)]); d.put_against('game:chest', [(11, 5, 9)])
        d.lantern((7, 3, 7)); d.lantern((7, 7, 6)); d.lantern((7, 2, 12), 'south')
        porch(d, (5, 12, 9, 13), 1, 3, 'north')
        d.plain_walls(kiln, 1, 4, MUD_DARK); d.hollow(kiln, 1, 4); d.floor(kiln, 5, MUD_DARK); d.rooms.append((kiln, 4))
        for (x, z) in ring((17, 2, 23, 8)): d.set((x, 5, z), stairs('cobble', 'north' if z == 2 else 'south' if z == 8 else 'west' if x == 17 else 'east'))
        for (x, z) in ring((18, 3, 22, 7)): d.set((x, 6, z), stairs('cobble', 'north' if z == 3 else 'south' if z == 7 else 'west' if x == 18 else 'east'))
        chimney(d, 20, 5, 5, 10, MUD_DARK)
        d.door((20, 1, 9), 'solid-oak', 'north'); d.door((16, 1, 5), 'solid-oak', 'east')
        for p in ((18, 1, 3), (22, 1, 3), (18, 1, 7), (22, 1, 7)): d.place('game:firepit-cold', p)
        d.lantern((20, 3, 5))
        lean_to(d, shed, 1, 3, 'north', open_sides=('south',), level=4, wall=PLANK_X)
        for p in ((17, 1, 13), (19, 1, 13), (21, 1, 13), (18, 1, 15), (20, 1, 15)): d.place('game:groundstorage', p)
        d.box_fill((3, 14, 11, 19), -3, -1, None); d.plain_walls((3, 14, 11, 19), -3, -1, COBBLE); d.ground((3, 14, 11, 19), -3, 'game:rock-claystone')
        d.set((11, -1, 16), stairs('cobble', 'west')); d.set((11, -2, 16), None); d.set((10, -2, 16), stairs('cobble', 'west')); d.set((11, -2, 16), COBBLE)
        d.fence((1, 0, 29, 21), 0, 'woodenfence-oak', gaps=[(7, 21), (8, 21)])
        d.path([(x, z) for x in (7, 8) for z in range(14, 22)] + [(x, 13) for x in range(9, 16)] + [(14, z) for z in range(2, 20)] + [(x, 11) for x in range(15, 29)], -1)
        grass(d, (0, 0, 30, 22), -1, every=11)
    else:
        # A pottery manufactory: stone-brick workshop hall with six wheels and a mud-brick upper floor of wares,
        # a tall brick kiln house with a chimney stack, a glaze house, clay stores under a long shed, a
        # stone-lined clay pit with a ramp, drystone walls, lanterns and a banner.
        hall = (2, 1, 14, 13); kiln = (18, 1, 28, 11); glaze = (14, 2, 18, 7); shed = (18, 14, 32, 18)
        yard(d, (0, 0, 36, 26))
        house(d, hall, storeys=2, height=3, axis=0, level=5, no_windows=('east',))
        lean_to(d, glaze, 1, 3, 'west', level=5, wall=BRICK)
        opening(d, [(14, 4), (14, 5)], 1, 2)
        d.door((8, 1, 13), 'solid-oak', 'north')
        hut_block(d, (13, 1, 12))
        d.floor_inside(hall, 0, POLISHED)
        for x in (3, 5, 7, 9, 11, 13): d.put('w4rd0sfurniture:roughtable-north', [(x, 1, 3)]); d.put('w4rd0sfurniture:roughstool-west', [(x, 1, 4)])
        d.put('w4rd0sfurniture:rusticlargeshelf-oak-south', [(3, 1, 11), (6, 1, 11)]) or d.put('w4rd0sfurniture:roughstandingshelf-north', [(3, 1, 11), (4, 1, 11)]); d.put('game:barrel', [(13, 1, 7)])
        for p in ((5, 1, 7), (8, 1, 7), (11, 1, 7)): d.place('game:groundstorage', p)
        d.put('game:barrel', [(15, 1, 3), (17, 1, 3), (15, 1, 6)]); d.put('game:shelf-normal-west', [(17, 1, 5)]) or None
        d.stair_run((12, 1, 11), 'west', 3, 'brick')
        for x in (9, 10, 11, 12): d.set((x, 4, 11), None)
        d.beds(4, [(3, 5, 3), (6, 5, 3), (9, 5, 3), (13, 5, 3)], 'rustic', 'east')
        d.put('w4rd0sfurniture:roughstandingshelf-north', [(x, 5, 11) for x in range(3, 12)]); d.put('w4rd0sfurniture:rusticwardrobe-east', [(13, 5, 9)])
        d.lantern((8, 3, 8)); d.lantern((8, 7, 7)); d.lantern((16, 3, 4)); d.lantern((8, 2, 14), 'south'); banner(d, (10, 3, 14), 'south')
        porch(d, (6, 14, 10, 15), 1, 3, 'north')
        d.plain_walls(kiln, 1, 5, BRICK); d.hollow(kiln, 1, 5); d.floor(kiln, 6, MUD_DARK); d.rooms.append((kiln, 5))
        d.plain_walls((19, 2, 27, 10), 6, 7, MUD_DARK); d.hollow((19, 2, 27, 10), 6, 7); d.floor((19, 2, 27, 10), 8, MUD_DARK)
        for (x, z) in ring((20, 3, 26, 9)): d.set((x, 8, z), stairs('brick', 'north' if z == 3 else 'south' if z == 9 else 'west' if x == 20 else 'east'))
        for y in range(6, 15): d.set((23, y, 6), BRICK)
        d.set((23, 15, 6), slab('cobble', 'down'))
        d.door((23, 1, 11), 'solid-oak', 'north'); d.door((18, 1, 6), 'solid-oak', 'east')
        for p in ((20, 1, 3), (26, 1, 3), (20, 1, 9), (26, 1, 9), (23, 1, 4), (23, 1, 8)): d.place('game:firepit-cold', p)
        d.lantern((21, 4, 6)); d.lantern((25, 4, 6))
        lean_to(d, shed, 1, 3, 'north', open_sides=('south',), level=5, wall=lambda x, y, z, a: BRICK if y == 1 else PLANK_X)
        for x in range(19, 32, 2): d.place('game:groundstorage', (x, 1, 15)); d.place('game:groundstorage', (x, 1, 17)) if x % 4 == 1 else None
        d.box_fill((3, 17, 13, 23), -3, -1, None); d.plain_walls((3, 17, 13, 23), -3, -1, BRICK); d.ground((3, 17, 13, 23), -3, 'game:rock-claystone')
        d.set((13, -1, 20), stairs('brick', 'west')); d.set((12, -2, 20), stairs('brick', 'west')); d.set((13, -2, 20), BRICK)
        d.fence((1, 0, 35, 25), 0, 'drystonefence-shale', gaps=[(8, 25), (9, 25)])
        d.path([(x, z) for x in (8, 9) for z in range(16, 26)] + [(x, 13) for x in range(10, 18)] + [(16, z) for z in range(8, 25)] + [(x, 12) for x in range(17, 35)], -1)
        grass(d, (0, 0, 36, 26), -1, every=12)
    return d


# ---------------------------------------------------------------- Craftsman (stone tools)
@recipe('craftsman')
def craftsman(d):
    L = d.level
    if L == 1:
        # A knapper's spot: a hide-like wattle tent over a log seat and a flat stone for knapping, flint
        # baskets, a firepit.
        yard(d, (0, 0, 10, 9))
        box = (1, 1, 5, 5)
        lean_to(d, box, 1, 3, 'north', open_sides=('south',), level=1, floor=DIRT, wall=lambda x, y, z, a: fence_code('wattle-sticks', 'ew' if a else 'ns'))
        hut_block(d, (4, 1, 4))
        d.set((2, 1, 2), 'game:rock-shale'); d.put('w4rd0sfurniture:roughstool-west', [(3, 1, 3)])
        d.put('game:stationarybasket-north', [(4, 1, 2)]); d.bed((2, 1, 4), 'hay', 'east') or d.bed((2, 1, 3), 'hay')
        d.place('game:firepit-cold', (3, 0, 7))
        d.torch((3, 2, 1), 'south')
        grass(d, (0, 0, 10, 9), -1, every=6)
    elif L == 2:
        # A craftsman's hut: a daub hut with a work bench, tool racks, a stone block for knapping, shelves of
        # stone tools, a bed, a wattle-fenced yard with a stone pile and a drying rack for cordage.
        box = (1, 1, 7, 7)
        yard(d, (0, 0, 15, 12))
        house(d, box, storeys=1, height=3, axis=0, level=2)
        d.door((7, 1, 4), 'crude', 'west')
        hut_block(d, (5, 1, 5))
        d.put('w4rd0sfurniture:roughtable-north', [(3, 1, 2)]); d.put('w4rd0sfurniture:roughstool-west', [(3, 1, 3)]); d.set((5, 1, 2), 'game:rock-shale')
        d.place('game:toolrack-west', (6, 2, 2)); d.put('game:shelf-normal-west', [(2, 1, 5)]); d.put('game:stationarybasket-north', [(2, 1, 6)])
        d.bed((6, 1, 6), 'rough', 'north') or d.bed((5, 1, 6), 'rough')
        d.torch((4, 2, 6), 'north')
        for p in ((10, 0, 2), (11, 0, 2), (10, 0, 3), (11, 1, 2)): d.set(p, 'game:rock-shale')
        for (x, z) in ((10, 7), (13, 7)):
            for y in (0, 1): d.set((x, y, z), LOG)
        for x in (11, 12): d.set((x, 1, 7), fence_code('wattle-sticks', 'ew')); d.set((x, 2, 7), fence_code('wattle-sticks', 'ew'))
        d.set((10, 2, 7), LOG); d.set((13, 2, 7), LOG)
        d.fence((0, 0, 15, 11), 0, 'wattle-sticks', gaps=[(8, 11), (9, 11)])
        d.path([(8, 5), (8, 6), (9, 5)] + [(8, z) for z in range(7, 12)], -1)
        grass(d, (0, 0, 15, 12), -1, every=8)
    elif L == 3:
        # A craftsman's workshop: rammed-earth shop with three benches under the windows, a store wing of shelves
        # and baskets, a covered work porch for the stone block and the cordage rack, oak fence and path.
        shop = (2, 1, 10, 9); store = (10, 3, 15, 9)
        yard(d, (0, 0, 20, 14))
        house(d, shop, storeys=1, height=3, axis=0, level=3, no_windows=('east',))
        lean_to(d, store, 1, 3, 'west', level=3, wall=RAMMED)
        opening(d, [(10, 6)], 1, 2)
        d.door((6, 1, 9), 'solid-oak', 'north')
        hut_block(d, (9, 1, 8))
        for x in (3, 5, 8): d.put('w4rd0sfurniture:roughtable-north', [(x, 1, 2)]); d.put('w4rd0sfurniture:roughstool-west', [(x, 1, 3)])
        d.place('game:toolrack-west', (9, 2, 4)); d.place('game:toolrack-west', (9, 2, 5)); d.set((4, 1, 5), 'game:rock-shale')
        d.bed((3, 1, 7), 'rough', 'south') or d.bed((3, 1, 6), 'rough')
        d.put('w4rd0sfurniture:roughstandingshelf-north', [(11, 1, 4), (12, 1, 4), (13, 1, 4)]); d.put('game:stationarybasket-north', [(11, 1, 8), (12, 1, 8)]); d.put('game:crate', [(14, 1, 8)])
        d.lantern((6, 3, 5)); d.lantern((12, 3, 6)); d.lantern((6, 2, 10), 'south')
        porch(d, (3, 10, 9, 12), 1, 3, 'north')
        d.set((4, 0, 11), 'game:rock-shale');
        for (x, z) in ((7, 11), (9, 11)): pass
        for z in range(11, 12): d.set((7, 1, z), fence_code('wattle-sticks', 'ew')); d.set((8, 1, z), fence_code('wattle-sticks', 'ew'))
        for p in ((17, 0, 3), (18, 0, 3), (17, 0, 4), (18, 1, 3), (17, 0, 6), (18, 0, 6)): d.set(p, 'game:rock-shale')
        d.fence((1, 0, 19, 13), 0, 'woodenfence-oak', gaps=[(6, 13), (5, 13)])
        d.path([(5, 13), (6, 13)] + [(x, 12) for x in (5, 6)] + [(x, 10) for x in range(10, 19)] if False else [(5, 13), (6, 13)] + [(x, 10) for x in range(10, 19)], -1)
        grass(d, (0, 0, 20, 14), -1, every=10)
    elif L == 4:
        # A craftsmen's hall: two storeys, cobble workshop below with six benches, tool walls and the stone
        # blocks, planks above for the craftsmen's beds and a finished-goods store, a porch, a stone yard
        # with a roofed raw-stone store and a cordage walk.
        hall = (2, 1, 12, 13); store = (16, 2, 22, 8)
        yard(d, (0, 0, 26, 18))
        house(d, hall, storeys=2, height=3, axis=1, level=4)
        d.door((7, 1, 13), 'solid-oak', 'north')
        hut_block(d, (6, 1, 12))
        for z in (3, 6, 9): d.put('w4rd0sfurniture:roughtable-north', [(3, 1, z)]); d.put('w4rd0sfurniture:roughstool-west', [(4, 1, z)]); d.put('w4rd0sfurniture:roughtable-north', [(11, 1, z)]); d.put('w4rd0sfurniture:roughstool-west', [(10, 1, z)])
        for x in (5, 7, 9): d.place('game:toolrack-west', (x, 2, 2))
        d.set((7, 1, 6), 'game:rock-shale'); d.set((7, 1, 9), 'game:rock-shale'); d.put('game:stationarybasket-north', [(3, 1, 11), (11, 1, 11)])
        d.stair_run((11, 1, 11), 'west', 3, 'planks') if False else d.stair_run((10, 1, 11), 'west', 3, 'planks')
        for x in (7, 8, 9, 10): d.set((x, 4, 11), None)
        d.beds(4, [(3, 5, 3), (3, 5, 6), (11, 5, 3), (11, 5, 6)], 'wood', 'south'); d.put('w4rd0sfurniture:roughstandingshelf-north', [(3, 5, 11), (4, 5, 11), (5, 5, 11)]); d.put_against('game:chest', [(11, 5, 9)])
        d.lantern((7, 3, 8)); d.lantern((7, 7, 7)); d.lantern((7, 2, 14), 'south')
        porch(d, (5, 14, 9, 15), 1, 3, 'north')
        lean_to(d, store, 1, 3, 'east', open_sides=('west',), level=4, wall=COBBLE)
        for p in ((18, 1, 3), (19, 1, 3), (20, 1, 3), (19, 2, 3), (18, 1, 5), (20, 1, 5), (18, 1, 7), (19, 1, 7), (20, 1, 7)): d.set(p, 'game:rock-shale')
        for (x, z) in ((16, 11), (16, 15), (22, 11), (22, 15)):
            for y in (0, 1): d.set((x, y, z), LOG)
        for x in range(17, 22): d.set((x, 1, 11), fence_code('wattle-sticks', 'ew')); d.set((x, 1, 15), fence_code('wattle-sticks', 'ew'))
        d.fence((1, 0, 25, 17), 0, 'woodenfence-oak', gaps=[(7, 17), (8, 17)])
        d.path([(x, z) for x in (7, 8) for z in (16, 17)] + [(x, 13) for x in range(13, 16)] + [(14, z) for z in range(2, 17)], -1)
        grass(d, (0, 0, 26, 18), -1, every=11)
    else:
        # A craftsmen's guildhall: stone-brick workshop hall with a polished floor and eight benches, a mud-
        # brick upper floor for the masters, a hip-roofed store tower, a stone-vaulted raw store, a cordage
        # yard, drystone walls and a banner over the door.
        hall = (2, 1, 14, 15); tow = (14, 9, 20, 15); store = (14, 1, 20, 7)
        yard(d, (0, 0, 30, 22))
        house(d, hall, storeys=2, height=3, axis=1, level=5, no_windows=('east',))
        house(d, (14, 9, 22, 15), storeys=2, height=3, axis=1, level=5, no_windows=('west',))
        lean_to(d, store, 1, 3, 'west', level=5, wall=BRICK)
        opening(d, [(14, 11), (14, 12), (14, 13)], 1, 2); opening(d, [(14, 11), (14, 12), (14, 13)], 5, 6); opening(d, [(14, 4)], 1, 2)
        for z in (11, 12, 13): d.set((14, 4, z), PLANK_X)
        d.door((8, 1, 15), 'solid-oak', 'north')
        hut_block(d, (7, 1, 14))
        d.floor_inside(hall, 0, POLISHED)
        for z in (3, 6, 9, 12): d.put('w4rd0sfurniture:roughtable-north', [(3, 1, z)]); d.put('w4rd0sfurniture:roughstool-west', [(4, 1, z)]); d.put('w4rd0sfurniture:roughtable-north', [(13, 1, z)]); d.put('w4rd0sfurniture:roughstool-west', [(12, 1, z)])
        for x in (5, 7, 9, 11): d.place('game:toolrack-west', (x, 2, 2))
        d.set((8, 1, 6), 'game:rock-shale'); d.set((8, 1, 10), 'game:rock-shale')
        d.stair_run((12, 1, 13), 'west', 3, 'brick') if False else d.stair_run((11, 1, 13), 'west', 3, 'brick')
        for x in (8, 9, 10, 11): d.set((x, 4, 13), None)
        d.beds(5, [(3, 5, 3), (3, 5, 6), (3, 5, 9), (13, 5, 3), (13, 5, 6)], 'rustic', 'east'); d.put('w4rd0sfurniture:rusticwardrobe-east', [(3, 5, 13)]); d.put('w4rd0sfurniture:rusticlargeshelf-oak-south', [(8, 5, 3)]) or None
        for p in ((16, 1, 3), (18, 1, 3), (17, 2, 3), (16, 1, 6), (18, 1, 6), (17, 1, 5)): d.set(p, 'game:rock-shale')
        d.put('game:crate', [(16, 1, 11), (18, 1, 11), (16, 1, 13)]); d.put('w4rd0sfurniture:roughstandingshelf-north', [(16, 5, 11), (18, 5, 11), (16, 5, 13)]); d.put('game:stationarybasket-north', [(20, 1, 11), (20, 1, 13)]); d.bed((20, 5, 12), 'rustic', 'east')
        for y in (3, 7): d.lantern((8, y, 8)); d.lantern((17, y, 13))
        d.lantern((17, 3, 4)); d.lantern((8, 2, 16), 'south'); banner(d, (6, 3, 16), 'south'); banner(d, (10, 3, 16), 'south')
        porch(d, (6, 16, 10, 17), 1, 3, 'north')
        for (x, z) in ((22, 2, ), (22, 8), (28, 2), (28, 8)):
            for y in (0, 1): d.set((x, y, z), LOG)
        for x in range(23, 28): d.set((x, 1, 2), fence_code('wattle-sticks', 'ew')); d.set((x, 1, 8), fence_code('wattle-sticks', 'ew'))
        for z in range(3, 8): d.set((22, 1, z), fence_code('wattle-sticks', 'ns')); d.set((28, 1, z), fence_code('wattle-sticks', 'ns'))
        d.fence((1, 0, 29, 21), 0, 'drystonefence-shale', gaps=[(8, 21), (9, 21)])
        d.path([(x, z) for x in (8, 9) for z in range(18, 22)] + [(x, 17) for x in range(11, 22)] + [(21, z) for z in range(2, 18)], -1)
        grass(d, (0, 0, 30, 22), -1, every=12)
    return d


# ---------------------------------------------------------------- Tailor
@recipe('tailor')
def tailor(d):
    L = d.level
    if L == 1:
        # A sewing shelter: a small dirt hut with a wattle front, a stool and a table in the light, a basket
        # of fleece, a hay bed.
        box = (1, 1, 5, 5)
        yard(d, (0, 0, 8, 8))
        house(d, box, storeys=1, height=3, axis=0, level=1, walls=dict(WALLS[1], low=lambda x, y, z, a: fence_code('wattle-sticks', 'ew' if a else 'ns') if z == 5 and y == 2 else DIRT))
        d.door((3, 1, 5), 'crude', 'north')
        hut_block(d, (4, 1, 4))
        d.put('w4rd0sfurniture:roughtable-north', [(2, 1, 3)]); d.put('w4rd0sfurniture:roughstool-west', [(2, 1, 4)]) or None
        d.put('game:stationarybasket-north', [(4, 1, 2)]); d.bed((2, 1, 2), 'hay', 'east') or None
        d.torch((4, 2, 2), 'west')
        d.path([(3, 6), (3, 7)], -1)
        grass(d, (0, 0, 8, 8), -1, every=6)
    elif L == 2:
        # A tailor's shop: daub house with a shop front window, a cutting table, shelves of cloth, a wardrobe
        # of finished clothes, the tailor's bed behind a wattle screen, a hearth, a small garden.
        box = (1, 1, 9, 9)
        yard(d, (0, 0, 12, 13))
        house(d, box, storeys=1, height=3, axis=0, level=2)
        d.door((5, 1, 9), 'crude', 'north'); d.window((3, 2, 9), 'hole'); d.window((7, 2, 9), 'hole')
        hut_block(d, (8, 1, 8))
        d.put('w4rd0sfurniture:roughtabledouble-south', [(4, 1, 5)]) or d.put('w4rd0sfurniture:roughtable-north', [(4, 1, 5)]); d.put('w4rd0sfurniture:roughstool-west', [(3, 1, 6)])
        d.put('w4rd0sfurniture:roughstandingshelf-north', [(3, 1, 2), (4, 1, 2)]); d.put('w4rd0sfurniture:rusticwardrobe-east', [(2, 1, 7)]) or d.put('game:cabinet-bottom', [(2, 1, 7)])
        for y in (1, 2): d.set((6, y, 3), fence_code('wattle-sticks', 'ns'))
        d.bed((8, 1, 2), 'rough', 'south') or d.bed((7, 1, 2), 'rough')
        for y in (1, 2): d.set((9, y, 5), COBBLE)
        d.place('game:firepit-cold', (8, 1, 5)); chimney(d, 9, 5, 3, 7, COBBLE)
        d.torch((5, 2, 7)); d.torch((5, 2, 10), 'south')
        garden(d, (2, 11, 8, 12), -1, rows=False)
        d.fence((0, 0, 12, 12), 0, 'wattle-sticks', gaps=[(5, 12), (10, 12)])
        d.path([(5, 10), (5, 11), (5, 12)] + [(x, 10) for x in range(6, 11)] + [(10, 11), (10, 12)], -1)
        grass(d, (0, 0, 12, 13), -1, every=9)
    elif L == 3:
        # A clothier's: rammed-earth shop with a cutting room and shop front, a cross-wing workroom with two
        # tables and dye tubs (barrels), a drying line of wattle in the yard, the tailors' beds, oak fence.
        shop = (2, 1, 10, 9); work = (10, 1, 16, 9)
        yard(d, (0, 0, 20, 15))
        house(d, shop, storeys=1, height=3, axis=0, level=3, no_windows=('east',))
        house(d, work, storeys=1, height=3, axis=1, level=3, no_windows=('west',))
        opening(d, [(10, 5)], 1, 2)
        d.door((6, 1, 9), 'solid-oak', 'north'); d.window((4, 2, 9), 'trap'); d.window((8, 2, 9), 'trap')
        hut_block(d, (9, 1, 8))
        d.put('w4rd0sfurniture:roughtabledouble-south', [(5, 1, 4)]) or d.put('w4rd0sfurniture:roughtable-north', [(5, 1, 4)]); d.put('w4rd0sfurniture:roughstool-west', [(4, 1, 5)])
        d.put('w4rd0sfurniture:roughstandingshelf-north', [(3, 1, 2), (4, 1, 2), (7, 1, 2)]); d.put('w4rd0sfurniture:rusticwardrobe-east', [(3, 1, 7)]) or d.put('game:cabinet-bottom', [(3, 1, 7)]); d.put('game:cabinet-bottom', [(8, 1, 7)])
        for y in (1, 2): d.set((2, y, 5), COBBLE)
        d.place('game:firepit-cold', (3, 1, 5)) if d.free((3, 1, 5)) else None; chimney(d, 2, 5, 3, 7, COBBLE)
        d.put('w4rd0sfurniture:roughtable-north', [(12, 1, 3), (14, 1, 3)]); d.put('w4rd0sfurniture:roughstool-west', [(12, 1, 4), (14, 1, 4)]); d.put('game:barrel', [(11, 1, 7), (12, 1, 7)])
        d.beds(2, [(15, 1, 6), (15, 1, 8)], 'rough', 'east') or d.bed((14, 1, 7), 'rough')
        d.lantern((6, 3, 5)); d.lantern((13, 3, 5)); d.lantern((6, 2, 10), 'south')
        for (x, z) in ((11, 12), (17, 12)):
            for y in (0, 1): d.set((x, y, z), LOG)
        for x in range(12, 17): d.set((x, 1, 12), fence_code('wattle-sticks', 'ew'))
        d.fence((1, 0, 19, 14), 0, 'woodenfence-oak', gaps=[(6, 14), (7, 14)])
        d.path([(x, z) for x in (6, 7) for z in range(10, 15)], -1)
        grass(d, (0, 0, 20, 15), -1, every=10)
    elif L == 4:
        # A tailors' house: two storeys, a cobble shop and cutting floor below with a bow-fronted shop window
        # (a porch of windows), a plank workroom above with four tables and the tailors' beds, a dye house
        # lean-to with tubs, a drying yard, lanterns and a sign banner.
        hall = (2, 1, 12, 11); dye = (12, 3, 17, 9)
        yard(d, (0, 0, 22, 18))
        house(d, hall, storeys=2, height=3, axis=0, level=4, no_windows=('east',))
        lean_to(d, dye, 1, 3, 'west', level=4, wall=COBBLE)
        opening(d, [(12, 6)], 1, 2)
        d.door((7, 1, 11), 'solid-oak', 'north')
        for x in (4, 5, 9, 10): d.window((x, 2, 11), 'trap')
        hut_block(d, (11, 1, 10))
        d.put('w4rd0sfurniture:roughtabledouble-south', [(4, 1, 5)]) or d.put('w4rd0sfurniture:roughtable-north', [(4, 1, 5)]); d.put('w4rd0sfurniture:roughtabledouble-south', [(8, 1, 5)]) or None
        d.put('w4rd0sfurniture:roughstandingshelf-north', [(3, 1, 2), (4, 1, 2), (5, 1, 2), (9, 1, 2), (10, 1, 2)])
        d.put('w4rd0sfurniture:rusticwardrobe-east', [(3, 1, 8)]) or d.put('game:cabinet-bottom', [(3, 1, 8)]); d.put('game:cabinet-bottom', [(3, 1, 9)]); d.put('w4rd0sfurniture:rusticcupboard-east', [(10, 1, 8)]) or None
        for y in (1, 2, 3): d.set((2, y, 6), BRICK)
        d.place('game:firepit-cold', (3, 1, 6)) if d.free((3, 1, 6)) else None; chimney(d, 2, 6, 4, 9, COBBLE)
        d.stair_run((11, 1, 9), 'west', 3, 'planks') if False else d.stair_run((10, 1, 9), 'west', 3, 'planks')
        for x in (7, 8, 9, 10): d.set((x, 4, 9), None)
        for x in (3, 5, 7, 9): d.put('w4rd0sfurniture:roughtable-north', [(x, 5, 3)]); d.put('w4rd0sfurniture:roughstool-west', [(x, 5, 4)])
        d.beds(3, [(3, 5, 7), (6, 5, 7), (9, 5, 7)], 'wood', 'south'); d.put_against('game:chest', [(11, 5, 3)])
        d.put('game:barrel', [(14, 1, 4), (16, 1, 4), (14, 1, 8), (16, 1, 8)]); d.place('game:firepit-cold', (15, 1, 6))
        d.lantern((7, 3, 7)); d.lantern((7, 7, 6)); d.lantern((15, 3, 6)); d.lantern((7, 2, 12), 'south'); banner(d, (9, 3, 12), 'south')
        for (x, z) in ((13, 13), (19, 13), (13, 16), (19, 16)):
            for y in (0, 1): d.set((x, y, z), LOG)
        for x in range(14, 19): d.set((x, 1, 13), fence_code('wattle-sticks', 'ew')); d.set((x, 1, 16), fence_code('wattle-sticks', 'ew'))
        d.fence((1, 0, 21, 17), 0, 'woodenfence-oak', gaps=[(7, 17), (8, 17)])
        d.path([(x, z) for x in (7, 8) for z in range(12, 18)] + [(x, 11) for x in range(9, 20)], -1)
        grass(d, (0, 0, 22, 18), -1, every=11)
    else:
        # A clothiers' hall: a stone-brick shop hall with a polished floor and a row of shop windows under the
        # porch, a mud-brick upper floor of workrooms and chambers, a square tower for the master's quarters
        # and the pattern store, a dye house range, a walled drying yard, lanterns, banners.
        hall = (2, 1, 14, 13); tow = (14, 7, 20, 13); dye = (14, 1, 20, 6)
        yard(d, (0, 0, 28, 20))
        house(d, hall, storeys=2, height=3, axis=0, level=5, no_windows=('east',))
        house(d, tow, storeys=3, height=3, axis=1, level=5, ends='gable', no_windows=('west',))
        d.ladder(19, 12, 5, 9, 'north'); d.set((19, 8, 12), None)
        lean_to(d, dye, 1, 3, 'west', level=5, wall=BRICK)
        opening(d, [(14, 9), (14, 10), (14, 11)], 1, 2); opening(d, [(14, 9), (14, 10), (14, 11)], 5, 6); opening(d, [(14, 3), (14, 4)], 1, 2)
        for z in (9, 10, 11): d.set((14, 4, z), PLANK_X)
        d.door((8, 1, 13), 'solid-oak', 'north')
        for x in (4, 5, 6, 10, 11, 12): d.window((x, 2, 13), 'trap')
        hut_block(d, (13, 1, 12))
        d.floor_inside(hall, 0, POLISHED)
        d.put('w4rd0sfurniture:rusticsturdytabledouble-east', [(4, 1, 6)]) or d.put('w4rd0sfurniture:roughtable-north', [(4, 1, 6)]); d.put('w4rd0sfurniture:rusticsturdytabledouble-east', [(9, 1, 6)]) or None
        d.put('w4rd0sfurniture:rusticlargeshelf-oak-south', [(3, 1, 2), (6, 1, 2), (9, 1, 2)]) or d.put('w4rd0sfurniture:roughstandingshelf-north', [(3, 1, 2), (4, 1, 2)])
        d.put('w4rd0sfurniture:rusticwardrobe-east', [(3, 1, 10)]) or None; d.put('w4rd0sfurniture:rusticcabinetlarge-oak-east', [(3, 1, 8)]); d.put('w4rd0sfurniture:rusticcupboard-east', [(12, 1, 9)]) or d.put('game:cabinet-bottom', [(12, 1, 9)])
        for y in (1, 2, 3): d.set((2, y, 6), POLISHED)
        chimney(d, 2, 6, 4, 10, BRICK)
        d.stair_run((12, 1, 11), 'west', 3, 'brick') if False else d.stair_run((11, 1, 11), 'west', 3, 'brick')
        for x in (8, 9, 10, 11): d.set((x, 4, 11), None)
        for x in (3, 5, 7, 9, 11): d.put('w4rd0sfurniture:roughtable-north', [(x, 5, 3)]); d.put('w4rd0sfurniture:roughstool-west', [(x, 5, 4)])
        d.beds(4, [(3, 5, 8), (6, 5, 8), (9, 5, 8), (12, 5, 8)], 'rustic', 'south') or d.beds(4, [(3, 5, 8), (6, 5, 8), (9, 5, 8), (12, 5, 8)], 'rustic')
        d.bed((16, 5, 11), 'rustic', 'east'); d.put('w4rd0sfurniture:rusticwardrobe-east', [(18, 5, 9)]); d.put('w4rd0sfurniture:rusticlargeshelf-oak-south', [(16, 1, 9)]) or d.put('game:crate', [(16, 1, 9)]); d.put('game:crate', [(18, 1, 11), (16, 1, 11)]); d.put('w4rd0sfurniture:roughstandingshelf-north', [(16, 9, 9), (18, 9, 9)])
        d.put('game:barrel', [(16, 1, 2), (18, 1, 2), (16, 1, 5), (18, 1, 5)]); d.place('game:firepit-cold', (17, 1, 3))
        for p in ((8, 3, 9), (8, 7, 6), (17, 3, 10), (17, 7, 10), (17, 3, 4)): d.lantern(p)
        d.lantern((8, 2, 14), 'south'); banner(d, (6, 3, 14), 'south'); banner(d, (10, 3, 14), 'south')
        porch(d, (3, 14, 13, 15), 1, 3, 'north')
        d.fence((21, 8, 27, 18), 0, 'drystonefence-shale', gaps=[(21, 13)])
        for z in (10, 13, 16):
            for x in range(22, 27): d.set((x, 1, z), fence_code('wattle-sticks', 'ew'))
            for y in (0, 1): d.set((22, y, z), LOG); d.set((26, y, z), LOG)
        d.fence((1, 0, 28, 19), 0, 'drystonefence-shale', gaps=[(8, 19), (9, 19)])
        d.path([(x, z) for x in (8, 9) for z in range(16, 20)] + [(x, 16) for x in range(10, 22)], -1)
        grass(d, (0, 0, 28, 20), -1, every=12)
    return d


# ---------------------------------------------------------------- Jeweller
@recipe('jeweller')
def jeweller(d):
    L = d.level
    if L == 1:
        # A gem-cutter's booth: a tiny hut with a bench at the window, a strongbox, a lantern-lit work table.
        box = (1, 1, 5, 5)
        yard(d, (0, 0, 8, 8))
        house(d, box, storeys=1, height=3, axis=1, level=1)
        d.door((3, 1, 5), 'crude', 'north'); d.window((5, 2, 3), 'hole')
        hut_block(d, (4, 1, 4))
        d.put('w4rd0sfurniture:roughtable-north', [(4, 1, 2)]) or d.put('w4rd0sfurniture:roughtable-north', [(4, 1, 3)]); d.put('w4rd0sfurniture:roughstool-west', [(3, 1, 2)])
        d.put_against('game:chest', [(2, 1, 2)]); d.bed((2, 1, 4), 'hay', 'east') or None
        d.torch((3, 2, 4), 'north')
        d.path([(3, 6), (3, 7)], -1)
        grass(d, (0, 0, 8, 8), -1, every=6)
    elif L == 2:
        # A jeweller's shop: a neat daub house on a cobble footing with a shop window, a workbench with a stool,
        # display cases, a locked strongroom corner of cobble, a bed, a hearth.
        box = (1, 1, 9, 7)
        yard(d, (0, 0, 12, 11))
        house(d, box, storeys=1, height=3, axis=0, level=2)
        for (x, z) in ring(box):
            if d.get((x, 1, z)) in (DAUB, DAUB_WATTLE): d.set((x, 1, z), COBBLE)
        d.door((5, 1, 7), 'crude', 'north'); d.window((3, 2, 7), 'trap'); d.window((7, 2, 7), 'trap')
        hut_block(d, (8, 1, 6))
        d.put('w4rd0sfurniture:roughtable-north', [(3, 1, 3)]); d.put('w4rd0sfurniture:roughstool-west', [(3, 1, 4)])
        d.put('foodshelves:fooddisplaycase-normal-east', [(2, 1, 5)]) or d.put('game:shelf-normal-west', [(2, 1, 5)]); d.put('game:cabinet-bottom', [(6, 1, 2)])
        d.plain_walls((6, 1, 9, 4), 1, 2, COBBLE); d.hollow((6, 1, 9, 4), 1, 2); d.door((7, 1, 4), 'crude', 'north')
        d.put_against('game:chest', [(7, 1, 2), (8, 1, 2)])
        d.bed((2, 1, 2), 'rough', 'east') if False else d.bed((3, 1, 2), 'rough', 'east') or d.bed((4, 1, 2), 'rough')
        for y in (1, 2): d.set((1, y, 5), COBBLE)
        d.place('game:firepit-cold', (2, 1, 6)) if d.free((2, 1, 6)) else None; chimney(d, 1, 5, 3, 7, COBBLE)
        d.torch((5, 2, 5)); d.torch((5, 2, 8), 'south')
        d.fence((0, 0, 12, 10), 0, 'wattle-sticks', gaps=[(5, 10), (6, 10)])
        d.path([(5, 8), (5, 9), (5, 10), (6, 8), (6, 9), (6, 10)], -1)
        grass(d, (0, 0, 12, 11), -1, every=9)
    elif L == 3:
        # A goldsmith's: a rammed-earth shop with a trapdoor-windowed front, a workroom wing with two benches
        # and a small forge for the settings, a cobble strongroom under a slab roof, the smith's bed, lanterns.
        shop = (2, 1, 10, 9); work = (10, 1, 16, 7); strong = (10, 7, 14, 11)
        yard(d, (0, 0, 20, 14))
        house(d, shop, storeys=1, height=3, axis=0, level=3, no_windows=('east',))
        house(d, work, storeys=1, height=3, axis=1, level=3, no_windows=('west', 'south'))
        opening(d, [(10, 4)], 1, 2)
        d.plain_walls(strong, 1, 3, COBBLE); d.hollow(strong, 1, 3); d.floor(strong, 4, COBBLE); d.rooms.append((strong, 3))
        d.door((10, 1, 9), 'solid-oak', 'east'); d.put_against('game:chest', [(11, 1, 8), (13, 1, 8), (11, 1, 10)])
        d.door((6, 1, 9), 'solid-oak', 'north'); d.window((4, 2, 9), 'trap'); d.window((8, 2, 9), 'trap')
        hut_block(d, (9, 1, 8))
        d.put('w4rd0sfurniture:roughtable-north', [(4, 1, 3)]); d.put('w4rd0sfurniture:roughstool-west', [(4, 1, 4)])
        d.put('foodshelves:fooddisplaycase-normal-east', [(3, 1, 6), (3, 1, 7)]) or d.put('game:shelf-normal-west', [(3, 1, 6)]); d.put('game:cabinet-bottom', [(7, 1, 2)]); d.put('game:bookshelf', [(8, 1, 2)])
        d.bed((8, 1, 6), 'rough', 'south') or d.bed((7, 1, 6), 'rough')
        for y in (1, 2): d.set((2, y, 5), COBBLE)
        chimney(d, 2, 5, 3, 7, COBBLE)
        d.put('w4rd0sfurniture:roughtable-north', [(12, 1, 3), (14, 1, 3)]); d.put('w4rd0sfurniture:roughstool-west', [(12, 1, 4), (14, 1, 4)]); d.place('game:forge', (15, 1, 6)) if d.free((15, 1, 6)) else None
        d.lantern((6, 3, 5)); d.lantern((13, 3, 4)); d.lantern((12, 2, 9)); d.lantern((6, 2, 10), 'south')
        d.fence((1, 0, 19, 13), 0, 'woodenfence-oak', gaps=[(6, 13), (7, 13)])
        d.path([(x, z) for x in (6, 7) for z in range(10, 14)], -1)
        grass(d, (0, 0, 20, 14), -1, every=10)
    elif L == 4:
        # A jewellers' house: two storeys, cobble shop and strongroom below with display cases along the shop
        # windows, a plank workroom above with four benches, a forge nook with a chimney, a lantern-lit porch,
        # a small walled garden with a bench.
        hall = (2, 1, 12, 11)
        yard(d, (0, 0, 18, 18))
        house(d, hall, storeys=2, height=3, axis=0, level=4)
        d.door((7, 1, 11), 'solid-oak', 'north')
        for x in (4, 5, 9, 10): d.window((x, 2, 11), 'trap')
        hut_block(d, (11, 1, 10))
        d.put('foodshelves:fooddisplaycase-normal-east', [(3, 1, 8), (3, 1, 9), (11, 1, 8), (11, 1, 9)]) or d.put('game:cabinet-bottom', [(3, 1, 8), (11, 1, 8)])
        d.put('w4rd0sfurniture:roughtabledouble-south', [(6, 1, 8)]) or d.put('w4rd0sfurniture:roughtable-north', [(6, 1, 8)])
        d.plain_walls((2, 1, 7, 5), 1, 3, COBBLE); d.hollow((2, 1, 7, 5), 1, 3); d.door((5, 1, 5), 'solid-oak', 'north')
        d.put_against('game:chest', [(3, 1, 2), (4, 1, 2), (6, 1, 2), (3, 1, 4)])
        for y in (1, 2, 3): d.set((12, y, 3), BRICK)
        d.place('game:forge', (11, 1, 3)) if d.free((11, 1, 3)) else None; chimney(d, 12, 3, 4, 9, COBBLE)
        d.put('game:barrel', [(9, 1, 2)])
        d.stair_run((11, 1, 7), 'west', 3, 'planks') if False else d.stair_run((10, 1, 6), 'west', 3, 'planks')
        for x in (7, 8, 9, 10): d.set((x, 4, 6), None)
        for x in (3, 5, 9, 11): d.put('w4rd0sfurniture:roughtable-north', [(x, 5, 3)]); d.put('w4rd0sfurniture:roughstool-west', [(x, 5, 4)])
        d.beds(2, [(3, 5, 8), (9, 5, 8)], 'wood', 'south'); d.put('game:bookshelf', [(6, 5, 9)]); d.put('w4rd0sfurniture:rusticcabinetlarge-oak-east', [(11, 5, 8)])
        d.lantern((7, 3, 7)); d.lantern((7, 7, 6)); d.lantern((7, 2, 12), 'south'); d.lantern((4, 3, 12), 'south'); d.lantern((10, 3, 12), 'south'); banner(d, (9, 3, 12), 'south')
        porch(d, (3, 12, 11, 13), 1, 3, 'north')
        garden(d, (14, 2, 16, 8), -1, rows=False); bench(d, [(15, 0, 11)])
        d.fence((1, 0, 17, 17), 0, 'woodenfence-oak', gaps=[(7, 17), (8, 17)])
        d.path([(x, z) for x in (7, 8) for z in range(14, 18)] + [(x, 14) for x in range(9, 17)], -1)
        grass(d, (0, 0, 18, 18), -1, every=11)
    else:
        # A goldsmiths' hall: a stone-brick shop hall with a polished floor, cases and a long counter under a
        # row of windows, a mud-brick upper floor of workrooms, a strong tower with a hipped roof and a barred
        # top, a forge room, lanterns on every post, banners, drystone walls and a stone forecourt.
        hall = (2, 1, 14, 13); tow = (14, 7, 20, 13); forge = (14, 1, 20, 6)
        yard(d, (0, 0, 26, 20))
        house(d, hall, storeys=2, height=3, axis=0, level=5, no_windows=('east',))
        stone_tower(d, tow, 1, 14, roof='hip', floors_every=4, ladder=(19, 'north', 12), bars=True)
        lean_to(d, forge, 1, 3, 'west', level=5, wall=BRICK)
        opening(d, [(14, 10)], 1, 2); opening(d, [(14, 10)], 5, 6); opening(d, [(14, 4)], 1, 2)
        d.door((8, 1, 13), 'solid-oak', 'north')
        for x in (4, 5, 6, 10, 11, 12): d.window((x, 2, 13), 'trap')
        hut_block(d, (13, 1, 12))
        d.floor_inside(hall, 0, POLISHED)
        d.put('foodshelves:fooddisplaycase-normal-east', [(3, 1, 9), (3, 1, 10), (3, 1, 11), (13, 1, 9), (13, 1, 10), (13, 1, 11)]) or d.put('game:cabinet-bottom', [(3, 1, 9), (13, 1, 9)])
        for x in range(5, 12): d.set((x, 1, 8), PLANK_X) if (x % 2 == 1) else d.put('w4rd0sfurniture:rusticsturdytabledouble-east', [(x, 1, 8)])
        d.put('w4rd0sfurniture:rusticcabinetlarge-oak-east', [(3, 1, 3), (3, 1, 6)]); d.put('game:bookshelf', [(12, 1, 3)]); d.put('w4rd0sfurniture:rusticcupboard-east', [(12, 1, 5)]) or None
        for y in (1, 2, 3): d.set((2, y, 4), POLISHED)
        chimney(d, 2, 4, 4, 10, BRICK)
        d.place('game:forge', (17, 1, 3)); d.place('game:anvil-copper', (15, 1, 4)); d.put('game:barrel', [(19, 1, 5)])
        for y in (1, 2, 3): d.set((20, y, 3), BRICK)
        chimney(d, 20, 3, 4, 8, BRICK)
        d.stair_run((11, 1, 11), 'west', 3, 'brick')
        for x in (8, 9, 10, 11): d.set((x, 4, 11), None)
        for x in (3, 5, 7, 9, 11): d.put('w4rd0sfurniture:roughtable-north', [(x, 5, 3)]); d.put('w4rd0sfurniture:roughstool-west', [(x, 5, 4)])
        d.beds(3, [(3, 5, 8), (7, 5, 8), (11, 5, 8)], 'rustic', 'south') or d.beds(3, [(3, 5, 8), (7, 5, 8), (11, 5, 8)], 'rustic')
        d.put('w4rd0sfurniture:rusticwardrobe-east', [(13, 5, 10)])
        d.put_against('game:chest', [(15, 1, 8), (18, 1, 8), (15, 1, 12), (18, 1, 12)]); d.put_against('game:chest', [(15, 5, 8), (18, 5, 8)]); d.bed((16, 9, 11), 'rustic', 'east')
        for p in ((8, 3, 9), (8, 7, 6), (17, 3, 10), (17, 7, 10), (17, 11, 10), (17, 3, 4)): d.lantern(p)
        d.lantern((8, 2, 14), 'south'); d.lantern((4, 3, 14), 'south'); d.lantern((12, 3, 14), 'south'); banner(d, (6, 3, 14), 'south'); banner(d, (10, 3, 14), 'south')
        porch(d, (3, 14, 13, 15), 1, 3, 'north')
        d.ground((2, 16, 24, 18), -1, COBBLE)
        d.fence((1, 0, 25, 19), 0, 'drystonefence-shale', gaps=[(8, 19), (9, 19)])
        flag(d, (23, 0, 17)); flag(d, (3, 0, 17)) if False else None
        d.path([(x, z) for x in (8, 9) for z in (16, 17, 18, 19)], -1)
        grass(d, (0, 0, 26, 20), -1, every=12)
    return d
