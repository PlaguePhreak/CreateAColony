"""Writes assets/createacolony/shapes/item/coin.json: a round copper coin like a British 1p.

The disc is twelve-sided: six thin boxes the width of one side, turned 30 degrees apart about its centre.
A raised rim is twelve short boxes around the edge. On the top face a portcullis is raised: three posts, three
bars and two chains hanging from the top bar, as on the penny's reverse. Run: python3 tools/models/coin_model.py
"""
import json
import math
import os

C = 8.0          # centre, in 1/16 block
R = 4.0          # radius to a corner of the twelve sides
T = 0.5          # thickness of the disc
RIM = 0.12       # rim height above the face
EMBOSS = 0.1     # design height above the face
SIDE = 2 * R * math.sin(math.pi / 12)   # length of one of the twelve sides
APOTHEM = R * math.cos(math.pi / 12)


def face(u0, v0, u1, v1):
    return {"texture": "#metal", "uv": [round(u0, 3), round(v0, 3), round(u1, 3), round(v1, 3)]}


def box(name, frm, to, rot_y=0.0):
    w, h, d = (to[i] - frm[i] for i in range(3))
    faces = {
        "north": face(0, 0, w, h), "south": face(0, 0, w, h),
        "east": face(0, 0, d, h), "west": face(0, 0, d, h),
        "up": face(frm[0], frm[2], to[0], to[2]), "down": face(frm[0], frm[2], to[0], to[2]),
    }
    el = {"name": name, "from": [round(x, 3) for x in frm], "to": [round(x, 3) for x in to], "faces": faces}
    if rot_y:
        el["rotationOrigin"] = [C, 0, C]
        el["rotationY"] = rot_y
    return el


elements = []
# The disc: each box spans the full width across two opposite sides and is one side wide.
for i in range(6):
    elements.append(box(f"Disc{i}", [C - APOTHEM, 0, C - SIDE / 2], [C + APOTHEM, T, C + SIDE / 2], i * 30.0))
# The rim, on both faces: a short box along each side, just inside the edge.
for i in range(12):
    angle = i * 30.0
    for top, (y0, y1) in (("Top", (T, T + RIM)), ("Bottom", (-RIM, 0))):
        elements.append(box(f"Rim{top}{i}", [C + APOTHEM - 0.45, y0, C - SIDE / 2], [C + APOTHEM - 0.05, y1, C + SIDE / 2], angle))

# The portcullis on the top face.
y0, y1 = T, T + EMBOSS
for name, frm, to in [
    ("PostLeft", [6.4, y0, 5.6], [6.9, y1, 10.0]),
    ("PostRight", [9.1, y0, 5.6], [9.6, y1, 10.0]),
    ("PostMiddle", [7.75, y0, 5.6], [8.25, y1, 10.0]),
    ("BarTop", [5.6, y0, 5.6], [10.4, y1, 6.1]),
    ("BarMiddle", [6.2, y0, 7.6], [9.8, y1, 8.0]),
    ("BarBottom", [6.2, y0, 9.4], [9.8, y1, 9.8]),
    ("ChainLeft", [5.6, y0, 6.1], [5.9, y1, 8.4]),
    ("ChainRight", [10.1, y0, 6.1], [10.4, y1, 8.4]),
    ("CrownBand", [7.0, y0, 4.6], [9.0, y1, 5.0]),
]:
    elements.append(box(name, frm, to))
# A large "1" on the bottom face, as on the old penny's other side.
yb0, yb1 = -EMBOSS, 0
for name, frm, to in [
    ("OneStem", [7.75, yb0, 6.0], [8.25, yb1, 10.0]),
    ("OneFlag", [7.2, yb0, 6.0], [7.75, yb1, 6.6]),
    ("OneFoot", [7.2, yb0, 9.6], [8.8, yb1, 10.0]),
]:
    elements.append(box(name, frm, to))

shape = {
    "textureWidth": 16,
    "textureHeight": 16,
    "textures": {"metal": "game:block/metal/ingot/copper"},
    "elements": elements,
}
out = os.path.join(os.path.dirname(__file__), "..", "..", "assets", "createacolony", "shapes", "item", "coin.json")
with open(out, "w") as f:
    json.dump(shape, f, indent=1)
print("wrote", os.path.normpath(out), len(elements), "elements")
