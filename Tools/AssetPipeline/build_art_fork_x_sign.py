"""Blocked-fork X sign: stands on the rail the player may not take this lap (plan 3-2).

blender -b --python Tools/AssetPipeline/build_art_fork_x_sign.py [-- --no-render]
- MD_Sign_ForkX in Staging/Blender/Models. No text, one flat symbol (a big X), like the fork signs.
- Same board as MD_Sign_* (dark board #2A1D14, Timber frame, two LanternGlass lanterns) so it reads as part
  of the sign set; the X is GateWarningRed #FF0904 with a low glow, because a closed path is a warning
  (style guide 2-1: red glow is only for danger and warnings). Lanterns light it in the dark mine.
- Board 1.7 x 1.7 m on two posts, 2.0 m wide overall: fits one 2.2 m lane, board centre 2.0 m up
  (cart rider eye 2.3 m), so it stands in front of the tunnel mouth and under the map sign.
- Faces -Y (the approaching cart), origin at the bottom centre between the posts: put it on the blocked
  rail at the point where the fork starts.
"""
import sys, math
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent))
import bpy
import mong_art_common as A

A.reset()
BOARD = A.mat('SignBoard', '#2A1D14', .8)
TIMBER = A.mat('Timber', '#C7B08F', .78)
IRON = A.mat('ForgedIron', '#333D45', .64, .48)
LANTERN = A.mat('LanternGlass', '#FF9C30', .65, emit='#FF9C30', strength=3)
RED = A.mat('GateWarningRed', '#FF0904', .55, emit='#FF0904', strength=.45)
BW = BH = 1.7
BZ = 1.15                      # board bottom
CZ = BZ + BH / 2               # board centre, 2.0 m
FACE = -.09                    # symbol surface y (board front is at -.06)
POST_X = BW / 2 + .1

p = [A.box('Board', (0, 0, CZ), (BW, .12, BH), BOARD, .06, segments=1),
     A.box('Frame top', (0, -.02, BZ + BH + .04), (BW + .24, .18, .16), TIMBER, .15, segments=1),
     A.box('Frame bottom', (0, -.02, BZ - .04), (BW + .24, .18, .16), TIMBER, .15, segments=1)]
for s in (-1, 1):
    p.append(A.box('Post', (s * POST_X, -.02, (BZ + BH + .12) / 2), (.16, .18, BZ + BH + .12), TIMBER, .15, segments=1))
    p.append(A.box('Post foot', (s * POST_X, -.02, .04), (.34, .34, .08), IRON, .2, segments=1))
    p.append(A.box('Lantern arm', (s * (BW / 2 - .2), -.25, BZ + BH + .2), (.06, .45, .06), IRON, .2, segments=1))
    p.append(A.box('Lantern cap', (s * (BW / 2 - .2), -.45, BZ + BH + .18), (.2, .2, .06), IRON, .2, segments=1))
    p.append(A.box('Lantern glass', (s * (BW / 2 - .2), -.45, BZ + BH + .06), (.15, .15, .18), LANTERN, .2, segments=1))
# the X: two thick bars, slightly rounded ends, flat on the board face
L, W = 1.62, .3
for k, ang in enumerate((45, -45)):
    p.append(A.box('X bar', (0, FACE - k * .005, CZ), (L, .05, W), RED, .1, segments=2, rot=(0, math.radians(ang), 0)))

ob = A.join(p, 'MD_Sign_ForkX')
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
A.set_origin(ob, (0, 0, 0))
tris = A.triangles([ob])
print('FORKX_TRIS', tris)

A.write_palette('Signs')
A.export_fbx(A.OUT_MODELS / 'MD_Sign_ForkX.fbx', [ob], armature=False)
A.register_asset(A.OUT_MODELS / 'MD_Sign_ForkX.fbx', 'prop', 'Signs')
A.save_blend('ArtV2_SignForkX')

if '--no-render' not in A.args():
    A.studio()
    out = A.REVIEW / 'ForkXSign'
    A.solo([ob])
    A.render_turn(out, 'MD_Sign_ForkX', [ob], views=('front', 'three_quarter', 'side'), res=(900, 900), size=3.2, center=(0, 0, 1.4))
    # with the three fork signs (appended from ArtV2_Signs.blend) for the set look
    with bpy.data.libraries.load(str(A.BLEND_DIR / 'ArtV2_Signs.blend'), link=False) as (f_, t_):
        t_.objects = [n for n in f_.objects if n.startswith('MD_Sign_')]
    signs = [o for o in t_.objects if o]
    for o in signs:
        bpy.context.scene.collection.objects.link(o)
    order = {'MD_Sign_Zoo': -1, 'MD_Sign_Prison': 0, 'MD_Sign_Mine': 1}
    for o in signs:
        o.location = (order.get(o.name, 0) * 3.6, 1.0, 0)
    ob.location = (6.6, -.6, 0)
    A.solo([ob] + signs)
    A.render(out / 'ForkXSign_with_fork_signs.png', [ob] + signs, 'three_quarter', res=(1800, 800), size=14.5, center=(1.6, 0, 1.3))
    # cart rider's view: map signs over three tunnel mouths at y=+6 (z 3.4); the X stands on the blocked lane,
    # 4 m in front of its tunnel. Two takes: prison (centre) blocked, zoo (left) blocked.
    for o in signs:
        o.location = (order.get(o.name, 0) * 4.2, 6, 3.4)
    for tag, lane in (('center_blocked', 0), ('left_blocked', -1)):
        ob.location = (lane * 4.2, 2, 0)
        A.solo([ob] + signs)
        for d in (20, 10):
            A.render_eye(out / f'ForkXSign_cart_view_{tag}_{d}m.png', (0, 6 - d, 2.3), (0, 6, 2.6))
    # scale next to the monkey and police
    ob.location = (0, 0, 0)
    for o in signs: o.hide_render = True
    rig = A.load_monkey_rig(); mm = [o for o in bpy.data.collections['Monkey'].objects if o.type == 'MESH']
    refs = A.append_reference_characters(offset_x=(99, -2.6))
    police = [o for o in bpy.data.collections['Police'].objects if o.type == 'MESH']
    rig.location = (2.2, -.4, 0)
    A.solo([ob] + mm + police)
    A.render(out / 'ForkXSign_compare.png', [ob] + mm + police, 'three_quarter', res=(1400, 800), size=8, center=(0, 0, 1.3))
print('FORKX_PASS', tris)
