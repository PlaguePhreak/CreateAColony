"""The two sample types for Aaron's review: Residence (small) and Guard Tower, levels 1 to 5."""
import sys
sys.path.insert(0, '.')
from build2 import *
from design import Design

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
        # A settler's hut: 5x7 of packed dirt between oak posts, wattle in the windows, a hay lintel over the
        # crude door, a dry-grass roof. Hay bed on the west wall, a fire in the middle, a basket and a chest.
        box = (2, 2, 6, 8)
        yard(d, (0, 0, 12, 12))
        w = Walls(d, box); w.door('south'); w.window('east', 3); w.window('west', 3); w.window('north', 2)
        w.build(1, 3)
        gable_over(d, box, w.top, 1, BY_LEVEL[1])
        d.bed((3, 1, 3), 'hay', 'south') or d.bed((3, 1, 4), 'hay')
        d.place('game:firepit-cold', (4, 1, 5)); d.set((4, 0, 5), COBBLE)
        d.put('game:stationarybasket-north', [(5, 1, 3)]); d.put_against('game:chest', [(5, 1, 7)])
        hut_block(d, (3, 1, 7))
        d.torch((4, 2, 9), 'south'); d.torch((4, 3, 5))
        woodpile(d, [(9, 3), (10, 3), (9, 4), (10, 4)], 0, 1, 'ns'); d.set((9, 0, 7), LOG)
        d.place('game:firepit-cold', (9, 0, 9))
        d.fence((1, 1, 11, 11), 0, 'wattle-sticks', gaps=[(4, 11)])
        d.set((4, -1, 11), PATH); d.set((4, -1, 12), PATH)
        grass(d, (0, 0, 12, 12), -1, every=6)
    elif L == 2:
        # A cottage: 7x9, cobble footing, daub panels in an oak frame with mudbrick window heads and cobble
        # sills, a cobble hearth and chimney on the west wall, a thatch roof. Two beds, a table, a chest,
        # a wattle yard with a gate, a garden and a woodpile.
        box = (2, 2, 8, 10)
        yard(d, (0, 0, 17, 14))
        w = Walls(d, box); w.door('south'); w.windows_along('south', 3); w.windows_along('north', 3); w.window('east', 4); w.window('west', 2); w.window('west', 6)
        w.build(1, 3)
        gable_over(d, box, w.top, 1, BY_LEVEL[2])
        hearth(d, 3, 6, 1, 'east'); chimney(d, 2, 6, 4, 8, COBBLE)
        d.beds(2, [(7, 1, 3), (7, 1, 8)], 'rough', 'west') or d.beds(2, [(7, 1, 3), (7, 1, 8)], 'rough')
        d.put('w4rd0sfurniture:roughtable-north', [(5, 1, 3)]); d.put('w4rd0sfurniture:roughstool-west', [(4, 1, 3)])
        d.put_against('game:chest', [(3, 1, 9)]); d.put('game:stationarybasket-north', [(4, 1, 9)]); d.put('w4rd0sfurniture:roughstandingshelf-north', [(3, 1, 3)]) or None
        hut_block(d, (6, 1, 9))
        d.torch((5, 2, 11), 'south'); d.torch((5, 3, 6))
        d.fence((1, 1, 16, 13), 0, 'wattle-sticks', gaps=[(5, 13)])
        d.path([(5, 11), (5, 12), (5, 13), (5, 14)], -1)
        woodpile(d, [(11, 2), (12, 2), (13, 2), (11, 3), (12, 3), (13, 3)], 0, 2, 'ns')
        garden(d, (11, 6, 15, 11), -1)
        d.put('game:barrel', [(9, 0, 11)])
        grass(d, (0, 0, 17, 14), -1, every=7)
    elif L == 3:
        # A house: 7x11 of rammed earth on two cobble courses, debarked lintels, plank floor, shuttered
        # windows, cobble corbels under a plank roof; an east annex for the beds with its own low roof;
        # a hearth and chimney on the north wall; oak fence, path, well, garden, woodpile, lanterns.
        box = (2, 2, 8, 12); annex = (8, 5, 12, 9)
        yard(d, (0, 0, 20, 16))
        w = Walls(d, box); w.door('south'); w.windows_along('south', 3); w.windows_along('west', 4); w.window('north', 2); w.window('north', 4)
        w.build(1, 3); w.corbels(('east', 'west'))
        gable_over(d, box, w.top, 1, BY_LEVEL[3])
        a = Walls(d, annex); a.window('east', 2); a.window('south', 2); a.window('north', 2)
        a.build(1, 3)
        gable_over(d, annex, a.top, 0, BY_LEVEL[3], abut='west')
        opening(d, [(8, 7)], 1, 2)
        hearth(d, 5, 3, 1, 'south'); chimney(d, 5, 2, 4, 9, COBBLE)
        d.beds(3, [(9, 1, 6), (11, 1, 6), (11, 1, 8)], 'rough', 'south') or d.beds(3, [(9, 1, 6), (11, 1, 6), (11, 1, 8)], 'rough')
        table_and_chairs(d, [(5, 1, 7)], 'rough'); d.put_against('game:chest', [(3, 1, 11)]); d.put('w4rd0sfurniture:roughstandingshelf-north', [(3, 1, 5)]) or None; d.put('game:stationarybasket-north', [(7, 1, 11)])
        hut_block(d, (7, 1, 3))
        d.lantern((5, 2, 13), 'south'); d.lantern((5, 3, 8)); d.lantern((10, 3, 7))
        d.fence((1, 1, 19, 15), 0, 'woodenfence-oak', gaps=[(5, 15)])
        d.path([(5, z) for z in range(13, 17)], -1)
        well(d, 16, 4); garden(d, (14, 8, 18, 13), -1); woodpile(d, [(x, 2) for x in range(11, 16)], 0, 2, 'ns')
        lantern_post(d, 9, 13)
        grass(d, (0, 0, 20, 16), -1, every=8)
    elif L == 4:
        # A two-storey house: 7x13, shale cobble below and oak planks above, the upper floor jettied out on
        # plank stairs over the yard side, debarked posts, a clay-brick chimney, a clay shingle roof; a one
        # storey annex for the kitchen; four beds and a wardrobe upstairs; a cobbled court behind an oak fence
        # on a cobble base, lantern posts, a well and a garden.
        box = (2, 2, 8, 14); annex = (8, 4, 12, 8)
        yard(d, (0, 0, 22, 18))
        w = Walls(d, box); w.door('south'); w.windows_along('south', 3); w.windows_along('west', 4); w.windows_along('north', 3)
        w.windows_along('south', 3, storey=1); w.windows_along('west', 4, storey=1); w.windows_along('east', 4, storey=1); w.windows_along('north', 3, storey=1)
        w.build(2, 3); w.jetty(('south', 'west')); w.corbels(('east',))
        gable_over(d, box, w.top, 1, BY_LEVEL[4])
        a = Walls(d, annex); a.window('east', 2); a.window('north', 2); a.door('south')
        a.build(1, 3)
        gable_over(d, annex, a.top, 0, BY_LEVEL[4], abut='west')
        opening(d, [(8, 6)], 1, 2)
        hearth(d, 5, 3, 1, 'south', BRICK); chimney(d, 5, 2, 4, 12, BRICK)
        d.place('game:clayoven-east', (9, 1, 5)); d.put('game:barrel', [(11, 1, 5)]); d.put('foodshelves:doubleshelf-normal-east', [(11, 1, 7)]) or d.put_against('game:chest', [(11, 1, 7)])
        table_and_chairs(d, [(5, 1, 8)], 'rough', double=True); d.put_against('game:chest', [(3, 1, 13)]); d.put('game:bookshelf', [(7, 1, 13)])
        d.stair_run((7, 1, 11), 'north', 3, 'planks')
        for z in (9, 10): d.set((7, 4, z), None)
        d.beds(4, [(3, 5, 3), (3, 5, 7), (3, 5, 11), (7, 5, 3)], 'wood', 'east') or d.beds(4, [(3, 5, 3), (3, 5, 7), (3, 5, 11), (7, 5, 3)], 'wood')
        d.put('w4rd0sfurniture:rusticwardrobe-east', [(7, 5, 6)]) or d.put_against('game:chest', [(7, 5, 6)]); d.put_against('game:chest', [(5, 5, 13)])
        hut_block(d, (5, 1, 4))
        d.lantern((5, 2, 15), 'south'); d.lantern((5, 3, 8)); d.lantern((5, 7, 8)); d.lantern((10, 3, 6))
        d.fence((1, 1, 21, 17), 0, 'woodenfence-oak', gaps=[(5, 17)])
        for (x, z) in ring((1, 1, 21, 17)): d.set((x, -1, z), COBBLE)
        stone_court(d, (2, 15, 14, 16)); d.path([(5, 17), (5, 18)], -1)
        well(d, 17, 4); garden(d, (15, 8, 20, 14), -1); woodpile(d, [(x, 2) for x in range(13, 19)], 0, 2, 'ns')
        lantern_post(d, 10, 15); lantern_post(d, 17, 16)
        grass(d, (0, 0, 22, 18), -1, every=9)
    else:
        # A stone house: 7x15, two storeys, shale stone brick on a polished plinth with polished quoins,
        # mud brick (plaster, once the code is confirmed) above on debarked posts, oak support beams under
        # the eave, stone corbels, two clay-brick chimneys, a clay shingle roof; a north wing of one storey
        # with the same roof; six beds; a drystone-walled court of cobble and polished flags, a well, a
        # garden, banners by the door and a flag.
        box = (2, 2, 8, 16); wing = (8, 4, 14, 10)
        yard(d, (0, 0, 24, 20))
        w = Walls(d, box); w.door('south'); w.windows_along('south', 3); w.windows_along('west', 4); w.windows_along('north', 3)
        w.windows_along('south', 3, storey=1); w.windows_along('west', 4, storey=1); w.windows_along('east', 4, storey=1); w.windows_along('north', 3, storey=1)
        w.build(2, 3); w.corbels(('east', 'west')); w.braces(('west',), storey=1)
        for (x, z) in ((2, 2), (8, 2), (2, 16), (8, 16)):
            for y in (1, 2, 3): d.set((x, y, z), POLISHED if y % 2 else BRICK)
        gable_over(d, box, w.top, 1, BY_LEVEL[5])
        g = Walls(d, wing); g.window('east', 2); g.window('east', 4); g.window('north', 2); g.window('north', 4); g.window('south', 2); g.window('south', 4)
        g.build(1, 4); g.corbels(('north', 'south'))
        gable_over(d, wing, g.top, 0, BY_LEVEL[5], abut='west')
        opening(d, [(8, 7)], 1, 2)
        hearth(d, 5, 3, 1, 'south', BRICK); chimney(d, 5, 2, 4, 12, BRICK); chimney(d, 14, 6, 5, 10, BRICK)
        for y in (1, 2, 3): d.set((14, y, 6), BRICK)
        d.place('game:firepit-cold', (13, 1, 6)); d.set((13, 0, 6), BRICK)
        table_and_chairs(d, [(5, 1, 9)], 'rustic', double=True); d.put('w4rd0sfurniture:rusticcabinetlarge-oak-east', [(3, 1, 15)]); d.put('game:bookshelf', [(7, 1, 15)]); d.put('w4rd0sfurniture:rusticlargeshelf-oak-south', [(3, 1, 5)]) or None
        d.beds(2, [(10, 1, 9), (12, 1, 9)], 'rustic', 'south') or d.beds(2, [(10, 1, 9), (12, 1, 9)], 'rustic'); d.put('w4rd0sfurniture:rusticwardrobe-east', [(13, 1, 5)]) or None
        d.stair_run((7, 1, 13), 'north', 3, 'brick')
        for z in (11, 12): d.set((7, 4, z), None)
        d.beds(4, [(3, 5, 3), (3, 5, 7), (3, 5, 11), (7, 5, 3)], 'rustic', 'east') or d.beds(4, [(3, 5, 3), (3, 5, 7), (3, 5, 11), (7, 5, 3)], 'rustic')
        d.put('w4rd0sfurniture:rusticwardrobe-east', [(7, 5, 7)]) or None; d.put('w4rd0sfurniture:rusticnightstand-west', [(5, 5, 15)]) or None; d.put_against('game:chest', [(3, 5, 15)])
        hut_block(d, (5, 1, 4))
        d.lantern((5, 2, 17), 'south'); banner(d, (3, 3, 17), 'south'); banner(d, (7, 3, 17), 'south'); d.lantern((5, 3, 8)); d.lantern((5, 7, 8)); d.lantern((11, 4, 7))
        d.fence((1, 1, 23, 19), 0, 'drystonefence-shale', gaps=[(5, 19)])
        d.set((4, 0, 19), POLISHED); d.set((6, 0, 19), POLISHED); d.set((4, 1, 19), POLISHED); d.set((6, 1, 19), POLISHED)
        stone_court(d, (2, 17, 16, 18)); d.path([(5, 19), (5, 20)], -1)
        well(d, 19, 4); garden(d, (17, 8, 22, 15), -1); woodpile(d, [(x, 2) for x in range(16, 22)], 0, 2, 'ns')
        lantern_post(d, 10, 17); lantern_post(d, 18, 18); flag(d, (21, 0, 18))
        grass(d, (0, 0, 24, 20), -1, every=10)
    return d


# ---------------------------------------------------------------- Guard Tower
@recipe('guardtower')
def guardtower(d):
    L = d.level
    if L == 1:
        # A watch platform: four oak posts, a log deck with a wattle parapet, a stick ladder, a dry-grass hat
        # on the posts; the guard's hay bed and chest in a wattle screen between the legs, a fire, a yard fence.
        box = (2, 2, 6, 6)
        yard(d, (0, 0, 10, 10))
        for (x, z) in ((2, 2), (6, 2), (2, 6), (6, 6)):
            for y in range(0, 9): d.set((x, y, z), LOG)
        d.floor(box, 5, LOG_X); d.notes.append(('floors', box, [5]))
        for (x, z) in ring(box):
            if (x, z) not in ((2, 2), (6, 2), (2, 6), (6, 6)): d.set((x, 6, z), fence_code('wattle-sticks', 'ew' if z in (2, 6) else 'ns'))
        for (x, z) in ring(box):
            if (x, z) not in ((2, 2), (6, 2), (2, 6), (6, 6)): d.set((x, 8, z), LOG_X if z in (2, 6) else LOG_Z)
        hat_over(d, box, 8, BY_LEVEL[1])
        d.ladder(4, 2, 0, 6, 'north'); d.set((4, 5, 2), 'game:ladder-stick-north'); d.set((4, 6, 2), None)
        d.torch((6, 7, 6)); d.torch((2, 7, 2))
        d.floor(box, 0, DIRT)
        for z in (3, 4, 5): d.set((2, 1, z), fence_code('wattle-sticks', 'ns')); d.set((2, 2, z), fence_code('wattle-sticks', 'ns'))
        for x in (3, 4, 5): d.set((x, 1, 6), fence_code('wattle-sticks', 'ew')); d.set((x, 2, 6), fence_code('wattle-sticks', 'ew'))
        hut_block(d, (5, 1, 3))
        d.bed((3, 1, 4), 'hay', 'north') or d.bed((3, 1, 3), 'hay')
        d.put_against('game:chest', [(5, 1, 5)])
        d.place('game:firepit-cold', (8, 0, 4))
        d.fence((1, 1, 9, 9), 0, 'wattle-sticks', gaps=[(4, 9)])
        grass(d, (0, 0, 10, 10), -1, every=6)
    elif L == 2:
        # A daub watchtower: 5x5, two storeys of daub in oak frames on a cobble footing, slit windows, a
        # ladder to the open deck under a thatch hat; a guard room annex with two beds and a chest.
        box = (3, 2, 7, 6); annex = (3, 6, 7, 10)
        yard(d, (0, 0, 12, 13))
        w = Walls(d, box); w.window('north', 2); w.window('east', 2); w.window('west', 2); w.window('north', 2, storey=1); w.window('east', 2, storey=1); w.window('west', 2, storey=1)
        w.build(2, 3)
        hat_over(d, box, w.top, BY_LEVEL[2])
        ladder_up(d, 4, 3, 1, 6, 'north', floors=(4,))
        a = Walls(d, annex); a.door('south'); a.window('east', 2); a.window('west', 2)
        a.build(1, 3)
        gable_over(d, annex, a.top, 1, BY_LEVEL[2], abut='north')
        opening(d, [(5, 6)], 1, 2)
        d.beds(2, [(4, 1, 8), (6, 1, 8)], 'rough', 'south') or d.beds(2, [(4, 1, 8), (6, 1, 8)], 'rough')
        d.put_against('game:chest', [(4, 1, 4)]); d.place('game:toolrack-west', (6, 2, 4)); d.put('game:barrel', [(6, 1, 9)])
        hut_block(d, (6, 1, 4))
        d.torch((5, 2, 11), 'south'); d.torch((5, 3, 4)); d.torch((5, 6, 4)); d.torch((5, 2, 8))
        d.fence((1, 1, 11, 12), 0, 'wattle-sticks', gaps=[(5, 12)])
        d.path([(5, 12), (5, 13)], -1)
        d.place('game:firepit-cold', (9, 0, 4)); woodpile(d, [(9, 8), (10, 8)], 0, 1, 'ns')
        grass(d, (0, 0, 12, 13), -1, every=7)
    elif L == 3:
        # A rammed-earth keep: 7x7, three storeys on two cobble courses with cobble corbels, arrow slits,
        # a plank hat; a guard hall of 5x7 against the east side with three beds, a hearth and a chimney;
        # an oak fence, a path, lantern posts and a well.
        box = (2, 2, 8, 8); hall = (8, 3, 12, 9)
        yard(d, (0, 0, 18, 14))
        w = Walls(d, box)
        for st in (0, 1, 2):
            w.window('north', 3, storey=st); w.window('west', 3, storey=st); w.window('south', 3, storey=st)
        w.build(3, 3); w.corbels(('north', 'south', 'west'))
        hat_over(d, box, w.top, BY_LEVEL[3])
        ladder_up(d, 7, 3, 1, 9, 'north', floors=(4, 8))
        h = Walls(d, hall); h.door('south'); h.window('east', 2); h.window('east', 4); h.window('north', 2)
        h.build(1, 3)
        gable_over(d, hall, h.top, 1, BY_LEVEL[3], abut='west')
        opening(d, [(8, 6)], 1, 2)
        hearth(d, 10, 4, 1, 'south'); chimney(d, 10, 3, 4, 8, COBBLE)
        d.beds(3, [(9, 1, 8), (11, 1, 8), (11, 1, 6)], 'rough', 'south') or d.beds(3, [(9, 1, 8), (11, 1, 8), (11, 1, 6)], 'rough')
        d.put_against('game:chest', [(3, 1, 7), (3, 1, 3)]); d.place('game:toolrack-west', (7, 2, 7)); d.put('game:crate', [(3, 5, 3)]); d.put_against('game:chest', [(3, 9, 3)])
        hut_block(d, (5, 1, 5))
        d.lantern((5, 3, 5)); d.lantern((5, 7, 5)); d.lantern((5, 11, 5)); d.lantern((10, 3, 6)); d.lantern((10, 2, 10), 'south')
        d.fence((1, 1, 17, 13), 0, 'woodenfence-oak', gaps=[(10, 13)])
        d.path([(10, 13), (10, 14)], -1)
        well(d, 15, 4); lantern_post(d, 14, 11); woodpile(d, [(x, 11) for x in range(3, 7)], 0, 2, 'we')
        grass(d, (0, 0, 18, 14), -1, every=8)
    elif L == 4:
        # A cobble tower with a hoarding: 7x7 cobble for two storeys, the third storey of planks jettied out
        # on all sides to 9x9 under a clay shingle hat, arrow slits below and windows above; a barrack hall
        # of 7x11 with five beds, a hearth and a brick chimney; a cobbled court, a flag, an oak fence on a
        # cobble base.
        box = (3, 3, 9, 9); hall = (9, 2, 19, 8)
        yard(d, (0, 0, 24, 16))
        w = Walls(d, box)
        for st in (0, 1):
            w.window('north', 3, storey=st); w.window('west', 3, storey=st); w.window('south', 3, storey=st)
        w.windows_along('north', 2, storey=2); w.windows_along('west', 2, storey=2); w.windows_along('south', 2, storey=2); w.windows_along('east', 2, storey=2)
        w.build(3, 3, up_from=2); w.jetty(('north', 'south', 'east', 'west'), storey=2)
        for (x, z) in ((2, 2), (10, 2), (2, 10), (10, 10)):
            for y in range(w.storey_y[2] - 1, w.top + 1): d.set((x, y, z), DEBARKED)
        hat_over(d, (2, 2, 10, 10), w.top, BY_LEVEL[4])
        ladder_up(d, 8, 4, 1, 9, 'north', floors=(4, 8))
        h = Walls(d, hall); h.door('south'); h.windows_along('south', 3); h.windows_along('north', 3); h.window('east', 3)
        h.build(1, 3); h.corbels(('north', 'south'))
        gable_over(d, hall, h.top, 0, BY_LEVEL[4], abut='west')
        opening(d, [(9, 5)], 1, 2)
        hearth(d, 17, 3, 1, 'south', BRICK); chimney(d, 17, 2, 4, 9, BRICK)
        d.beds(5, [(11, 1, 3), (14, 1, 3), (11, 1, 7), (14, 1, 7), (18, 1, 6)], 'wood', 'south') or d.beds(5, [(11, 1, 3), (14, 1, 3), (11, 1, 7), (14, 1, 7), (18, 1, 6)], 'wood')
        table_and_chairs(d, [(16, 1, 5)], 'rough'); d.put_against('game:chest', [(4, 1, 8), (4, 1, 4)]); d.place('game:toolrack-west', (8, 2, 8)); d.put('game:crate', [(4, 5, 4), (4, 9, 4)])
        hut_block(d, (6, 1, 6))
        d.lantern((6, 3, 6)); d.lantern((6, 7, 6)); d.lantern((6, 11, 6)); d.lantern((14, 3, 5)); d.lantern((14, 2, 9), 'south'); banner(d, (12, 3, 9), 'south')
        d.fence((1, 1, 23, 15), 0, 'woodenfence-oak', gaps=[(14, 15)])
        for (x, z) in ring((1, 1, 23, 15)): d.set((x, -1, z), COBBLE)
        stone_court(d, (2, 11, 22, 13)); d.path([(14, 15), (14, 16)], -1)
        flag(d, (21, 0, 10)); lantern_post(d, 11, 14); lantern_post(d, 18, 14)
        grass(d, (0, 0, 24, 16), -1, every=9)
    else:
        # A stone keep: 9x9 shale stone brick on a polished plinth with polished quoins, three storeys, slit
        # windows, a corbel course under a clay shingle hat; a barrack range of 7x13 with eight beds, a
        # great hearth and a brick chimney; a gate tower of 5x5 with its own hat in a drystone bailey wall;
        # a flagged court, banners and a flag.
        box = (3, 3, 11, 11); hall = (11, 2, 23, 8); gate = (13, 14, 17, 18)
        yard(d, (0, 0, 28, 22))
        w = Walls(d, box)
        for st in (0, 1, 2):
            w.window('north', 4, storey=st); w.window('west', 4, storey=st); w.window('south', 4, storey=st)
            if st > 0: w.window('north', 2, storey=st); w.window('north', 6, storey=st); w.window('west', 2, storey=st); w.window('west', 6, storey=st)
        w.build(3, 3); w.corbels(('north', 'south', 'west', 'east'))
        for (x, z) in ((3, 3), (11, 3), (3, 11), (11, 11)):
            for y in range(1, w.top): d.set((x, y, z), POLISHED if y % 2 else BRICK)
        hat_over(d, box, w.top, BY_LEVEL[5])
        ladder_up(d, 10, 4, 1, 9, 'north', floors=(4, 8))
        d.place('game:firepit-cold', (7, 1, 7)); d.set((7, 0, 7), BRICK)
        h = Walls(d, hall); h.door('south'); h.windows_along('south', 3); h.windows_along('north', 3); h.window('east', 3)
        h.build(1, 4); h.corbels(('north', 'south'))
        gable_over(d, hall, h.top, 0, BY_LEVEL[5], abut='west')
        opening(d, [(11, 5)], 1, 2)
        hearth(d, 21, 3, 1, 'south', BRICK); chimney(d, 21, 2, 5, 10, BRICK)
        d.beds(8, [(13, 1, 3), (16, 1, 3), (19, 1, 3), (13, 1, 7), (16, 1, 7), (19, 1, 7), (22, 1, 6), (22, 1, 4)], 'rustic', 'south') or d.beds(8, [(13, 1, 3), (16, 1, 3), (19, 1, 3), (13, 1, 7), (16, 1, 7), (19, 1, 7), (22, 1, 6), (22, 1, 4)], 'rustic')
        table_and_chairs(d, [(17, 1, 5)], 'rustic', double=True); d.put('w4rd0sfurniture:rusticwardrobe-east', [(12, 1, 5)]) or None
        d.put_against('game:chest', [(4, 1, 10), (4, 1, 4), (10, 1, 10)]); d.place('game:toolrack-west', (10, 2, 6)); d.place('game:toolrack-west', (10, 2, 7)); d.put('game:crate', [(4, 5, 4), (4, 9, 4)]); d.put('game:bookshelf', [(10, 5, 10)])
        hut_block(d, (8, 1, 9))
        for y in (3, 7, 11): d.lantern((7, y, 7))
        d.lantern((17, 3, 5)); d.lantern((17, 2, 9), 'south'); banner(d, (15, 3, 9), 'south'); banner(d, (19, 3, 9), 'south')
        # the bailey: a drystone wall round the court with a gate tower on the south side
        for (x, z) in ring((1, 1, 27, 21)):
            if 13 <= x <= 17 and z == 21: continue
            if d.free((x, 0, z)): d.set((x, 0, z), POLISHED); d.set((x, 1, z), BRICK)
        g = Walls(d, gate); g.build(2, 3)
        for y in (1, 2, 3): d.set((15, y, 14), None); d.set((15, y, 18), None)
        for y in (2, 3): d.set((15, y, 14), None)
        d.set((15, 3, 18), LOG_X); d.set((15, 3, 14), LOG_X)
        d.gate((15, 1, 18), 'north')
        hat_over(d, gate, g.top, BY_LEVEL[5])
        ladder_up(d, 16, 15, 1, 6, 'north', floors=(4,))
        stone_court(d, (2, 10, 26, 13)); stone_court(d, (12, 19, 18, 20)); d.path([(15, 21), (15, 22)], -1)
        flag(d, (25, 0, 15)); flag(d, (4, 0, 15)); lantern_post(d, 11, 15); lantern_post(d, 19, 15)
        lantern_post(d, 26, 20); lantern_post(d, 2, 20)
        grass(d, (0, 0, 28, 22), -1, every=10)
    return d


if __name__ == '__main__':
    import preview, os
    which = sys.argv[1:] or None
    for (type, variant), fn in RECIPES.items():
        if which and type not in which: continue
        builds = []
        for level in range(1, 6):
            d = fn(level)
            probs = d.check(type) + check_floors(d) + check_floating(d)
            x0, y0, z0, x1, y1, z1 = d.bounds()
            print(f'{type} {variant} {level}: {x1-x0+1}x{y1-y0+1}x{z1-z0+1} {len(d.blocks)} blocks', ('PROBLEMS ' + str(probs)) if probs else 'ok')
            builds.append(d)
        preview.sheet(builds, [f'{type} {variant} level {l}' for l in range(1, 6)], f'out_samples/{type}{"_" + variant if variant else ""}.png', both=True)
        for d in builds: d.save_to('out_samples/blueprints')
