"""Reference renders of existing assets under the art v2 studio light (docs/images/style/*.png).

blender -b --python Tools/AssetPipeline/render_style_reference.py
"""
import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent))
import bpy
import mong_art_common as A

A.reset(); A.studio()
rigs = A.append_reference_characters(offset_x=(-1.3, 1.3))
chars = [o for c in ('Monkey', 'Police') for o in bpy.data.collections[c].objects if o.type == 'MESH']
for v in ('front', 'three_quarter', 'side'):
    A.render(A.STYLE_IMAGES / f'characters_{v}.png', chars, v, res=(1200, 900), size=5.2, center=(0, 0, 1.1))

# Props from MuhanokModules.blend at their authored scale, beside the monkey for size.
src = str(A.BLEND_DIR / 'MuhanokModules.blend')
want = ['MineCart', 'RockPile', 'ZooGiraffe', 'ZooElephant']
with bpy.data.libraries.load(src, link=False) as (f, t):
    t.collections = [c for c in f.collections if c in want]
x = 1.6
props = []
for col in t.collections:
    bpy.context.scene.collection.children.link(col)
    obs = [o for o in col.objects if o.type == 'MESH']
    lo, hi = A._bounds(obs)
    for o in obs:
        o.location.x += x - lo.x; o.location.z -= lo.z
    x += (hi.x - lo.x) + .8
    props += obs
police = bpy.data.collections['Police']
for o in police.objects: o.hide_render = True
rigs['Monkey'].location.x = 0
A.render(A.STYLE_IMAGES / 'props_three_quarter.png', props + [o for o in bpy.data.collections['Monkey'].objects if o.type == 'MESH'],
         'three_quarter', res=(1600, 800))
print('STYLE_REFERENCE_PASS')
