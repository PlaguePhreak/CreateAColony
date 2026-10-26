"""Second-pass building helpers on top of walls.py: roofs from the module library, annexes, towers, yards."""
from walls import *
import roofs_lib
from parts import yard, grass, woodpile, garden, hut_block, flag, banner, table_and_chairs, opening


def gable_over(d, box, top, axis, material, abut=None):
    """A gable/hip roof from the library over walls `box` whose top wall layer is `top`. axis 1: ridge
    along z. abut: the side of the box that meets a taller wall; the roof runs plain into it."""
    x0, z0, x1, z1 = box
    span = (x1 - x0 + 1) if axis == 1 else (z1 - z0 + 1)
    length = (z1 - z0 + 1) if axis == 1 else (x1 - x0 + 1)
    m, end_over = roofs_lib.gable(span)
    low_end = {1: 'north', 0: 'west'}[axis]; high_end = {1: 'south', 0: 'east'}[axis]
    caps = (abut != low_end, abut != high_end)
    ext_low = end_over if caps[0] else 0; ext_high = end_over if caps[1] else 0
    t = m.tiled(1, length + ext_low + ext_high, caps=caps)
    side_over = (m.size[0] - span) // 2
    if axis == 0: t = t.rotated(1)
    origin = (x0 - side_over, top + 1, z0 - ext_low) if axis == 1 else (x0 - ext_low, top + 1, z0 - side_over)
    t.stamp(d, origin, material)
    d.rooms.append((box, top))
    return t


def hat_over(d, box, top, material):
    x0, z0, x1, z1 = box
    size = x1 - x0 + 1
    m = roofs_lib.witch_hat(size)
    over = (m.size[0] - size) // 2
    m.stamp(d, (x0 - over, top + 1, z0 - over), material)
    d.rooms.append((box, top))
    return m


def ladder_up(d, x, z, y0, y1, facing, floors=()):
    """A ladder from y0 to y1 with a hole cut in each floor it passes."""
    d.ladder(x, z, y0, y1, facing)
    for fy in floors:
        if y0 <= fy <= y1: d.set((x, fy, z), f'game:ladder-wood-oak-{facing}')


def well(d, x, z, y=0):
    """A cobble well ring with a bucket on a post beam."""
    for (dx, dz) in ((-1, -1), (0, -1), (1, -1), (-1, 0), (1, 0), (-1, 1), (0, 1), (1, 1)): d.set((x + dx, y, z + dz), COBBLE)
    d.set((x, y, z), None); d.set((x, y - 1, z), 'game:water-still-7' if 'game:water-still-7' in CATALOG else 'game:soil-medium-none')
    d.set((x - 1, y + 1, z), LOG); d.set((x + 1, y + 1, z), LOG); d.set((x - 1, y + 2, z), LOG); d.set((x + 1, y + 2, z), LOG)
    for dx in (-1, 0, 1): d.set((x + dx, y + 3, z), LOG_X)
    d.place('game:woodbucket', (x, y + 1, z)) if 'game:woodbucket' in CATALOG else None


def lantern_post(d, x, z, y=0, height=3):
    for yy in range(y, y + height): d.set((x, yy, z), LOG)
    d.lantern((x, y + height, z))


def stone_court(d, box, y=-1, code=COBBLE, every=7):
    """A cobbled court with the odd polished flag in it."""
    x0, z0, x1, z1 = box
    i = 0
    for x in range(x0, x1 + 1):
        for z in range(z0, z1 + 1):
            i += 1
            d.set((x, y, z), POLISHED if i % every == 0 else code)
            if d.get((x, y + 1, z)).startswith(('game:tallgrass', 'game:flower')): d.set((x, y + 1, z), None)
