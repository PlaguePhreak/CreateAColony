"""Builds the Oak & Shale pack: every type, levels 1 to 5, drawn new with the design kit.
python3 run.py [type ...]   writes assets/.../blueprints/oakshale/ and preview sheets to out_oakshale/"""
import os, sys, importlib, collections
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import kit2, roofdata, preview
from design import Design

PACK = os.path.normpath(os.path.join(HERE, '..', '..', '..', 'assets', 'createacolony', 'config', 'blueprints', 'oakshale'))
PREVIEWS = os.path.join(HERE, 'out_oakshale')
GROUPS = ['recipes_homes', 'recipes_town', 'recipes_land', 'recipes_crafts', 'recipes_metal', 'recipes_mining', 'recipes_military']

RECIPES = {}
for g in GROUPS:
    try:
        m = importlib.import_module(g)
        RECIPES.update(m.RECIPES)
    except ModuleNotFoundError:
        pass


def build_all(which=None, save=True):
    results = {}
    for (type, variant), fn in RECIPES.items():
        if which and type not in which and f'{type}/{variant}' not in which: continue
        builds = []
        for level in range(1, 6):
            d = fn(level)
            n, ex = d.finish_roofs()
            problems = d.save_to(PACK) if save else d.check(type)
            x0, y0, z0, x1, y1, z1 = d.bounds()
            tag = f'{type}{"/" + variant if variant else ""} {level}'
            print(f'{tag:24s} {x1 - x0 + 1:2d}x{y1 - y0 + 1:2d}x{z1 - z0 + 1:2d} blocks {len(d.blocks):5d} roof {n:3d} ({ex} exact)' + (f'  PROBLEMS {problems}' if problems else ''))
            builds.append(d); results[(type, variant, level)] = (d, problems)
        os.makedirs(PREVIEWS, exist_ok=True)
        preview.sheet(builds, [f'{type} {variant} level {l}'.replace('  ', ' ') for l in range(1, 6)],
                      os.path.join(PREVIEWS, f'preview_{type}{"_" + variant if variant else ""}.png'))
    bad = {k: p for k, (d, p) in results.items() if p}
    print(len(results), 'files,', len(bad), 'with problems')
    return results


if __name__ == '__main__':
    build_all(sys.argv[1:] or None)
