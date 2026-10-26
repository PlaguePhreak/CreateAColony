"""Homes: Residence (small, medium), Tavern, Healer. Each level is its own plan."""
from parts import *

RECIPES = {}


def recipe(type, variant=''):
    def deco(fn):
        RECIPES[(type, variant)] = lambda level: fn(Design(type, variant, level))
        return fn
    return deco


# ---------------------------------------------------------------- Residence, small
@recipe('residence', 'small')
def residence_small(d):
    L = d.level
    if L == 1:
        # A settler's cot: a 5x7 packed-dirt hut with a hipped-top roof, a hay bed, a firepit under a
        # wattle windbreak by the door. The first night's shelter.
        box = (1, 1, 5, 7)
        yard(d, (0, 0, 8, 10))
        house(d, box, storeys=1, height=3, axis=1, level=1)
        d.door((3, 1, 7), 'crude', 'north')
        hut_block(d, (3, 1, 6))
        d.bed((2, 1, 2), 'hay', 'south')
        d.put('game:trunk-east', [(4, 1, 2)])
        d.torch((4, 2, 6))
        d.place('game:firepit-cold', (7, 0, 8))
        d.fence_line([(8, 7), (8, 8), (8, 9), (7, 9), (6, 9)], 0, 'wattle-sticks')
        d.path([(3, 8), (3, 9)], -1)
        grass(d, (0, 0, 8, 10), -1, every=7)
    elif L == 2:
        # A daub cottage: 7x9 oak frame with wattle-and-daub panels, a hearth of shale cobbles against the
        # north wall, two beds, a table, a window either side of the door, a wattle-fenced bed of greens.
        box = (2, 1, 8, 9)
        yard(d, (0, 0, 13, 12))
        house(d, box, storeys=1, height=3, axis=1, level=2)
        d.door((5, 1, 9), 'crude', 'north')
        d.windows([(3, 2, 9), (7, 2, 9), (8, 2, 5), (2, 2, 5)], 'wattle')
        hut_block(d, (4, 1, 8))
        # hearth: cobble back, firepit in front, chimney through the roof
        for y in (1, 2): d.set((5, y, 2), COBBLE)
        d.place('game:firepit-cold', (5, 1, 3))
        chimney(d, 5, 2, 3, 7, COBBLE)
        d.bed((3, 1, 3), 'rough', 'south'); d.bed((7, 1, 3), 'rough', 'south')
        table_and_chairs(d, [(5, 1, 6)], 'rough')
        d.put_against('game:chest', [(7, 1, 8)])
        d.torch((3, 2, 6)); d.torch((7, 2, 6))
        garden(d, (10, 2, 12, 7), -1)
        d.fence((9, 1, 13, 8), 0, 'wattle-sticks', gaps=[(9, 4)])
        d.path([(5, 10), (5, 11), (6, 10), (6, 11)], -1)
        grass(d, (0, 0, 13, 12), -1, every=9)
    elif L == 3:
        # A cottage with a cross-wing: rammed-earth walls under daub gables, the wing at a right angle for the
        # beds, a root cellar under the wing with a ladder, oak fence round a garden, stone path, lanterns.
        main = (2, 1, 8, 11); wing = (8, 5, 14, 9)
        yard(d, (0, 0, 17, 14))
        house(d, main, storeys=1, height=3, axis=1, level=3, no_windows=('east',))
        h2 = house(d, wing, storeys=1, height=3, axis=0, level=3, ends='jerkin', no_windows=('west',))
        # open the wall between main and wing
        for y in (1, 2): d.set((8, y, 7), None); d.set((8, y, 6), None)
        d.door((5, 1, 11), 'solid-oak', 'north')
        hut_block(d, (4, 1, 10))
        for y in (1, 2): d.set((5, y, 2), COBBLE)
        d.place('game:firepit-cold', (5, 1, 3)); chimney(d, 5, 2, 3, 7, COBBLE)
        d.bed((10, 1, 6), 'rough', 'south'); d.bed((12, 1, 6), 'rough', 'south'); d.bed((13, 1, 8), 'rough', 'east')
        table_and_chairs(d, [(5, 1, 7)], 'rough')
        d.put_against('game:chest', [(3, 1, 10)]); d.put('foodshelves:doubleshelf-normal-east', [(3, 1, 5)]) or d.put('game:shelf-normal-west', [(3, 1, 5)])
        # the cellar under the wing
        d.box_fill((9, 6, 13, 8), -3, -1, None); d.plain_walls((9, 6, 13, 8), -3, -1, COBBLE); d.floor((9, 6, 13, 8), -4, COBBLE)
        d.floor_inside((8, 5, 14, 9), 0, PLANK_X)
        d.set((13, 0, 8), None); d.set((13, 1, 8), None); d.ladder(13, 8, -3, 0, 'north')
        d.bed((13, 1, 8), 'rough', 'east') or None
        d.put('game:trunk-west', [(10, -3, 7)]); d.put('game:barrel', [(12, -3, 7)]); d.put('game:crate', [(11, -3, 7)])
        d.lantern((5, 3, 7)); d.lantern((11, 3, 7)); d.lantern((5, 2, 12), 'south')
        garden(d, (10, 11, 15, 13), -1)
        d.fence((1, 0, 16, 13), 0, 'woodenfence-oak', gaps=[(5, 13), (6, 13), (1, 7)])
        d.path([(5, z) for z in range(12, 14)] + [(6, 12), (6, 13)], -1)
        grass(d, (0, 0, 17, 14), -1, every=11)
    elif L == 4:
        # A two-storey house: cobble ground floor with the kitchen and hearth, an oak-plank upper storey
        # for the beds, an inside stair, a porch over the door, a walled kitchen garden.
        box = (2, 1, 8, 13)
        yard(d, (0, 0, 15, 17))
        h = house(d, box, storeys=2, height=3, axis=1, level=4)
        d.door((5, 1, 13), 'solid-oak', 'north')
        hut_block(d, (4, 1, 12))
        # hearth and chimney on the north wall
        for y in (1, 2, 3): d.set((5, y, 2), BRICK)
        d.place('game:firepit-cold', (5, 1, 3)); chimney(d, 5, 2, 4, 9, COBBLE)
        d.put('game:clayoven-east', [(3, 1, 3)])
        table_and_chairs(d, [(5, 1, 8)], 'rough')
        d.put('foodshelves:doubleshelf-normal-east', [(3, 1, 6)]); d.put_against('game:chest', [(7, 1, 11)])
        # stair up along the east wall
        d.stair_run((7, 1, 10), 'north', 3, 'planks')
        for z in (7, 8, 9, 10): d.set((7, 4, z), None)
        d.set((7, 4, 7), None)
        d.beds(4, [(3, 5, 3), (3, 5, 7), (5, 5, 3), (3, 5, 10), (5, 5, 10)], 'wood', 'south')
        d.beds(2, [(3, 5, 11), (5, 5, 11), (4, 5, 8)], 'wood')
        d.lantern((5, 3, 10)); d.lantern((5, 7, 6)); d.lantern((5, 2, 14), 'south')
        porch(d, (3, 14, 7, 15), 1, 3, 'north')
        garden(d, (10, 2, 14, 10), -1)
        d.fence((1, 0, 15, 16), 0, 'woodenfence-oak', gaps=[(5, 16), (6, 16)])
        d.path([(5, 16), (6, 16), (5, 17), (6, 17)], -1)
        for x in range(9, 15): d.path([(x, 12)], -1)
        grass(d, (0, 0, 15, 17), -1, every=10)
    else:
        # A stone house: shale-brick ground floor with a polished footing, mud-brick upper storey, an L of two
        # hipped-top roofs, six rustic beds, a stone stair, a drystone wall round the yard, lanterns, a banner.
        main = (2, 1, 10, 13); wing = (10, 7, 18, 13)
        yard(d, (0, 0, 21, 17))
        house(d, main, storeys=2, height=3, axis=1, level=5, no_windows=('east',))
        house(d, wing, storeys=2, height=3, axis=0, level=5, no_windows=('west',))
        for y in (1, 2, 5, 6):
            for z in (9, 10, 11): d.set((10, y, z), None)
        for z in (9, 10, 11): d.set((10, 4, z), PLANK_X)
        d.door((6, 1, 13), 'solid-oak', 'north')
        hut_block(d, (5, 1, 12))
        for y in (1, 2, 3): d.set((6, y, 2), POLISHED)
        d.place('game:firepit-cold', (6, 1, 3)); chimney(d, 6, 2, 4, 9, BRICK)
        d.put('game:clayoven-east', [(3, 1, 3)]); d.put('foodshelves:doubleshelf-normal-east', [(3, 1, 6)])
        table_and_chairs(d, [(6, 1, 8)], 'rustic', double=True) or table_and_chairs(d, [(6, 1, 8)], 'rustic')
        d.put('w4rd0sfurniture:rusticcabinetlarge-oak-east', [(3, 1, 10)]) or d.put_against('game:chest', [(3, 1, 10)])
        d.stair_run((9, 1, 11), 'north', 3, 'brick')
        for z in (8, 9, 10, 11): d.set((9, 4, z), None)
        d.beds(3, [(3, 5, 3), (3, 5, 6), (3, 5, 9)], 'rustic', 'east')
        d.beds(3, [(12, 5, 9), (15, 5, 9), (16, 5, 11)], 'rustic', 'east')
        d.put('w4rd0sfurniture:rusticwardrobe-east', [(12, 5, 12)]); d.put('w4rd0sfurniture:rusticnightstand-west', [(5, 5, 4)])
        d.lantern((6, 3, 10)); d.lantern((6, 7, 7)); d.lantern((14, 7, 10)); d.lantern((6, 2, 14), 'south'); d.lantern((14, 2, 14), 'south')
        banner(d, (8, 3, 14), 'south')
        garden(d, (12, 2, 18, 5), -1)
        d.fence((1, 0, 20, 16), 0, 'drystonefence-shale', gaps=[(6, 16), (7, 16)])
        d.path([(6, z) for z in range(14, 18)] + [(7, z) for z in range(14, 18)] + [(x, 15) for x in range(8, 16)], -1)
        grass(d, (0, 0, 21, 17), -1, every=12)
    return d


# ---------------------------------------------------------------- Residence, medium: longhouses
@recipe('residence', 'medium')
def residence_medium(d):
    L = d.level
    if L == 1:
        # A dirt longhouse: 7x11 packed earth under one long roof, hay beds along the walls, a fire in the middle
        # of the floor with a smoke hole, the door in the gable end.
        box = (1, 1, 7, 11)
        yard(d, (0, 0, 10, 14))
        r = house(d, box, storeys=1, height=3, axis=1, level=1)
        d.door((4, 1, 11), 'crude', 'north')
        hut_block(d, (3, 1, 10))
        d.place('game:firepit-cold', (4, 1, 6))
        d.set((4, 6, 6), None)   # smoke hole at the ridge
        d.bed((2, 1, 2), 'hay', 'south'); d.bed((6, 1, 2), 'hay', 'south'); d.bed((2, 1, 8), 'hay', 'south')
        d.put('game:trunk-east', [(6, 1, 9)])
        d.torch((2, 2, 5)); d.torch((6, 2, 5))
        d.path([(4, 12), (4, 13)], -1)
        grass(d, (0, 0, 10, 14), -1, every=7)
    elif L == 2:
        # A daub longhouse: 9x13 frame, a byre end for a trunk and baskets, four beds, a central hearth with a
        # cobble surround, a loft over the beds reached by a ladder.
        box = (2, 1, 10, 13)
        yard(d, (0, 0, 13, 16))
        house(d, box, storeys=1, height=4, axis=1, level=2)
        d.door((6, 1, 13), 'crude', 'north'); d.door((2, 1, 7), 'crude', 'east')
        hut_block(d, (5, 1, 12))
        for p in ((5, 0, 6), (7, 0, 6), (6, 0, 5), (6, 0, 7)): d.set(p, COBBLE)
        d.place('game:firepit-cold', (6, 1, 6)); d.set((6, 7, 6), None)
        d.beds(4, [(3, 1, 2), (9, 1, 2), (3, 1, 4), (9, 1, 4)], 'rough', 'south')
        # loft over the north end
        d.floor((3, 2, 9, 4), 4, PLANK_X)
        d.set((3, 4, 4), None); d.ladder(3, 4, 1, 4, 'north')
        d.put('game:trunk-east', [(8, 1, 11)]); d.put('game:stationarybasket-north', [(4, 1, 11), (3, 1, 11)])
        table_and_chairs(d, [(6, 1, 9)], 'rough')
        d.torch((4, 2, 8)); d.torch((8, 2, 8)); d.torch((6, 2, 14), 'south')
        d.fence((1, 0, 12, 15), 0, 'wattle-sticks', gaps=[(6, 15), (1, 7)])
        d.path([(6, 14), (6, 15)], -1)
        grass(d, (0, 0, 13, 16), -1, every=9)
    elif L == 3:
        # A hall house with a cross-wing: rammed earth below, daub gables, the hall for eating with a hearth
        # and a long table, the wing holding six beds in two rows, a cellar, an oak fence and a stone path.
        hall = (2, 1, 10, 11); wing = (10, 3, 18, 9)
        yard(d, (0, 0, 21, 14))
        house(d, hall, storeys=1, height=4, axis=1, level=3, no_windows=('east',))
        house(d, wing, storeys=1, height=3, axis=0, level=3, no_windows=('west',))
        for y in (1, 2):
            for z in (5, 6, 7): d.set((10, y, z), None)
        d.door((6, 1, 11), 'solid-oak', 'north')
        hut_block(d, (5, 1, 10))
        for y in (1, 2): d.set((6, y, 2), COBBLE)
        d.place('game:firepit-cold', (6, 1, 3)); chimney(d, 6, 2, 3, 8, COBBLE)
        table_and_chairs(d, [(6, 1, 6)], 'rough', double=True) or table_and_chairs(d, [(6, 1, 6)], 'rough')
        d.put('foodshelves:doubleshelf-normal-east', [(3, 1, 4)]); d.put_against('game:chest', [(9, 1, 9)])
        d.beds(6, [(12, 1, 4), (14, 1, 4), (16, 1, 4), (12, 1, 7), (14, 1, 7), (16, 1, 7)], 'rough', 'south')
        d.box_fill((11, 4, 17, 8), -3, -1, None); d.plain_walls((11, 4, 17, 8), -3, -1, COBBLE); d.floor((11, 4, 17, 8), -4, COBBLE)
        d.set((17, 0, 8), None); d.ladder(17, 8, -3, 0, 'north')
        d.put('game:barrel', [(12, -3, 5)]); d.put('game:crate', [(13, -3, 5)]); d.put('game:trunk-west', [(15, -3, 5)])
        d.lantern((6, 3, 8)); d.lantern((14, 3, 6)); d.lantern((6, 2, 12), 'south')
        garden(d, (12, 11, 18, 13), -1)
        d.fence((1, 0, 20, 13), 0, 'woodenfence-oak', gaps=[(6, 13), (7, 13)])
        d.path([(6, 12), (7, 12), (6, 13), (7, 13)], -1)
        grass(d, (0, 0, 21, 14), -1, every=11)
    elif L == 4:
        # A two-storey hall: cobble below with the hall and kitchen, oak planks above with eight beds in two
        # dormitories, a stair, a porch, a chimney, a walled garden with a well-trodden path.
        box = (2, 1, 10, 17)
        yard(d, (0, 0, 17, 21))
        house(d, box, storeys=2, height=3, axis=1, level=4)
        d.door((6, 1, 17), 'solid-oak', 'north')
        hut_block(d, (5, 1, 16))
        for y in (1, 2, 3): d.set((6, y, 2), BRICK)
        d.place('game:firepit-cold', (6, 1, 3)); chimney(d, 6, 2, 4, 10, COBBLE)
        d.put('game:clayoven-east', [(3, 1, 3)]); d.put('foodshelves:doubleshelf-normal-east', [(3, 1, 6)])
        table_and_chairs(d, [(6, 1, 8)], 'rough', double=True); table_and_chairs(d, [(6, 1, 12)], 'rough', double=True)
        d.put_against('game:chest', [(9, 1, 15), (3, 1, 15)])
        d.stair_run((9, 1, 14), 'north', 3, 'planks')
        for z in (11, 12, 13, 14): d.set((9, 4, z), None)
        d.plain_walls((2, 9, 10, 9), 5, 7, PLANK_Z); d.set((6, 5, 9), None); d.set((6, 6, 9), None)
        d.beds(4, [(3, 5, 3), (3, 5, 6), (8, 5, 3), (8, 5, 6)], 'wood', 'south')
        d.beds(4, [(3, 5, 11), (3, 5, 14), (8, 5, 11), (6, 5, 14)], 'wood', 'south')
        d.lantern((6, 3, 10)); d.lantern((6, 7, 5)); d.lantern((6, 7, 13)); d.lantern((6, 2, 18), 'south')
        porch(d, (4, 18, 8, 19), 1, 3, 'north')
        garden(d, (12, 2, 16, 12), -1)
        d.fence((1, 0, 17, 20), 0, 'woodenfence-oak', gaps=[(6, 20), (7, 20)])
        d.path([(6, 20), (7, 20), (6, 21), (7, 21)] + [(x, 15) for x in range(11, 17)], -1)
        grass(d, (0, 0, 17, 21), -1, every=10)
    else:
        # A stone courtyard house: shale brick below, mud brick above, three ranges round a yard, ten rustic
        # beds, a stair in each range, polished footing, drystone wall and gate, lanterns and a flag.
        west = (2, 1, 8, 19); north = (8, 1, 20, 7); east = (20, 1, 26, 19)
        yard(d, (0, 0, 28, 23))
        house(d, west, storeys=2, height=3, axis=1, level=5, no_windows=('east',))
        house(d, north, storeys=2, height=3, axis=0, level=5, no_windows=())
        house(d, east, storeys=2, height=3, axis=1, level=5, no_windows=('west',))
        for y in (1, 2, 5, 6):
            for z in (3, 4, 5): d.set((8, y, z), None); d.set((20, y, z), None)
        for z in (3, 4, 5): d.set((8, 4, z), PLANK_X); d.set((20, 4, z), PLANK_X)
        d.door((5, 1, 19), 'solid-oak', 'north'); d.door((23, 1, 19), 'solid-oak', 'north'); d.door((14, 1, 7), 'solid-oak', 'north')
        hut_block(d, (13, 1, 4))
        for y in (1, 2, 3): d.set((14, y, 1), POLISHED)
        d.place('game:firepit-cold', (14, 1, 2)); chimney(d, 14, 1, 4, 10, BRICK)
        table_and_chairs(d, [(12, 1, 4)], 'rustic', double=True); table_and_chairs(d, [(17, 1, 4)], 'rustic', double=True)
        d.put('game:clayoven-east', [(10, 1, 2)]); d.put('foodshelves:doubleshelf-normal-east', [(18, 1, 2)])
        d.stair_run((7, 1, 17), 'north', 3, 'brick'); d.stair_run((21, 1, 17), 'north', 3, 'brick')
        for z in (14, 15, 16, 17): d.set((7, 4, z), None); d.set((21, 4, z), None)
        d.beds(5, [(3, 5, 3), (3, 5, 6), (3, 5, 9), (3, 5, 12), (3, 5, 15)], 'rustic', 'east')
        d.beds(5, [(23, 5, 3), (23, 5, 6), (23, 5, 9), (23, 5, 12), (23, 5, 15)], 'rustic', 'east')
        d.put('w4rd0sfurniture:rusticwardrobe-east', [(10, 5, 3), (18, 5, 3)])
        for p in ((5, 3, 10), (23, 3, 10), (14, 3, 4), (5, 7, 10), (23, 7, 10), (14, 7, 4)): d.lantern(p)
        d.lantern((5, 2, 20), 'south'); d.lantern((23, 2, 20), 'south'); d.lantern((14, 2, 8), 'south')
        flag(d, (14, 0, 13))
        d.path([(x, z) for x in range(9, 20) for z in (8, 9)] + [(14, z) for z in range(10, 23)] + [(x, 20) for x in range(3, 26)], -1)
        d.fence((1, 0, 27, 22), 0, 'drystonefence-shale', gaps=[(14, 22), (13, 22), (15, 22)])
        grass(d, (0, 0, 28, 23), -1, every=12)
    return d
