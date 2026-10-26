"""Preview pictures of designs: one isometric view per level, side by side."""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from PIL import Image, ImageDraw, ImageFont
import render
render.COLOURS.insert(0, ("roof", (150, 60, 45)))
render.COLOURS[0:0] = [("roof#drygrass", (196, 172, 92)), ("roof#thatch", (205, 150, 62)), ("roof#wood", (128, 86, 48)), ("roof#shingle", (168, 72, 54))]
render.COLOURS += [("rock-", (140, 130, 120)), ("daub", (215, 195, 150)), ("farmland", (100, 70, 40)), ("tallgrass", (90, 150, 60)), ("flower", (90, 150, 60)), ("supportbeam", (106, 76, 44)), ("stonebricks", (120, 125, 135)), ("stonebrickstairs", (120, 125, 135)), ("stonebrickslab", (120, 125, 135)), ("debarkedlog", (170, 130, 85)), ("forge", (70, 60, 60)), ("anvil", (95, 95, 105)), ("toolrack", (110, 80, 50)), ("crate", (170, 125, 70)), ("groundstorage", (150, 150, 140)), ("trunk", (150, 100, 50)), ("shelf", (130, 90, 55)), ("foodshelves", (140, 95, 55)), ("stonebakeoven", (120, 100, 90)), ("aculinaryartillery", (110, 80, 60)), ("woodbucket", (150, 110, 60)), ("cabinet", (125, 85, 50)), ("cob", (190, 170, 120)), ("plaster", (235, 225, 200)), ("claybrick", (170, 95, 70)), ("glass", (180, 210, 230)), ("trapdoor", (150, 110, 70))]


def render_build(b, s=12, back=False):
    hut = b.hut()
    rm = getattr(b, 'roof_mat', {})
    f = (lambda x, z: (-x, -z)) if back else (lambda x, z: (x, z))
    bl = {f(x - hut[0], z - hut[2])[0:1] + (y - hut[1],) + f(x - hut[0], z - hut[2])[1:]: ('roof#' + rm[(x, y, z)] if (x, y, z) in rm else c) for (x, y, z), c in b.blocks.items()
          if 'multiblock' not in c and (not c.startswith('game:soil') and 'claystone' not in c or y >= hut[1])}
    w, h = render.picture_size(bl, s)
    img = Image.new('RGBA', (int(w) + 20, int(h) + 20), (238, 242, 246, 255))
    x0, x1, y0, y1, z0, z1 = render.bounds(bl)
    ox = 10 - (x0 + z0) * s * 0.866; oy = 10 + (x1 - z0) * s * 0.5 + y1 * s
    render.draw(img, bl, ox, oy, s)
    return img.crop(img.getbbox())


def sheet(builds, titles, path, both=False):
    """One row of front views; with both=True a second row of views from the back."""
    rows = [[render_build(b) for b in builds]]
    if both: rows.append([render_build(b, back=True) for b in builds])
    try: font = ImageFont.truetype("DejaVuSans-Bold.ttf", 14)
    except OSError: font = ImageFont.load_default()
    w = max(sum(i.width + 20 for i in r) for r in rows) + 20
    heights = [max(i.height for i in r) + 60 for r in rows]
    out = Image.new('RGB', (w, sum(heights)), (238, 242, 246)); d = ImageDraw.Draw(out)
    y = 0
    for r, h, side in zip(rows, heights, ('', ' (from behind)')):
        x = 20
        for i, t in zip(r, titles):
            out.paste(i, (x, y + h - 20 - i.height)); d.text((x, y + 10), t + side, fill=(30, 30, 30), font=font); x += i.width + 20
        y += h
    os.makedirs(os.path.dirname(path) or '.', exist_ok=True)
    out.save(path)
