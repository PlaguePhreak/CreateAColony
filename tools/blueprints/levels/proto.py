"""A first prototype on the new walls: Residence small, level 2, with his shallow builder roof as a stand-in."""
import sys
sys.path.insert(0, '.')
from design import Design
from walls import *
from parts import yard, grass, woodpile, garden
import run

def residence2():
    d = Design('residence', 'small', 2)
    box = (2, 2, 8, 10)                         # 7 x 9 walls
    yard(d, (0, 0, 16, 13))
    w = Walls(d, box)
    w.door('south'); w.windows_along('south', 3); w.windows_along('north', 3); w.window('east', 4); w.window('west', 4)
    w.build(storeys=1, height=3)
    # hearth on the west wall with a cobble chimney through the eave
    hearth(d, 3, 6, 1, 'east')
    chimney(d, 2, 6, 4, 8, COBBLE)
    d.beds(2, [(7, 1, 3), (7, 1, 8)], 'rough', 'west')
    d.put('w4rd0sfurniture:roughtable-north', [(5, 1, 3)]); d.put('w4rd0sfurniture:roughstool-west', [(4, 1, 3)])
    d.put_against('game:chest', [(3, 1, 9)]); d.put('game:stationarybasket-north', [(4, 1, 9)])
    d.torch((5, 2, 11), 'south'); d.torch((5, 2, 6))
    d.set_hut((7, 1, 9)) if False else None
    from parts import hut_block; hut_block(d, (6, 1, 9))
    # roof: his builder level 1 shallow roof fits 7-wide walls; tiled to 11 along z (9 walls + 1 overhang each end)
    m = RoofModule.from_build('builder_level1').tiled(1, 11)
    m.stamp(d, (box[0] - 1, w.top + 1, box[1] - 1), BY_LEVEL[2])
        # yard
    d.fence((1, 1, 15, 12), 0, 'wattle-sticks', gaps=[(5, 12)])
    d.path([(5, 12), (5, 13)], -1)
    woodpile(d, [(11, 2), (12, 2), (13, 2)], 0, 2, 'ns')
    garden(d, (11, 5, 14, 10), -1)
    grass(d, (0, 0, 16, 13), -1, every=7)
    return d

d = residence2()
probs = d.check('residence') + check_floors(d) + check_floating(d)
print('problems', probs)

import preview; preview.sheet([d], ["residence small level 2 (prototype)"], "out_proto/residence2.png")
