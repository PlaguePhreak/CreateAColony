"""The roof module library: references by shape and wall span. Until Aaron's reference export arrives,
the entries marked stand-in come from his existing buildings."""
from roofs import RoofModule

_cache = {}
def _get(key, maker):
    if key not in _cache: _cache[key] = maker()
    return _cache[key]

# gables / hips by wall span across the ridge: (module, wall span, overhang each end along the ridge, layers)
def gable(span):
    """A roof for walls `span` wide across the ridge; returns (module, end_overhang). The module's ridge runs
    along z; tile it to walls_length + 2 * end_overhang."""
    if span == 5: return _get('g5', lambda: RoofModule.from_build('forester_small_level1')), 0        # stand-in: 5x5 shallow pyramid
    if span == 7: return _get('g7', lambda: RoofModule.from_build('builder_level1')), 1                # stand-in: 7x7 shallow hip, wood
    raise KeyError(f'no gable reference for span {span} yet')

def witch_hat(size):
    """A witch-hat for a square tower `size` wide (outer walls); the module is size+2 wide (eave overhang)."""
    if size == 7: return _get('h7', lambda: RoofModule.from_build('guardtower_level4'))
    if size == 9: return _get('h9', lambda: RoofModule.from_build('guardtower_level5', box=(2, 25, 1, 12, 33, 11)))
    if size == 5: return _get('h5', lambda: RoofModule.from_build('forester_small_level1'))               # stand-in
    raise KeyError(f'no witch-hat reference for {size}x{size} yet')
