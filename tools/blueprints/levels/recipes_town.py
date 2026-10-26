"""The town: Town Hall, Builder, Tavern, Church, Healer, Warehouse, Royal Mint."""
from parts import *
from recipes_homes import recipe, RECIPES


# ---------------------------------------------------------------- Town Hall
@recipe('townhall')
def townhall(d):
    L = d.level
    if L == 1:
        # A meeting shelter: an open-sided roof on oak posts over a long table and benches, a flag at the
        # door, a notice post. The colony's first gathering place, nothing more.
        box = (1, 1, 7, 9)
        yard(d, (0, 0, 10, 12))
        lean = house(d, box, storeys=1, height=3, axis=1, level=1, walls=dict(WALLS[1], low=None), post_every=4, window_sides=())
        # open sides: take the dirt infill out again, keep posts and beams
        for (x, z) in ring(box):
            for y in (1, 2):
                if d.get((x, y, z)) == DIRT: d.set((x, y, z), None)
        d.floor_inside(box, 0, DIRT)
        hut_block(d, (4, 1, 8))
        table_and_chairs(d, [(4, 1, 4)], 'rough', double=True) or table_and_chairs(d, [(4, 1, 4)], 'rough')
        bench(d, [(2, 1, 6), (6, 1, 6)])
        flag(d, (8, 0, 10))
        d.torch((2, 2, 2), 'east'); d.torch((6, 2, 2), 'west')
        d.path([(4, 10), (4, 11)], -1)
        grass(d, (0, 0, 10, 12), -1, every=7)
    elif L == 2:
        # A moot hall: 9x13 daub hall with a hearth, a long table, benches, the warden's desk and chest at
        # the far end, a flag outside, a wattle fence.
        box = (2, 1, 10, 13)
        yard(d, (0, 0, 14, 17))
        house(d, box, storeys=1, height=4, axis=1, level=2)
        d.door((6, 1, 13), 'crude', 'north')
        hut_block(d, (5, 1, 12))
        for y in (1, 2): d.set((6, y, 2), COBBLE)
        d.place('game:firepit-cold', (6, 1, 3)); chimney(d, 6, 2, 3, 9, COBBLE)
        table_and_chairs(d, [(6, 1, 7)], 'rough', double=True)
        bench(d, [(3, 1, 5), (9, 1, 5)])
        d.put('w4rd0sfurniture:roughtable-north', [(8, 1, 10)]); d.put_against('game:chest', [(9, 1, 11)])
        d.put('game:bookshelf', [(3, 1, 11)])
        d.torch((4, 2, 8)); d.torch((8, 2, 8)); d.torch((6, 2, 14), 'south')
        flag(d, (12, 0, 14))
        d.fence((1, 0, 13, 16), 0, 'wattle-sticks', gaps=[(6, 16), (7, 16)])
        d.path([(6, 14), (6, 15), (6, 16), (7, 14), (7, 15), (7, 16)], -1)
        grass(d, (0, 0, 14, 17), -1, every=9)
    elif L == 3:
        # A hall with a clerk's wing: rammed-earth hall on a cobble footing, the warden's room in a cross-wing
        # with a desk, bookshelves and a bed, a porch, a flag and banner, oak fence, stone forecourt.
        hall = (2, 1, 10, 13); wing = (10, 7, 16, 13)
        yard(d, (0, 0, 19, 18))
        house(d, hall, storeys=1, height=4, axis=1, level=3, no_windows=('east',))
        house(d, wing, storeys=1, height=3, axis=0, level=3, no_windows=('west',))
        opening(d, [(10, 9), (10, 10), (10, 11)], 1, 2)
        d.door((6, 1, 13), 'solid-oak', 'north')
        hut_block(d, (5, 1, 12))
        for y in (1, 2): d.set((6, y, 2), COBBLE)
        d.place('game:firepit-cold', (6, 1, 3)); chimney(d, 6, 2, 3, 9, COBBLE)
        table_and_chairs(d, [(6, 1, 6)], 'rough', double=True); table_and_chairs(d, [(6, 1, 9)], 'rough', double=True)
        bench(d, [(3, 1, 4), (9, 1, 4)])
        d.put('w4rd0sfurniture:roughtable-north', [(13, 1, 9)]); d.put('w4rd0sfurniture:roughchair-north', [(13, 1, 10)])
        d.put('game:bookshelf', [(15, 1, 8), (15, 1, 9)]); d.put_against('game:chest', [(15, 1, 12)])
        d.bed((12, 1, 11), 'rough', 'south')
        porch(d, (4, 14, 8, 15), 1, 3, 'north')
        d.lantern((6, 3, 8)); d.lantern((13, 3, 10)); d.lantern((6, 3, 14), 'south')
        flag(d, (1, 0, 15)); banner(d, (8, 3, 14), 'south')
        d.fence((1, 0, 18, 17), 0, 'woodenfence-oak', gaps=[(6, 17), (7, 17)])
        d.path([(x, z) for x in range(3, 10) for z in (16, 17)], -1)
        grass(d, (0, 0, 19, 18), -1, every=11)
    elif L == 4:
        # A two-storey town hall: cobble hall below with the council table, plank chamber above with the
        # records and the warden's bed, a stair, a balcony over the porch, a bell cote at the gable.
        box = (2, 1, 12, 15)
        yard(d, (0, 0, 19, 20))
        house(d, box, storeys=2, height=3, axis=1, level=4)
        d.door((7, 1, 15), 'solid-oak', 'north')
        hut_block(d, (6, 1, 14))
        for y in (1, 2, 3): d.set((7, y, 2), BRICK)
        d.place('game:firepit-cold', (7, 1, 3)); chimney(d, 7, 2, 4, 11, COBBLE)
        table_and_chairs(d, [(7, 1, 7)], 'rustic', double=True); table_and_chairs(d, [(7, 1, 10)], 'rustic', double=True)
        bench(d, [(3, 1, 5), (11, 1, 5)])
        d.put_against('game:chest', [(3, 1, 13), (11, 1, 13)])
        d.stair_run((11, 1, 12), 'north', 3, 'planks')
        for z in (9, 10, 11, 12): d.set((11, 4, z), None)
        d.put('game:bookshelf', [(3, 5, 3), (4, 5, 3), (5, 5, 3)]); d.put('w4rd0sfurniture:roughtable-north', [(7, 5, 6)])
        d.put('w4rd0sfurniture:rusticchair-east', [(6, 5, 6)]); d.bed((9, 5, 11), 'wood', 'north'); d.put('w4rd0sfurniture:rusticcabinetlarge-oak-east', [(3, 5, 9)])
        d.put('game:crate', [(3, 5, 12), (4, 5, 12)])
        # balcony: a plank slab floor on posts over the porch, with a fence
        d.floor((5, 16, 9, 17), 3, PLANK_X)
        for (x, z) in ((5, 17), (9, 17)):
            for y in (1, 2): d.set((x, y, z), LOG)
        d.fence((5, 16, 9, 17), 4, 'woodenfence-oak', gaps=[(x, 16) for x in range(5, 10)], only_on_ground=False)
        d.door((7, 4, 15), 'solid-oak', 'north')
        d.set((7, 5, 15), 'game:multiblock-monolithic-0-p1-0')
        d.lantern((7, 3, 12)); d.lantern((7, 7, 8)); d.lantern((7, 3, 16), 'south')
        flag(d, (1, 0, 17)); flag(d, (13, 0, 17)); banner(d, (4, 6, 15), 'south'); banner(d, (10, 6, 15), 'south')
        d.fence((1, 0, 18, 19), 0, 'woodenfence-oak', gaps=[(7, 19), (6, 19), (8, 19)])
        d.path([(x, z) for x in range(4, 11) for z in (18, 19)], -1)
        grass(d, (0, 0, 19, 20), -1, every=11)
    else:
        # A stone town hall: shale-brick great hall with a polished footing, a mud-brick upper chamber, a square
        # tower with a hipped roof at the corner for the bell and the lookout, a walled forecourt with flags.
        hall = (2, 1, 12, 17); tow = (12, 11, 18, 17)
        yard(d, (0, 0, 22, 23))
        house(d, hall, storeys=2, height=3, axis=1, level=5, no_windows=('east',))
        tower(d, tow, 1, 12, BRICK, roof='hip', floors_every=4, ladder=(17, 'north', 16))
        opening(d, [(12, 13), (12, 14), (12, 15)], 1, 2); opening(d, [(12, 13), (12, 14), (12, 15)], 5, 6)
        for z in (13, 14, 15): d.set((12, 4, z), PLANK_X)
        for z in (13, 14, 15): d.set((12, 8, z), None); d.set((12, 9, z), None)
        d.door((7, 1, 17), 'solid-oak', 'north'); d.door((18, 1, 14), 'solid-oak', 'west')
        hut_block(d, (6, 1, 16))
        for y in (1, 2, 3): d.set((7, y, 2), POLISHED)
        d.place('game:firepit-cold', (7, 1, 3)); chimney(d, 7, 2, 4, 11, BRICK)
        table_and_chairs(d, [(7, 1, 7)], 'rustic', double=True); table_and_chairs(d, [(7, 1, 10)], 'rustic', double=True); table_and_chairs(d, [(7, 1, 13)], 'rustic', double=True)
        bench(d, [(3, 1, 5), (11, 1, 5)])
        d.put('w4rd0sfurniture:rusticcabinetlarge-oak-east', [(3, 1, 14)]); d.put_against('game:chest', [(11, 1, 9)])
        d.stair_run((11, 1, 15), 'north', 3, 'brick')
        for z in (12, 13, 14, 15): d.set((11, 4, z), None)
        d.put('game:bookshelf', [(3, 5, 3), (4, 5, 3), (5, 5, 3), (9, 5, 3), (10, 5, 3)])
        d.put('w4rd0sfurniture:rusticdoublesturdycrossleggedtable-east', [(6, 5, 8)]) or d.put('w4rd0sfurniture:rusticsturdytabledouble-east', [(6, 5, 8)])
        d.put('w4rd0sfurniture:rusticchair-east', [(5, 5, 8)]); d.put('w4rd0sfurniture:rusticchair-west', [(8, 5, 8)])
        d.bed((9, 5, 13), 'rustic', 'east'); d.put('w4rd0sfurniture:rusticwardrobe-east', [(3, 5, 12)])
        d.put('game:crate', [(14, 1, 12), (16, 1, 12)]); d.put('game:crate', [(14, 5, 12), (16, 5, 12)])
        for y in (3, 7, 11): d.lantern((7, y, 10)); d.lantern((15, y, 14))
        d.lantern((7, 3, 18), 'south'); d.lantern((18, 3, 11), 'east')
        flag(d, (1, 0, 19)); flag(d, (13, 0, 19)); banner(d, (4, 3, 18), 'south'); banner(d, (10, 3, 18), 'south')
        d.fence((1, 0, 21, 22), 0, 'drystonefence-shale', gaps=[(6, 22), (7, 22), (8, 22)])
        d.path([(x, z) for x in range(3, 20) for z in (19, 20)] + [(x, z) for x in (6, 7, 8) for z in (21, 22)], -1)
        grass(d, (0, 0, 22, 23), -1, every=13)
    return d


# ---------------------------------------------------------------- Builder
@recipe('builder')
def builder(d):
    L = d.level
    if L == 1:
        # A builder's camp: a lean-to of logs and wattle open to the yard, tool racks, a log stack, piles of
        # stone and earth laid out on the ground, a firepit.
        box = (1, 1, 5, 7)
        yard(d, (0, 0, 12, 10))
        lean_to(d, box, 1, 3, 'west', open_sides=('east',), level=1, floor=DIRT)
        hut_block(d, (3, 1, 6))
        d.put('game:toolrack-west', [(2, 2, 3)]) or d.set((2, 2, 3), None) or d.place('game:toolrack-west', (2, 2, 3))
        d.bed((2, 1, 2), 'hay', 'south')
        d.put('game:trunk-east', [(4, 1, 2)])
        woodpile(d, [(7, 2), (7, 3), (7, 4), (8, 2), (8, 3), (8, 4)], 0, 2, 'ns')
        for p in ((10, 0, 2), (11, 0, 2), (10, 0, 3)): d.set(p, 'game:rock-shale')
        d.place('game:groundstorage', (10, 0, 6)); d.place('game:groundstorage', (8, 0, 7))
        d.place('game:firepit-cold', (8, 0, 9))
        d.torch((4, 2, 6), 'north')
        grass(d, (0, 0, 12, 10), -1, every=7)
    elif L == 2:
        # A builder's hut and yard: 7x7 daub hut with a bed, chest and tool racks, a wattle-fenced yard with a
        # sawing trestle (log on posts), a log stack and stone piles under a thatch of wattle on posts.
        box = (1, 1, 7, 7)
        yard(d, (0, 0, 16, 12))
        house(d, box, storeys=1, height=3, axis=0, level=2)
        d.door((7, 1, 4), 'crude', 'west')
        hut_block(d, (5, 1, 5))
        d.bed((2, 1, 2), 'rough', 'south'); d.put_against('game:chest', [(2, 1, 6)])
        d.place('game:toolrack-west', (6, 2, 2)); d.place('game:toolrack-west', (6, 2, 3))
        d.torch((4, 2, 6), 'north')
        # open store: posts with a wattle roof
        for (x, z) in ((10, 1), (14, 1), (10, 5), (14, 5)):
            for y in (0, 1, 2): d.set((x, y, z), LOG)
        for x in range(10, 15):
            for z in range(1, 6): d.set((x, 3, z), fence_code('wattle-sticks', 'ew'))
        woodpile(d, [(11, 2), (12, 2), (13, 2)], 0, 2, 'ns')
        for p in ((11, 0, 4), (12, 0, 4), (13, 0, 4), (12, 1, 4)): d.set(p, 'game:rock-shale')
        d.place('game:groundstorage', (11, 0, 8)); d.place('game:groundstorage', (13, 0, 8))
        d.fence((0, 0, 16, 11), 0, 'wattle-sticks', gaps=[(8, 11), (9, 11), (16, 4), (16, 3)])
        d.path([(8, z) for z in range(8, 12)] + [(9, z) for z in range(8, 12)], -1)
        grass(d, (0, 0, 16, 12), -1, every=9)
    elif L == 3:
        # A builder's workshop: a rammed-earth house with a lean-to workshop on the yard side, a drafting table,
        # shelves of plans, a bed room, a roofed store shed for timber and stone, an oak-fenced yard with a gate.
        box = (2, 1, 8, 11); shop = (8, 3, 13, 9)
        yard(d, (0, 0, 20, 14))
        house(d, box, storeys=1, height=3, axis=1, level=3, no_windows=('east',))
        lean_to(d, shop, 1, 3, 'west', open_sides=('east',), level=3)
        opening(d, [(8, 5), (8, 6)], 1, 2)
        d.door((5, 1, 11), 'solid-oak', 'north')
        hut_block(d, (4, 1, 10))
        d.bed((3, 1, 3), 'rough', 'south'); d.put_against('game:chest', [(7, 1, 3)])
        d.put('w4rd0sfurniture:roughtable-north', [(10, 1, 5)]); d.put('w4rd0sfurniture:roughstandingshelf-north', [(12, 1, 4)])
        d.place('game:toolrack-west', (12, 2, 8)); d.put('game:crate', [(10, 1, 8)])
        d.lantern((5, 3, 7)); d.lantern((10, 3, 6)); d.lantern((5, 2, 12), 'south')
        # store shed
        shed = (15, 1, 19, 11)
        lean_to(d, shed, 1, 3, 'east', open_sides=('west',), level=3, wall=PLANK_Z)
        woodpile(d, [(17, 2), (18, 2), (17, 3), (18, 3)], 0, 2, 'ns')
        for p in ((17, 0, 6), (18, 0, 6), (17, 0, 7), (18, 1, 6)): d.set(p, 'game:rock-shale')
        d.place('game:groundstorage', (17, 0, 9)); d.place('game:groundstorage', (18, 0, 10))
        d.fence((1, 0, 20, 13), 0, 'woodenfence-oak', gaps=[(5, 13), (6, 13)])
        d.path([(5, 12), (5, 13), (6, 12), (6, 13)] + [(x, 12) for x in range(7, 16)], -1)
        grass(d, (0, 0, 20, 14), -1, every=11)
    elif L == 4:
        # A master builder's house: two storeys, cobble below with the workshop (drafting table, racks,
        # chests), planks above with two beds and the plan chest, a porch, and a timber crane frame in the
        # yard over the stone and log stacks.
        box = (2, 1, 10, 13)
        yard(d, (0, 0, 22, 17))
        house(d, box, storeys=2, height=3, axis=1, level=4)
        d.door((6, 1, 13), 'solid-oak', 'north'); d.door((10, 1, 7), 'solid-oak', 'west')
        hut_block(d, (5, 1, 12))
        d.put('w4rd0sfurniture:roughtabledouble-south', [(6, 1, 4)]) or d.put('w4rd0sfurniture:roughtable-north', [(6, 1, 4)])
        d.put('w4rd0sfurniture:roughstandingshelf-north', [(3, 1, 3), (4, 1, 3)])
        d.place('game:toolrack-west', (9, 2, 3)); d.place('game:toolrack-west', (9, 2, 4))
        d.put('game:crate', [(3, 1, 10), (4, 1, 10)]); d.put_against('game:chest', [(3, 1, 7)])
        d.stair_run((9, 1, 11), 'north', 3, 'planks')
        for z in (8, 9, 10, 11): d.set((9, 4, z), None)
        d.bed((3, 5, 3), 'wood', 'south'); d.bed((7, 5, 3), 'wood', 'south')
        d.put_against('game:chest', [(3, 5, 11)]); d.put('game:bookshelf', [(5, 5, 11)])
        d.lantern((6, 3, 8)); d.lantern((6, 7, 7)); d.lantern((6, 2, 14), 'south')
        porch(d, (4, 14, 8, 15), 1, 3, 'north')
        # the crane: two tall posts with a beam, over the stone yard
        for y in range(0, 7): d.set((14, y, 4), LOG); d.set((18, y, 4), LOG)
        for x in range(14, 19): d.set((x, 7, 4), LOG_X)
        for p in ((15, 0, 3), (16, 0, 3), (17, 0, 3), (16, 1, 3), (15, 0, 5), (16, 0, 5)): d.set(p, 'game:rock-shale')
        woodpile(d, [(14, 8), (15, 8), (16, 8), (17, 8), (18, 8), (15, 9), (16, 9), (17, 9)], 0, 2, 'we')
        d.place('game:groundstorage', (14, 0, 12)); d.place('game:groundstorage', (16, 0, 12)); d.place('game:groundstorage', (18, 0, 12))
        d.fence((1, 0, 21, 16), 0, 'woodenfence-oak', gaps=[(6, 16), (7, 16), (21, 8), (21, 9)])
        d.path([(x, z) for x in (6, 7) for z in (16, 17)] + [(x, 14) for x in range(9, 20)] + [(x, z) for x in (11, 12) for z in range(5, 14)], -1)
        grass(d, (0, 0, 22, 17), -1, every=11)
    else:
        # A builders' guild: a stone-brick hall with the drafting room and store, a hipped-roof tower for the
        # master's chamber, a long roofed timber store across the yard, a stone crane base, drystone walls.
        hall = (2, 1, 10, 15); tow = (10, 9, 16, 15); store = (2, 19, 20, 23)
        yard(d, (0, 0, 24, 26))
        house(d, hall, storeys=2, height=3, axis=1, level=5, no_windows=('east',))
        tower(d, tow, 1, 11, BRICK, roof='hip', floors_every=4, ladder=(15, 'north', 14))
        opening(d, [(10, 11), (10, 12), (10, 13)], 1, 2); opening(d, [(10, 11), (10, 12), (10, 13)], 5, 6)
        for z in (11, 12, 13): d.set((10, 4, z), PLANK_X); d.set((10, 8, z), None); d.set((10, 9, z), None)
        d.door((6, 1, 15), 'solid-oak', 'north')
        hut_block(d, (5, 1, 14))
        d.put('w4rd0sfurniture:rusticsturdytabledouble-east', [(5, 1, 4)]) or d.put('w4rd0sfurniture:rusticsturdytabledouble-south', [(5, 1, 4)])
        d.put('w4rd0sfurniture:rusticlargeshelf-oak-south', [(3, 1, 3)]); d.put('game:bookshelf', [(8, 1, 3), (9, 1, 3)])
        d.place('game:toolrack-west', (9, 2, 7)); d.place('game:toolrack-west', (9, 2, 8))
        d.put('game:crate', [(3, 1, 8), (3, 1, 10), (3, 1, 12)]); d.put_against('game:chest', [(9, 1, 12)])
        d.stair_run((9, 1, 13), 'north', 3, 'brick')
        for z in (10, 11, 12, 13): d.set((9, 4, z), None)
        d.bed((3, 5, 3), 'rustic', 'east'); d.bed((3, 5, 6), 'rustic', 'east'); d.bed((13, 5, 11), 'rustic', 'east')
        d.put('w4rd0sfurniture:rusticwardrobe-east', [(3, 5, 10)]); d.put('w4rd0sfurniture:rusticcabinetlarge-oak-east', [(8, 5, 10)])
        d.put('game:crate', [(13, 1, 11), (15, 1, 11), (13, 1, 13)]); d.put('game:crate', [(13, 9, 11), (15, 9, 11)])
        for y in (3, 7): d.lantern((6, y, 9)); d.lantern((13, y, 12))
        d.lantern((6, 2, 16), 'south'); banner(d, (4, 3, 16), 'south'); banner(d, (8, 3, 16), 'south')
        # timber store: a long open shed with a jerkin roof on posts
        house(d, store, storeys=1, height=3, axis=0, level=5, walls=dict(WALLS[5], low=None, gable=MUD), post_every=3, window_sides=())
        for (x, z) in ring(store):
            for y in (1, 2):
                if d.get((x, y, z)) == BRICK: d.set((x, y, z), None)
        d.floor_inside(store, 0, COBBLE)
        woodpile(d, [(x, 21) for x in range(4, 10)], 1, 2, 'we'); woodpile(d, [(x, 21) for x in range(12, 19)], 1, 2, 'we')
        d.put('game:crate', [(4, 1, 20), (6, 1, 20), (8, 1, 20)])
        for p in ((13, 1, 20), (14, 1, 20), (15, 1, 20), (14, 2, 20)): d.set(p, 'game:rock-shale')
        # crane on a stone base
        for p in ((19, 0, 4), (19, 0, 5), (20, 0, 4), (20, 0, 5)): d.set(p, BRICK)
        for y in range(1, 8): d.set((19, y, 4), LOG)
        for x in range(15, 20): d.set((x, 8, 4), LOG_X)
        for p in ((15, 0, 3), (16, 0, 3), (17, 0, 3), (16, 1, 3), (15, 0, 5), (16, 0, 5), (17, 0, 5)): d.set(p, 'game:rock-shale')
        d.place('game:groundstorage', (14, 0, 7)); d.place('game:groundstorage', (16, 0, 7)); d.place('game:groundstorage', (18, 0, 7))
        d.fence((1, 0, 23, 25), 0, 'drystonefence-shale', gaps=[(6, 25), (7, 25), (23, 11), (23, 12)])
        d.path([(x, z) for x in (6, 7) for z in range(16, 26)] + [(x, 17) for x in range(8, 23)] + [(x, z) for x in (11, 12) for z in range(3, 9)], -1)
        grass(d, (0, 0, 24, 26), -1, every=13)
    return d


# ---------------------------------------------------------------- Tavern
@recipe('tavern')
def tavern(d):
    L = d.level
    if L == 1:
        # An ale stall: an open shelter on posts with a bar of logs, two barrels, a bench, a firepit outside.
        box = (1, 1, 7, 5)
        yard(d, (0, 0, 10, 9))
        lean_to(d, box, 1, 3, 'north', open_sides=('south', 'east', 'west'), level=1, floor=DIRT)
        hut_block(d, (6, 1, 4))
        for x in (2, 3, 4): d.set((x, 1, 4), LOG_X)
        d.put('game:barrel', [(2, 1, 2), (3, 1, 2)])
        bench(d, [(3, 0, 7), (4, 0, 7)])
        d.place('game:firepit-cold', (7, 0, 7))
        d.torch((4, 2, 1), 'south')
        grass(d, (0, 0, 10, 9), -1, every=7)
    elif L == 2:
        # An alehouse: a 9x9 daub house with a taproom (bar, barrels, two tables), the keeper's bed behind
        # the bar, a hearth, a wattle yard with benches under the eave.
        box = (1, 1, 9, 9)
        yard(d, (0, 0, 14, 13))
        house(d, box, storeys=1, height=3, axis=0, level=2)
        d.door((5, 1, 9), 'crude', 'north')
        hut_block(d, (8, 1, 8))
        for x in (3, 4, 5): d.set((x, 1, 3), LOG_X)
        d.put('game:barrel', [(2, 1, 2), (3, 1, 2), (4, 1, 2)]); d.put('aculinaryartillery:bottlerack-north', [(6, 1, 2)])
        d.bed((7, 1, 2), 'rough', 'east') or d.bed((7, 1, 2), 'rough')
        table_and_chairs(d, [(3, 1, 6)], 'rough'); table_and_chairs(d, [(7, 1, 6)], 'rough')
        for y in (1, 2): d.set((1, y, 5), COBBLE)
        d.place('game:firepit-cold', (2, 1, 5)); chimney(d, 1, 5, 3, 7, COBBLE)
        d.torch((5, 2, 7)); d.torch((5, 2, 10), 'south')
        bench(d, [(2, 0, 11), (7, 0, 11)])
        d.fence((0, 0, 13, 12), 0, 'wattle-sticks', gaps=[(5, 12), (6, 12)])
        d.path([(5, 10), (5, 11), (5, 12), (6, 10), (6, 11), (6, 12)], -1)
        grass(d, (0, 0, 14, 13), -1, every=9)
    elif L == 3:
        # An inn: rammed-earth taproom with a long bar, a cellar of barrels below with a trapdoor ladder, a
        # cross-wing with three guest beds, a porch, lanterns, a fenced yard with a firepit for the evenings.
        tap = (2, 1, 12, 9); wing = (12, 1, 18, 9)
        yard(d, (0, 0, 21, 14))
        house(d, tap, storeys=1, height=3, axis=0, level=3, no_windows=('east',))
        house(d, wing, storeys=1, height=3, axis=1, level=3, no_windows=('west',))
        opening(d, [(12, 5)], 1, 2)
        d.door((7, 1, 9), 'solid-oak', 'north')
        hut_block(d, (10, 1, 8))
        for x in range(4, 10): d.set((x, 1, 3), LOG_X)
        d.put('game:barrel', [(4, 1, 2), (6, 1, 2), (8, 1, 2)]); d.put('aculinaryartillery:bottlerack-north', [(5, 1, 2), (7, 1, 2)])
        table_and_chairs(d, [(4, 1, 6)], 'rough'); table_and_chairs(d, [(9, 1, 6)], 'rough'); bench(d, [(6, 1, 7)])
        for y in (1, 2): d.set((2, y, 5), COBBLE)
        d.place('game:firepit-cold', (3, 1, 5)); chimney(d, 2, 5, 3, 8, COBBLE)
        d.beds(3, [(14, 1, 2), (16, 1, 2), (16, 1, 7)], 'rough', 'south'); d.put_against('game:chest', [(13, 1, 8)])
        d.box_fill((3, 2, 11, 8), -3, -1, None); d.plain_walls((3, 2, 11, 8), -3, -1, COBBLE); d.floor((3, 2, 11, 8), -4, COBBLE)
        d.set((10, 0, 3), None); d.ladder(10, 3, -3, 0, 'north')
        d.put('foodshelves:barrelrack-normal-north', [(5, -3, 3), (7, -3, 3)]); d.put('game:barrel', [(4, -3, 7), (6, -3, 7), (8, -3, 7)])
        d.lantern((7, 3, 5)); d.lantern((15, 3, 5)); d.lantern((7, 2, 10), 'south'); d.lantern((5, -1, 5))
        porch(d, (5, 10, 9, 11), 1, 3, 'north')
        d.place('game:firepit-cold', (15, 0, 12)); bench(d, [(13, 0, 12), (17, 0, 12)])
        d.fence((1, 0, 20, 13), 0, 'woodenfence-oak', gaps=[(7, 13), (8, 13)])
        d.path([(7, 12), (7, 13), (8, 12), (8, 13)], -1)
        grass(d, (0, 0, 21, 14), -1, every=11)
    elif L == 4:
        # A coaching inn: two storeys, cobble taproom and kitchen below with the long bar and a clay oven,
        # six guest beds in plank rooms above, a stair, a stable lean-to at the side for the travellers'
        # animals, a sign banner, a fenced forecourt.
        box = (2, 1, 14, 11); stable = (14, 3, 19, 9)
        yard(d, (0, 0, 22, 17))
        house(d, box, storeys=2, height=3, axis=0, level=4, no_windows=('east',))
        lean_to(d, stable, 1, 3, 'west', open_sides=('east',), level=4, wall=PLANK_Z)
        d.door((8, 1, 11), 'solid-oak', 'north'); d.door((2, 1, 6), 'solid-oak', 'east')
        hut_block(d, (12, 1, 10))
        for x in range(4, 11): d.set((x, 1, 3), LOG_X)
        d.put('game:barrel', [(4, 1, 2), (6, 1, 2), (8, 1, 2), (10, 1, 2)]); d.put('aculinaryartillery:bottlerack-north', [(5, 1, 2), (7, 1, 2), (9, 1, 2)])
        d.put('game:clayoven-east', [(12, 1, 3)]); d.put('foodshelves:breadshelf-normal-north', [(12, 1, 5)])
        table_and_chairs(d, [(4, 1, 7)], 'rough', double=True); table_and_chairs(d, [(9, 1, 7)], 'rough', double=True); bench(d, [(4, 1, 9), (10, 1, 9)])
        for y in (1, 2, 3): d.set((2, y, 8), BRICK)
        d.place('game:firepit-cold', (3, 1, 8)); chimney(d, 2, 8, 4, 10, COBBLE)
        d.stair_run((13, 1, 8), 'north', 3, 'planks')
        for z in (5, 6, 7, 8): d.set((13, 4, z), None)
        d.plain_walls((8, 1, 8, 11), 5, 7, PLANK_Z); d.set((8, 5, 6), None); d.set((8, 6, 6), None)
        d.beds(3, [(3, 5, 3), (3, 5, 6), (3, 5, 9)], 'wood', 'east'); d.beds(3, [(12, 5, 3), (10, 5, 3), (11, 5, 9)], 'wood', 'south')
        d.put('w4rd0sfurniture:roughnightstand-north', [(5, 5, 3), (9, 5, 3)])
        d.put('game:hay-normal-ud', [(16, 1, 5), (17, 1, 5)]); d.put('game:trunk-north', [(16, 1, 8)]); d.set((16, 1, 5), HAY); d.set((17, 1, 5), HAY)
        d.lantern((8, 3, 6)); d.lantern((5, 7, 6)); d.lantern((11, 7, 6)); d.lantern((8, 2, 12), 'south'); d.lantern((17, 3, 6))
        porch(d, (6, 12, 10, 13), 1, 3, 'north'); banner(d, (11, 3, 12), 'south')
        d.fence((1, 0, 21, 16), 0, 'woodenfence-oak', gaps=[(8, 16), (9, 16), (21, 6), (21, 7)])
        d.path([(x, z) for x in (8, 9) for z in range(14, 17)] + [(x, 14) for x in range(10, 20)], -1)
        grass(d, (0, 0, 22, 17), -1, every=11)
    else:
        # A great inn: stone-brick hall with the taproom and kitchen, mud-brick upper floors in two ranges
        # round a courtyard, ten rustic beds, two stairs, a stone cellar of barrels, lanterns, a flag and a
        # drystone wall with a gate.
        main = (2, 1, 14, 9); west = (2, 9, 8, 21); east = (14, 9, 20, 21)
        yard(d, (0, 0, 24, 25))
        house(d, main, storeys=2, height=3, axis=0, level=5)
        house(d, west, storeys=2, height=3, axis=1, level=5, no_windows=('north',))
        house(d, east, storeys=2, height=3, axis=1, level=5, no_windows=('north',))
        opening(d, [(4, 9), (5, 9), (6, 9)], 1, 2); opening(d, [(16, 9), (17, 9), (18, 9)], 1, 2)
        opening(d, [(4, 9), (5, 9), (6, 9)], 5, 6); opening(d, [(16, 9), (17, 9), (18, 9)], 5, 6)
        for x in (4, 5, 6, 16, 17, 18): d.set((x, 4, 9), PLANK_X)
        d.door((8, 1, 15), 'solid-oak', 'west'); d.door((14, 1, 15), 'solid-oak', 'east'); d.door((11, 1, 9), 'solid-oak', 'north')
        hut_block(d, (12, 1, 8))
        for x in range(4, 11): d.set((x, 1, 3), LOG_X)
        d.put('game:barrel', [(4, 1, 2), (6, 1, 2), (8, 1, 2), (10, 1, 2)]); d.put('aculinaryartillery:bottlerack-north', [(5, 1, 2), (7, 1, 2), (9, 1, 2)])
        d.put('game:clayoven-east', [(12, 1, 3)]); d.put('foodshelves:breadshelf-normal-north', [(12, 1, 5)])
        table_and_chairs(d, [(4, 1, 6)], 'rustic', double=True); table_and_chairs(d, [(9, 1, 6)], 'rustic', double=True)
        for y in (1, 2, 3): d.set((2, y, 5), POLISHED)
        d.place('game:firepit-cold', (3, 1, 5)); chimney(d, 2, 5, 4, 10, BRICK)
        d.stair_run((7, 1, 19), 'north', 3, 'brick'); d.stair_run((15, 1, 19), 'north', 3, 'brick')
        for z in (16, 17, 18, 19): d.set((7, 4, z), None); d.set((15, 4, z), None)
        d.beds(5, [(3, 5, 11), (3, 5, 14), (3, 5, 17), (5, 5, 11), (5, 5, 14)], 'rustic', 'east')
        d.beds(5, [(17, 5, 11), (17, 5, 14), (17, 5, 17), (15, 5, 11), (15, 5, 14)], 'rustic', 'east')
        d.put('w4rd0sfurniture:rusticwardrobe-east', [(3, 5, 20), (17, 5, 20)])
        d.put('game:bookshelf', [(4, 5, 3)]); d.put('w4rd0sfurniture:rusticcupboard-east', [(10, 5, 3)]); d.bed((7, 5, 3), 'rustic', 'east')
        d.box_fill((3, 2, 13, 8), -3, -1, None); d.plain_walls((3, 2, 13, 8), -3, -1, BRICK); d.floor((3, 2, 13, 8), -4, BRICK)
        d.set((12, 0, 7), None); d.ladder(12, 7, -3, 0, 'north')
        d.put('foodshelves:barrelrack-normal-north', [(5, -3, 3), (7, -3, 3), (9, -3, 3)]); d.put('game:barrel', [(4, -3, 7), (6, -3, 7), (8, -3, 7), (10, -3, 7)])
        for p in ((8, 3, 6), (5, 3, 15), (17, 3, 15), (5, 7, 15), (17, 7, 15), (8, 7, 6), (8, -1, 5)): d.lantern(p)
        d.lantern((9, 2, 15), 'east'); d.lantern((13, 2, 15), 'west'); d.lantern((11, 2, 10), 'south')
        flag(d, (11, 0, 23)); banner(d, (9, 3, 10), 'south'); banner(d, (13, 3, 10), 'south')
        d.place('game:firepit-cold', (11, 0, 15)); bench(d, [(10, 0, 13), (12, 0, 17)])
        d.fence((1, 0, 23, 24), 0, 'drystonefence-shale', gaps=[(10, 24), (11, 24), (12, 24)])
        d.path([(x, z) for x in range(9, 14) for z in range(10, 25) if (x, z) != (11, 15)], -1)
        grass(d, (0, 0, 24, 25), -1, every=13)
    return d


# ---------------------------------------------------------------- Church
@recipe('church')
def church(d):
    L = d.level
    if L == 1:
        # A wayside shrine: a stone post under a little roof, a bench before it, flowers, a path.
        yard(d, (0, 0, 8, 8))
        for y in (0, 1, 2): d.set((4, y, 3), COBBLE)
        d.set((4, 3, 3), slab('cobble', 'down'))
        for (x, z) in ((2, 1), (6, 1), (2, 5), (6, 5)):
            for y in (0, 1, 2, 3): d.set((x, y, z), LOG)
        d.gable_roof((2, 1, 6, 5), 3, 0, ends='jerkin', hip_layers=1)
        d.rooms.append(((2, 1, 6, 5), 3))
        hut_block(d, (4, 0, 4))
        d.place('game:firepit-cold', (4, 0, 2)) if False else None
        bench(d, [(3, 0, 7), (5, 0, 7)]) or None
        d.put('w4rd0sfurniture:roughbench-west', [(4, 0, 7)])
        for p in ((1, 0, 2), (7, 0, 2), (1, 0, 4), (7, 0, 4)): d.set(p, 'game:flower-bluebell-free')
        d.torch((3, 2, 3), 'west'); d.torch((5, 2, 3), 'east')
        d.path([(4, 5), (4, 6)], -1)
    elif L == 2:
        # A chapel: a 7x11 daub chapel with a steep roof, benches in two rows, a stone altar at the east
        # end with candles, a bell post outside the door.
        box = (2, 1, 8, 11)
        yard(d, (0, 0, 10, 15))
        house(d, box, storeys=1, height=4, axis=1, level=2, windows='wattle')
        d.door((5, 1, 11), 'crude', 'north')
        hut_block(d, (3, 1, 10))
        for p in ((5, 1, 2), (4, 1, 2), (6, 1, 2)): d.set(p, COBBLE)
        d.set((5, 2, 2), slab('cobble', 'down')); d.torch((4, 2, 2)); d.torch((6, 2, 2))
        for z in (4, 6, 8): bench(d, [(3, 1, z)]); bench(d, [(7, 1, z)])
        d.torch((5, 3, 6)); d.torch((5, 2, 12), 'south')
        for y in (0, 1, 2, 3): d.set((9, y, 13), LOG)
        d.set((9, 4, 13), LOG_X); d.set((9, 3, 13), None); d.place('game:lantern-large-up', (9, 3, 13))
        d.path([(5, 12), (5, 13), (5, 14)], -1)
        grass(d, (0, 0, 10, 15), -1, every=9)
    elif L == 3:
        # A village church: rammed-earth nave with daub gables, a porch, a sanctuary a step up at the east end
        # with the altar and lanterns, a small timber bell tower on the north side, a walled yard with a path.
        nave = (2, 1, 10, 15); tow = (10, 2, 14, 6)
        yard(d, (0, 0, 17, 19))
        house(d, nave, storeys=1, height=4, axis=1, level=3, windows='hole', no_windows=('east',))
        tower(d, tow, 1, 9, PLANK_Z, roof='hip', post_every=0, posts=True)
        opening(d, [(10, 4)], 1, 2)
        for y in range(5, 8):   # the bell loft open on all sides
            for (x, z) in ring(tow):
                if d.get((x, y, z)) == PLANK_Z: d.set((x, y, z), None)
        d.floor_inside(tow, 5, PLANK_X); d.ladder(13, 5, 1, 5, 'north'); d.set((13, 5, 5), None)
        d.place('game:lantern-large-up', (12, 6, 4))
        d.door((6, 1, 15), 'solid-oak', 'north')
        hut_block(d, (3, 1, 14))
        for x in range(3, 10): d.set((x, 1, 3), COBBLE); d.set((x, 1, 2), COBBLE)
        for x in range(3, 10): d.set((x, 1, 4), stairs('cobble', 'north'))
        for p in ((5, 2, 2), (6, 2, 2), (7, 2, 2)): d.set(p, POLISHED)
        d.lantern((4, 3, 2), 'south'); d.lantern((8, 3, 2), 'south')
        for z in (6, 8, 10, 12): bench(d, [(3, 1, z)]); bench(d, [(8, 1, z)])
        d.lantern((6, 4, 9)); d.lantern((6, 3, 16), 'south')
        porch(d, (4, 16, 8, 17), 1, 3, 'north')
        banner(d, (4, 4, 16), 'south'); banner(d, (8, 4, 16), 'south')
        d.fence((1, 0, 16, 18), 0, 'woodenfence-oak', gaps=[(6, 18), (7, 18)])
        d.path([(6, 18), (7, 18)], -1)
        grass(d, (0, 0, 17, 19), -1, every=11)
    elif L == 4:
        # A church with a tower: cobble nave, a square bell tower over the west door with a hipped roof, a
        # chancel under its own lower roof at the east end, a vestry lean-to, lanterns, a walled churchyard.
        nave = (4, 1, 12, 17); chancel = (5, -5, 11, 1); tow = (5, 17, 11, 23); vestry = (12, 3, 16, 7)
        yard(d, (0, -8, 20, 27))
        house(d, nave, storeys=1, height=6, axis=1, level=4, windows='trap', no_windows=('north',))
        h2 = house(d, chancel, storeys=1, height=4, axis=1, level=4, no_windows=('south',))
        tower(d, tow, 1, 14, COBBLE, roof='hip', floors_every=5, ladder=(10, 'north', 22))
        lean_to(d, vestry, 1, 3, 'west', level=4, wall=PLANK_Z)
        opening(d, [(7, 1), (8, 1), (9, 1)], 1, 3); opening(d, [(7, 17), (8, 17), (9, 17)], 1, 3); opening(d, [(12, 5)], 1, 2)
        for y in range(11, 14):
            for (x, z) in ring(tow):
                if (x + z) % 2 == 0 and d.get((x, y, z)) == COBBLE: d.set((x, y, z), None)
        d.door((8, 1, 23), 'solid-oak', 'north')
        hut_block(d, (10, 1, 16))
        for x in range(6, 11): d.set((x, 1, -1), BRICK); d.set((x, 1, 0), BRICK)
        for x in range(6, 11): d.set((x, 1, 1), stairs('brick', 'north'))
        for p in ((7, 2, -3), (8, 2, -3), (9, 2, -3)): d.set(p, POLISHED)
        d.lantern((6, 3, -3), 'south'); d.lantern((10, 3, -3), 'south')
        for z in (4, 6, 8, 10, 12, 14): bench(d, [(5, 1, z), (6, 1, z)]); bench(d, [(10, 1, z), (11, 1, z)])
        d.lantern((8, 5, 6)); d.lantern((8, 5, 12)); d.lantern((8, 3, 20)); d.lantern((8, 3, 24), 'south')
        d.put('game:bookshelf', [(14, 1, 4)]); d.put_against('game:chest', [(14, 1, 6)])
        banner(d, (6, 4, 24), 'south'); banner(d, (10, 4, 24), 'south')
        d.fence((1, -7, 19, 26), 0, 'woodenfence-oak', gaps=[(8, 26), (7, 26), (9, 26)])
        d.path([(x, z) for x in (7, 8, 9) for z in (24, 25, 26)], -1)
        grass(d, (0, -8, 20, 27), -1, every=11)
    else:
        # A stone church: shale-brick nave with aisles under lean-to roofs, a tall tower with a hipped spire,
        # a chancel with a polished stone floor, lanterns down the nave, banners, a drystone churchyard wall.
        nave = (6, 1, 14, 21); chancel = (7, -5, 13, 1); tow = (6, 21, 14, 29); aisle_w = (2, 3, 6, 19); aisle_e = (14, 3, 18, 19)
        yard(d, (0, -8, 22, 33))
        house(d, nave, storeys=1, height=8, axis=1, level=5, windows='trap', no_windows=('north', 'east', 'west'))
        house(d, chancel, storeys=1, height=5, axis=1, level=5, no_windows=('south',))
        tower(d, tow, 1, 20, BRICK, roof='hip', floors_every=6, ladder=(13, 'north', 28))
        lean_to(d, aisle_w, 1, 4, 'east', level=5, wall=BRICK, open_sides=('east',))
        lean_to(d, aisle_e, 1, 4, 'west', level=5, wall=BRICK, open_sides=('west',))
        for z in range(5, 18, 3): opening(d, [(6, z), (14, z)], 1, 3)
        for z in range(4, 19):
            for y in (6, 7):
                if (z - 4) % 3 == 1: d.set((6, y, z), None); d.set((14, y, z), None)
        opening(d, [(9, 1), (10, 1), (11, 1)], 1, 4); opening(d, [(9, 21), (10, 21), (11, 21)], 1, 3)
        for y in range(15, 19):
            for (x, z) in ring(tow):
                if (x + z) % 2 == 0 and d.get((x, y, z)) == BRICK: d.set((x, y, z), None)
        d.door((10, 1, 29), 'solid-oak', 'north'); d.door((2, 1, 11), 'solid-oak', 'east')
        hut_block(d, (12, 1, 20))
        for x in range(8, 13): d.set((x, 1, -1), POLISHED); d.set((x, 1, 0), POLISHED)
        for x in range(8, 13): d.set((x, 1, 1), stairs('brick', 'north'))
        for p in ((9, 2, -3), (10, 2, -3), (11, 2, -3)): d.set(p, POLISHED)
        d.place('game:lantern-large-up', (10, 3, -3))
        d.lantern((8, 4, -3), 'south'); d.lantern((12, 4, -3), 'south')
        for z in range(4, 19, 2): bench(d, [(7, 1, z), (8, 1, z)]); bench(d, [(12, 1, z), (13, 1, z)])
        for z in (6, 11, 16): d.lantern((10, 6, z)); d.lantern((4, 3, z)); d.lantern((16, 3, z))
        d.lantern((10, 4, 25)); d.lantern((10, 4, 30), 'south')
        banner(d, (8, 5, 30), 'south'); banner(d, (12, 5, 30), 'south'); banner(d, (7, 6, 20), 'south'); banner(d, (13, 6, 20), 'south')
        d.put('game:bookshelf', [(3, 1, 5), (3, 1, 6)]); d.put_against('game:chest', [(17, 1, 5)])
        d.fence((1, -7, 21, 32), 0, 'drystonefence-shale', gaps=[(9, 32), (10, 32), (11, 32)])
        d.path([(x, z) for x in (9, 10, 11) for z in (30, 31, 32)] + [(x, 23) for x in range(2, 6)] + [(4, z) for z in range(20, 24)], -1)
        grass(d, (0, -8, 22, 33), -1, every=13)
    return d


# ---------------------------------------------------------------- Healer
@recipe('healer')
def healer(d):
    L = d.level
    if L == 1:
        # A herb-wife's tent: a wattle and dirt hut with a sick bed under the eave, drying shelf, a
        # bed of herbs by the door.
        box = (1, 1, 5, 7)
        yard(d, (0, 0, 10, 10))
        house(d, box, storeys=1, height=3, axis=1, level=1, walls=dict(WALLS[1], low=lambda x, y, z, a: fence_code('wattle-sticks', 'ew' if a else 'ns') if y == 2 else DIRT))
        d.door((3, 1, 7), 'crude', 'north')
        hut_block(d, (4, 1, 6))
        d.bed((2, 1, 2), 'hay', 'south'); d.put('game:shelf-normal-west', [(2, 1, 5)]) or d.put('game:shelf-normal-south', [(4, 1, 2)])
        d.torch((4, 2, 4), 'west')
        garden(d, (7, 2, 9, 6), -1, rows=False); d.fence((6, 1, 10, 7), 0, 'wattle-sticks', gaps=[(6, 4)])
        d.place('game:firepit-cold', (3, 0, 9))
        grass(d, (0, 0, 10, 10), -1, every=7)
    elif L == 2:
        # A healer's cottage: daub house with a sick room of two beds, the healer's own bed, a work table,
        # shelves of jars, a hearth, and a fenced physic garden in rows.
        box = (2, 1, 8, 11)
        yard(d, (0, 0, 15, 14))
        house(d, box, storeys=1, height=3, axis=1, level=2)
        d.door((5, 1, 11), 'crude', 'north')
        hut_block(d, (4, 1, 10))
        for y in (1, 2): d.set((5, y, 2), COBBLE)
        d.place('game:firepit-cold', (5, 1, 3)); chimney(d, 5, 2, 3, 7, COBBLE)
        d.bed((3, 1, 5), 'rough', 'south'); d.bed((7, 1, 5), 'rough', 'south'); d.bed((7, 1, 9), 'rough', 'north') or d.bed((7, 1, 8), 'rough')
        d.put('w4rd0sfurniture:roughtable-north', [(3, 1, 8)]); d.put('foodshelves:jarstand-normal-east', [(3, 1, 9)]) or d.put('game:shelf-normal-west', [(3, 1, 9)])
        d.torch((5, 2, 7)); d.torch((5, 2, 12), 'south')
        garden(d, (10, 2, 14, 9), -1); d.fence((9, 1, 15, 10), 0, 'wattle-sticks', gaps=[(9, 5)])
        d.path([(5, 12), (5, 13), (6, 12), (6, 13)], -1)
        grass(d, (0, 0, 15, 14), -1, every=9)
    elif L == 3:
        # A healer's house with a ward: rammed-earth house for the healer (bed, table, stillroom shelves) and a
        # cross-wing ward of four beds with its own door to the garden, a cellar for stores, lanterns.
        home = (2, 1, 8, 11); ward = (8, 3, 16, 9)
        yard(d, (0, 0, 20, 14))
        house(d, home, storeys=1, height=3, axis=1, level=3, no_windows=('east',))
        house(d, ward, storeys=1, height=3, axis=0, level=3, no_windows=('west',))
        opening(d, [(8, 6)], 1, 2)
        d.door((5, 1, 11), 'solid-oak', 'north'); d.door((12, 1, 9), 'solid-oak', 'north')
        hut_block(d, (4, 1, 10))
        for y in (1, 2): d.set((5, y, 2), COBBLE)
        d.place('game:firepit-cold', (5, 1, 3)); chimney(d, 5, 2, 3, 7, COBBLE)
        d.bed((3, 1, 7), 'rough', 'south'); d.put('w4rd0sfurniture:roughtable-north', [(6, 1, 7)])
        d.put('foodshelves:jarstand-normal-east', [(3, 1, 4)]); d.put('foodshelves:doubleshelf-normal-east', [(3, 1, 5)])
        d.beds(4, [(10, 1, 4), (12, 1, 4), (14, 1, 4), (15, 1, 7)], 'rough', 'south')
        d.put('w4rd0sfurniture:roughnightstand-north', [(11, 1, 4), (13, 1, 4)])
        d.box_fill((3, 2, 7, 10), -3, -1, None); d.plain_walls((3, 2, 7, 10), -3, -1, COBBLE); d.floor((3, 2, 7, 10), -4, COBBLE)
        d.set((7, 0, 9), None); d.ladder(7, 9, -3, 0, 'north')
        d.put('game:barrel', [(4, -3, 3)]); d.put('game:crate', [(5, -3, 3)]); d.put('foodshelves:jarstand-normal-east', [(4, -3, 8)])
        d.lantern((5, 3, 6)); d.lantern((12, 3, 6)); d.lantern((5, 2, 12), 'south'); d.lantern((5, -1, 6))
        garden(d, (9, 11, 16, 13), -1)
        d.fence((1, 0, 19, 13), 0, 'woodenfence-oak', gaps=[(5, 13), (6, 13)])
        d.path([(5, 12), (5, 13), (6, 12), (6, 13)] + [(x, 10) for x in range(9, 17)], -1)
        grass(d, (0, 0, 20, 14), -1, every=11)
    elif L == 4:
        # An infirmary: two storeys, a cobble ward of six beds below with a hearth, the healer's rooms and
        # stillroom above (shelves, table, bed), a porch, a walled physic garden on two sides with paths.
        box = (2, 1, 10, 15)
        yard(d, (0, 0, 19, 19))
        house(d, box, storeys=2, height=3, axis=1, level=4)
        d.door((6, 1, 15), 'solid-oak', 'north')
        hut_block(d, (5, 1, 14))
        for y in (1, 2, 3): d.set((6, y, 2), BRICK)
        d.place('game:firepit-cold', (6, 1, 3)); chimney(d, 6, 2, 4, 10, COBBLE)
        d.beds(6, [(3, 1, 5), (3, 1, 8), (3, 1, 11), (9, 1, 5), (9, 1, 8), (9, 1, 11)], 'wood', 'south')
        d.put('w4rd0sfurniture:roughnightstand-north', [(4, 1, 5), (8, 1, 5), (4, 1, 8), (8, 1, 8)])
        d.stair_run((9, 1, 14), 'west', 3, 'planks') if False else d.stair_run((8, 1, 13), 'west', 3, 'planks')
        for x in (5, 6, 7, 8): d.set((x, 4, 13), None)
        d.bed((3, 5, 3), 'wood', 'south'); d.put('w4rd0sfurniture:roughtabledouble-south', [(6, 5, 7)]) or d.put('w4rd0sfurniture:roughtable-north', [(6, 5, 7)])
        d.put('foodshelves:jarstand-normal-east', [(3, 5, 7), (3, 5, 8)]); d.put('foodshelves:doubleshelf-normal-east', [(3, 5, 10)]); d.put('game:bookshelf', [(9, 5, 4)])
        d.lantern((6, 3, 9)); d.lantern((6, 7, 7)); d.lantern((6, 2, 16), 'south')
        porch(d, (4, 16, 8, 17), 1, 3, 'north')
        garden(d, (12, 2, 18, 8), -1); garden(d, (12, 11, 18, 14), -1)
        d.fence((1, 0, 19, 18), 0, 'woodenfence-oak', gaps=[(6, 18), (7, 18)])
        d.path([(x, z) for x in (6, 7) for z in (18, 19)] + [(x, 9) for x in range(11, 19)] + [(11, z) for z in range(2, 16)], -1)
        grass(d, (0, 0, 19, 19), -1, every=11)
    else:
        # A hospital: a stone-brick ward hall with ten beds, a stillroom wing, the healers' chambers above, a
        # covered walk along the garden, polished floors, lanterns everywhere, a drystone-walled herb garden.
        hall = (2, 1, 10, 21); wing = (10, 13, 18, 21); walk = (10, 2, 14, 12)
        yard(d, (0, 0, 24, 25))
        house(d, hall, storeys=2, height=3, axis=1, level=5, no_windows=('east',))
        house(d, wing, storeys=2, height=3, axis=0, level=5, no_windows=('west',))
        opening(d, [(10, 16), (10, 17), (10, 18)], 1, 2); opening(d, [(10, 16), (10, 17), (10, 18)], 5, 6)
        for z in (16, 17, 18): d.set((10, 4, z), PLANK_X)
        porch(d, walk, 1, 3, 'west')
        d.door((6, 1, 21), 'solid-oak', 'north'); d.door((10, 1, 7), 'solid-oak', 'west')
        hut_block(d, (5, 1, 20))
        for y in (1, 2, 3): d.set((6, y, 2), POLISHED)
        d.place('game:firepit-cold', (6, 1, 3)); chimney(d, 6, 2, 4, 10, BRICK)
        d.floor_inside(hall, 0, POLISHED)
        d.beds(10, [(3, 1, 5), (3, 1, 8), (3, 1, 11), (3, 1, 14), (3, 1, 17), (9, 1, 9), (9, 1, 12), (9, 1, 15), (9, 1, 18), (9, 1, 5)], 'rustic', 'east')
        d.put('w4rd0sfurniture:rusticnightstand-west', [(4, 1, 5), (4, 1, 8), (4, 1, 11), (4, 1, 14)])
        d.put('w4rd0sfurniture:rusticsturdytabledouble-east', [(12, 1, 15)]) or d.put('w4rd0sfurniture:roughtable-north', [(12, 1, 15)])
        d.put('foodshelves:jarstand-normal-east', [(17, 1, 15), (17, 1, 16)]); d.put('foodshelves:doubleshelf-normal-east', [(17, 1, 19)]); d.put('game:barrel', [(12, 1, 19), (14, 1, 19)])
        d.stair_run((17, 1, 17), 'south', 3, 'brick') if False else d.stair_run((13, 1, 20), 'east', 3, 'brick')
        for x in (13, 14, 15, 16): d.set((x, 4, 20), None)
        d.beds(2, [(3, 5, 3), (3, 5, 6)], 'rustic', 'east'); d.put('w4rd0sfurniture:rusticwardrobe-east', [(3, 5, 10)]); d.put('game:bookshelf', [(8, 5, 3), (9, 5, 3)])
        d.put('w4rd0sfurniture:rusticchairdining-east', [(5, 5, 8)]); d.put('w4rd0sfurniture:rusticsturdytabledouble-south', [(6, 5, 8)])
        for p in ((6, 3, 7), (6, 3, 13), (6, 3, 18), (6, 7, 10), (14, 3, 17), (14, 7, 17)): d.lantern(p)
        d.lantern((6, 2, 22), 'south'); d.lantern((12, 3, 1), 'north')
        garden(d, (16, 2, 22, 11), -1)
        d.fence((1, 0, 23, 24), 0, 'drystonefence-shale', gaps=[(6, 24), (7, 24)])
        d.path([(x, z) for x in (6, 7) for z in (22, 23, 24)] + [(x, 7) for x in range(11, 16)] + [(15, z) for z in range(2, 13)], -1)
        grass(d, (0, 0, 24, 25), -1, every=13)
    return d


# ---------------------------------------------------------------- Warehouse
@recipe('warehouse')
def warehouse(d):
    L = d.level
    if L == 1:
        # A store tent: a wattle lean-to on posts with chests and baskets on a dirt floor.
        box = (1, 1, 7, 5)
        yard(d, (0, 0, 9, 8))
        lean_to(d, box, 1, 3, 'north', open_sides=('south',), level=1, floor=DIRT, wall=lambda x, y, z, a: fence_code('wattle-sticks', 'ew' if a else 'ns'))
        hut_block(d, (6, 1, 4))
        storage(d, 4, [(2, 1, 2), (3, 1, 2), (4, 1, 2), (5, 1, 2)], ('game:chest-south', 'game:stationarybasket-north', 'game:chest-south', 'game:trunk-east'))
        d.torch((4, 2, 1), 'south')
        grass(d, (0, 0, 9, 8), -1, every=7)
    elif L == 2:
        # A storehouse: a daub barn with wide doors, chests along both walls, crates and barrels, a loft
        # for sacks reached by a ladder.
        box = (1, 1, 9, 11)
        yard(d, (0, 0, 12, 14))
        house(d, box, storeys=1, height=4, axis=1, level=2, window_sides=('east', 'west'))
        d.door((4, 1, 11), 'crude', 'north'); d.door((6, 1, 11), 'crude', 'north')
        hut_block(d, (8, 1, 10))
        storage(d, 8, [(2, 1, 2), (2, 1, 4), (2, 1, 6), (2, 1, 8), (8, 1, 2), (8, 1, 4), (8, 1, 6), (8, 1, 8)])
        d.floor((2, 2, 8, 5), 4, PLANK_X); d.set((2, 4, 5), None); d.ladder(2, 5, 1, 4, 'north')
        d.put('foodshelves:floursack-normal-east', [(4, 5, 3), (6, 5, 3)]); d.put('game:crate', [(5, 5, 3)])
        d.torch((5, 2, 6)); d.torch((5, 2, 12), 'south')
        d.path([(4, 12), (5, 12), (6, 12), (5, 13)], -1)
        grass(d, (0, 0, 12, 14), -1, every=9)
    elif L == 3:
        # A warehouse with a loading bay: rammed-earth store with a covered bay on posts in front of the doors,
        # rows of chests and crates, a counting desk, a loft, and an open wagon shed across the yard.
        box = (2, 1, 12, 13); bay = (4, 14, 10, 16)
        yard(d, (0, 0, 20, 19))
        house(d, box, storeys=1, height=4, axis=1, level=3, window_sides=('east', 'west'))
        porch(d, bay, 1, 3, 'north')
        d.door((6, 1, 13), 'solid-oak', 'north'); d.door((8, 1, 13), 'solid-oak', 'north')
        hut_block(d, (11, 1, 12))
        storage(d, 12, [(3, 1, 2), (3, 1, 4), (3, 1, 6), (3, 1, 8), (3, 1, 10), (11, 1, 2), (11, 1, 4), (11, 1, 6), (11, 1, 8), (11, 1, 10), (6, 1, 4), (8, 1, 4)])
        d.put('w4rd0sfurniture:roughtable-north', [(10, 1, 11)]); d.put('w4rd0sfurniture:roughstool-west', [(10, 1, 10)])
        d.floor((3, 2, 11, 6), 4, PLANK_X); d.set((3, 4, 6), None); d.ladder(3, 6, 1, 4, 'north')
        d.put('foodshelves:floursack-normal-east', [(5, 5, 3), (7, 5, 3), (9, 5, 3)]); d.put('game:crate', [(5, 5, 5), (9, 5, 5)])
        d.lantern((7, 3, 7)); d.lantern((7, 3, 11)); d.lantern((7, 3, 15))
        shed = (14, 2, 19, 10)
        lean_to(d, shed, 1, 3, 'east', open_sides=('west',), level=3, wall=PLANK_Z)
        d.put('game:crate', [(17, 1, 4), (17, 1, 6)]); d.put('game:barrel', [(17, 1, 8)])
        d.fence((1, 0, 20, 18), 0, 'woodenfence-oak', gaps=[(6, 18), (7, 18), (8, 18)])
        d.path([(x, z) for x in (6, 7, 8) for z in (17, 18)] + [(x, 12) for x in range(13, 19)], -1)
        grass(d, (0, 0, 20, 19), -1, every=11)
    elif L == 4:
        # A two-storey warehouse: cobble ground floor for heavy goods with a wide bay, a plank upper floor for
        # sacks and crates with a hoist beam jutting from the gable, a stair, the clerk's desk, lanterns.
        box = (2, 1, 12, 15)
        yard(d, (0, 0, 20, 20))
        house(d, box, storeys=2, height=3, axis=1, level=4, window_sides=('east', 'west'))
        d.door((6, 1, 15), 'solid-oak', 'north'); d.door((8, 1, 15), 'solid-oak', 'north')
        hut_block(d, (11, 1, 14))
        storage(d, 14, [(3, 1, 2), (3, 1, 4), (3, 1, 6), (3, 1, 8), (3, 1, 10), (3, 1, 12), (11, 1, 2), (11, 1, 4), (11, 1, 6), (11, 1, 8), (11, 1, 10), (6, 1, 5), (8, 1, 5), (7, 1, 9)])
        d.put('w4rd0sfurniture:roughtable-north', [(10, 1, 13)]); d.put('w4rd0sfurniture:roughstool-west', [(10, 1, 12)])
        d.stair_run((3, 1, 13), 'east', 3, 'planks')
        for x in (3, 4, 5, 6): d.set((x, 4, 13), None)
        d.put('foodshelves:floursack-normal-east', [(3, 5, 3), (5, 5, 3), (7, 5, 3), (9, 5, 3)])
        d.put('game:crate', [(3, 5, 6), (5, 5, 6), (3, 5, 9), (11, 5, 6), (11, 5, 9)]); d.put('game:barrel', [(9, 5, 6), (9, 5, 9)])
        # hoist: a beam out of the south gable with a hatch door below it
        for z in (16, 17, 18): d.set((7, 8, z), LOG_Z)
        d.door((7, 5, 15), 'solid-oak', 'north'); d.set((7, 6, 15), 'game:multiblock-monolithic-0-p1-0')
        porch(d, (4, 16, 10, 17), 1, 3, 'north')
        for z in (5, 9, 13): d.lantern((7, 3, z)); d.lantern((7, 7, z))
        d.lantern((7, 3, 17), 'south')
        d.fence((1, 0, 19, 19), 0, 'woodenfence-oak', gaps=[(x, 19) for x in range(5, 10)])
        d.path([(x, z) for x in range(5, 10) for z in (18, 19)], -1)
        grass(d, (0, 0, 20, 20), -1, every=11)
    else:
        # A stone warehouse: a long shale-brick store of two storeys with a hoist gable, a cobbled yard
        # enclosed by a drystone wall with a gate, a covered weighing porch, a side store for barrels, a
        # counting room with a desk and chest, lanterns on every post.
        box = (2, 1, 14, 19); side = (14, 5, 19, 15)
        yard(d, (0, 0, 24, 25))
        house(d, box, storeys=2, height=3, axis=1, level=5, window_sides=('east', 'west'))
        lean_to(d, side, 1, 3, 'west', level=5, wall=BRICK, open_sides=('east',))
        opening(d, [(14, 9), (14, 10), (14, 11)], 1, 2)
        d.door((7, 1, 19), 'solid-oak', 'north'); d.door((9, 1, 19), 'solid-oak', 'north')
        d.floor_inside(box, 0, COBBLE)
        hut_block(d, (8, 1, 17))
        storage(d, 18, [(3, 1, z) for z in range(2, 17, 2)] + [(13, 1, z) for z in range(2, 16, 2)] + [(7, 1, 5), (9, 1, 5), (7, 1, 9)])
        d.put('game:barrel', [(16, 1, 7), (17, 1, 7), (16, 1, 9), (17, 1, 9), (16, 1, 12), (17, 1, 12)])
        d.plain_walls((9, 14, 13, 18), 1, 3, MUD); d.hollow((9, 14, 13, 18), 1, 3); d.door((11, 1, 14), 'solid-oak', 'north')
        d.put('w4rd0sfurniture:rusticsturdytabledouble-east', [(10, 1, 16)]) or d.put('w4rd0sfurniture:roughtable-north', [(10, 1, 16)])
        d.put('w4rd0sfurniture:rusticchair-west', [(12, 1, 16)]); d.put_against('game:chest', [(12, 1, 17)])
        d.stair_run((3, 1, 17), 'east', 3, 'brick')
        for x in (3, 4, 5, 6): d.set((x, 4, 17), None)
        d.put('foodshelves:floursack-normal-east', [(3, 5, z) for z in range(3, 12, 2)]); d.put('game:crate', [(13, 5, z) for z in range(3, 14, 2)]); d.put('game:barrel', [(7, 5, 6), (9, 5, 6), (7, 5, 10), (9, 5, 10)])
        for z in (20, 21, 22): d.set((8, 8, z), LOG_Z)
        d.door((8, 5, 19), 'solid-oak', 'north'); d.set((8, 6, 19), 'game:multiblock-monolithic-0-p1-0')
        porch(d, (5, 20, 11, 21), 1, 3, 'north')
        for z in (5, 10, 15): d.lantern((8, 3, z)); d.lantern((8, 7, z))
        d.lantern((8, 3, 21), 'south'); d.lantern((16, 3, 10))
        d.fence((1, 0, 23, 24), 0, 'drystonefence-shale', gaps=[(x, 24) for x in range(6, 11)])
        d.ground((2, 20, 22, 23), -1, COBBLE)
        d.path([(x, z) for x in range(6, 11) for z in (22, 23, 24)], -1)
        grass(d, (0, 0, 24, 25), -1, every=13)
    return d


# ---------------------------------------------------------------- Royal Mint
@recipe('mint')
def mint(d):
    L = d.level
    if L == 1:
        # A coiner's bench: a lean-to over an anvil and a strongbox, a firepit for the melt, a bed in the back.
        box = (1, 1, 6, 5)
        yard(d, (0, 0, 9, 8))
        lean_to(d, box, 1, 3, 'north', open_sides=('south',), level=1, floor=DIRT)
        hut_block(d, (5, 1, 4))
        d.place('game:anvil-copper', (3, 1, 2)); d.put_against('game:chest', [(2, 1, 2)])
        d.bed((5, 1, 2), 'hay', 'east') or d.bed((4, 1, 2), 'hay')
        d.place('game:firepit-cold', (3, 0, 7))
        d.torch((3, 2, 1), 'south')
        grass(d, (0, 0, 9, 8), -1, every=7)
    elif L == 2:
        # A mint house: a small daub house with a barred strongroom at the back (cobble walls, no window),
        # the striking bench with anvil and forge-fire at the front, the minter's bed.
        box = (1, 1, 7, 11)
        yard(d, (0, 0, 10, 14))
        house(d, box, storeys=1, height=3, axis=1, level=2, no_windows=('north',))
        d.door((4, 1, 11), 'crude', 'north')
        hut_block(d, (6, 1, 10))
        d.plain_walls((1, 1, 7, 4), 1, 2, COBBLE); d.hollow((1, 1, 7, 4), 1, 2); d.door((4, 1, 4), 'crude', 'north')
        d.put_against('game:chest', [(2, 1, 2), (6, 1, 2)]); d.put('game:crate', [(4, 1, 2)])
        d.place('game:anvil-copper', (3, 1, 7)); d.place('game:firepit-cold', (5, 1, 7))
        d.bed((2, 1, 9), 'rough', 'south') or d.bed((2, 1, 8), 'rough')
        d.torch((4, 2, 6)); d.torch((4, 2, 12), 'south')
        d.path([(4, 12), (4, 13)], -1)
        grass(d, (0, 0, 10, 14), -1, every=9)
    elif L == 3:
        # The mint: a rammed-earth workshop with a stone strongroom under a stone-slab roof, a forge, two
        # anvils, the weigher's table, a guard bench by the door, a cobble-walled yard.
        shop = (2, 1, 10, 9); strong = (10, 3, 15, 9)
        yard(d, (0, 0, 18, 13))
        house(d, shop, storeys=1, height=3, axis=0, level=3, no_windows=('east',))
        d.plain_walls(strong, 1, 3, COBBLE); d.hollow(strong, 1, 3); d.floor(strong, 4, COBBLE); d.floor_inside(strong, 0, COBBLE)
        d.rooms.append((strong, 3))
        d.door((10, 1, 6), 'solid-oak', 'east')
        d.door((6, 1, 9), 'solid-oak', 'north')
        hut_block(d, (9, 1, 8))
        d.place('game:forge', (3, 1, 3)); d.place('game:anvil-copper', (5, 1, 4)); d.place('game:anvil-copper', (7, 1, 4))
        d.put('w4rd0sfurniture:roughtable-north', [(4, 1, 7)]); d.put('w4rd0sfurniture:roughstool-west', [(3, 1, 7)])
        d.put_against('game:chest', [(11, 1, 4), (11, 1, 8), (14, 1, 4), (14, 1, 8)]); d.put('game:crate', [(12, 1, 6), (13, 1, 6)])
        bench(d, [(8, 1, 8)])
        for y in (1, 2): d.set((2, y, 3), COBBLE)
        chimney(d, 2, 3, 3, 7, COBBLE)
        d.lantern((6, 3, 6)); d.lantern((12, 3, 6)); d.lantern((6, 2, 10), 'south')
        d.fence((1, 0, 17, 12), 0, 'drystonefence-shale', gaps=[(6, 12), (7, 12)])
        d.path([(6, 10), (6, 11), (6, 12), (7, 10), (7, 11), (7, 12)], -1)
        grass(d, (0, 0, 18, 13), -1, every=11)
    elif L == 4:
        # A royal mint: cobble workshop below (forge, three anvils, weighing table), the master's chambers
        # above, a square stone strong tower with the vault at its foot and the counting room over it.
        shop = (2, 1, 12, 11); tow = (12, 5, 18, 11)
        yard(d, (0, 0, 21, 16))
        house(d, shop, storeys=2, height=3, axis=0, level=4, no_windows=('east',))
        tower(d, tow, 1, 11, COBBLE, roof='hip', floors_every=4, ladder=(17, 'north', 10))
        d.door((12, 1, 8), 'solid-oak', 'east'); opening(d, [(12, 8)], 5, 6)
        d.door((7, 1, 11), 'solid-oak', 'north')
        hut_block(d, (11, 1, 10))
        d.place('game:forge', (3, 1, 3)); d.place('game:anvil-copper', (5, 1, 4)); d.place('game:anvil-copper', (7, 1, 4)); d.place('game:anvil-copper', (9, 1, 4))
        d.put('w4rd0sfurniture:roughtabledouble-south', [(5, 1, 8)]) or d.put('w4rd0sfurniture:roughtable-north', [(5, 1, 8)])
        d.put('game:barrel', [(10, 1, 7)]); bench(d, [(9, 1, 9)])
        for y in (1, 2, 3): d.set((2, y, 3), BRICK)
        chimney(d, 2, 3, 4, 9, COBBLE)
        d.stair_run((3, 1, 9), 'east', 3, 'planks')
        for x in (3, 4, 5, 6): d.set((x, 4, 9), None)
        d.bed((9, 5, 3), 'wood', 'south'); d.put('w4rd0sfurniture:rusticcabinetlarge-oak-east', [(3, 5, 3)]); d.put('game:bookshelf', [(5, 5, 3)])
        d.put('w4rd0sfurniture:roughtable-north', [(9, 5, 8)]); d.put('w4rd0sfurniture:roughchair-north', [(9, 5, 9)])
        d.put_against('game:chest', [(13, 1, 6), (13, 1, 10), (16, 1, 6), (16, 1, 10)]); d.put('game:crate', [(15, 1, 8)])
        d.put('w4rd0sfurniture:roughtable-north', [(15, 5, 8)]); d.put_against('game:chest', [(13, 5, 10)])
        d.lantern((7, 3, 7)); d.lantern((7, 7, 7)); d.lantern((15, 3, 8)); d.lantern((15, 7, 8)); d.lantern((7, 2, 12), 'south')
        banner(d, (5, 3, 12), 'south'); banner(d, (9, 3, 12), 'south')
        d.fence((1, 0, 20, 15), 0, 'drystonefence-shale', gaps=[(7, 15), (8, 15)])
        d.path([(x, z) for x in (7, 8) for z in (12, 13, 14, 15)], -1)
        grass(d, (0, 0, 21, 16), -1, every=11)
    else:
        # The royal mint: a shale-brick hall with the striking floor (forge, four anvils, polished floor), a
        # treasury tower with a barred top, the master's lodging in a mud-brick upper storey, a guarded gate
        # in a drystone wall, flags and banners.
        hall = (2, 1, 14, 13); tow = (14, 7, 20, 13)
        yard(d, (0, 0, 24, 19))
        house(d, hall, storeys=2, height=3, axis=0, level=5, no_windows=('east',))
        tower(d, tow, 1, 14, BRICK, roof='hip', floors_every=4, ladder=(19, 'north', 12))
        for y in range(11, 13):
            for (x, z) in ring(tow):
                if (x + z) % 2 == 0 and d.get((x, y, z)) == BRICK: d.set((x, y, z), None)
        d.door((14, 1, 10), 'solid-oak', 'east'); opening(d, [(14, 10)], 5, 6)
        d.door((8, 1, 13), 'solid-oak', 'north')
        hut_block(d, (13, 1, 12))
        d.floor_inside(hall, 0, POLISHED)
        d.place('game:forge', (3, 1, 3)); d.place('game:forge', (3, 1, 6))
        for x in (5, 7, 9, 11): d.place('game:anvil-copper', (x, 1, 4))
        d.put('w4rd0sfurniture:rusticsturdytabledouble-east', [(6, 1, 9)]) or d.put('w4rd0sfurniture:roughtable-north', [(6, 1, 9)])
        d.put('w4rd0sfurniture:rusticchairdining-east', [(5, 1, 9)]); d.put('game:barrel', [(12, 1, 7), (12, 1, 9)]); bench(d, [(11, 1, 11)])
        for y in (1, 2, 3): d.set((2, y, 4), POLISHED)
        chimney(d, 2, 4, 4, 10, BRICK)
        d.stair_run((3, 1, 11), 'east', 3, 'brick')
        for x in (3, 4, 5, 6): d.set((x, 4, 11), None)
        d.bed((11, 5, 3), 'rustic', 'east'); d.put('w4rd0sfurniture:rusticwardrobe-east', [(3, 5, 3)]); d.put('game:bookshelf', [(5, 5, 3), (6, 5, 3)])
        d.put('w4rd0sfurniture:rusticsturdytabledouble-south', [(8, 5, 8)]); d.put('w4rd0sfurniture:rusticchair-west', [(10, 5, 8)])
        d.put_against('game:chest', [(15, 1, 8), (15, 1, 12), (18, 1, 8), (18, 1, 12)]); d.put('game:crate', [(16, 1, 10), (17, 1, 10)])
        d.put_against('game:chest', [(15, 5, 8), (18, 5, 8)]); d.put('w4rd0sfurniture:roughtable-north', [(17, 5, 11)])
        d.put_against('game:chest', [(15, 9, 8), (18, 9, 8), (15, 9, 12), (18, 9, 12)])
        for p in ((8, 3, 7), (8, 7, 7), (17, 3, 10), (17, 7, 10), (17, 11, 10)): d.lantern(p)
        d.lantern((8, 2, 14), 'south'); d.lantern((14, 2, 9), 'east')
        flag(d, (3, 0, 15)); flag(d, (13, 0, 15)); banner(d, (6, 3, 14), 'south'); banner(d, (10, 3, 14), 'south')
        d.fence((1, 0, 23, 18), 0, 'drystonefence-shale', gaps=[(8, 18), (9, 18)])
        # gate posts in stone
        for y in (0, 1, 2): d.set((7, y, 18), BRICK); d.set((10, y, 18), BRICK)
        d.path([(x, z) for x in (8, 9) for z in range(14, 19)], -1)
        grass(d, (0, 0, 24, 19), -1, every=13)
    return d
