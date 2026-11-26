"""Fork signs above the rail tunnel entrances: one flat icon + one map word each.

blender -b --python Tools/AssetPipeline/build_art_signs.py [-- --no-render]
- MD_Sign_Prison (cell bars), MD_Sign_Zoo (paw print), MD_Sign_Mine (pickaxe), in Staging/Blender/Models.
- Board 2.8 x 1.9 m, dark board #2A1D14 + ink #FFD36B (guide 2-3), word in the HUD title font (Black Han Sans, OFL).
- Faces -Y (the approaching cart), origin at the bottom centre of the frame: put it on the tunnel lintel.
- Two small lanterns (in-game LanternGlass) light the board in the dark mine, like the existing fork boards.
"""
import sys, math
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent))
import bpy
from mathutils import Vector
import mong_art_common as A

A.reset()
BOARD = A.mat('SignBoard', '#2A1D14', .8)
INK = A.mat('SignInk', '#FFD36B', .6)
TIMBER = A.mat('Timber', '#C7B08F', .78)
IRON = A.mat('ForgedIron', '#333D45', .64, .48)
LANTERN = A.mat('LanternGlass', '#FF9C30', .65, emit='#FF9C30', strength=3)
BW, BH = 2.8, 1.9
FACE = -.09        # ink surface y (board front is at -.06)
ICON_Z = 1.32      # icon centre height above the origin
WORD_Z = .48

def collect(name, objs):
    ob = A.join(objs, name)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    A.set_origin(ob, (0, 0, 0))
    return ob

def ink_box(name, x, z, w, h, rot=0.0):
    return A.box(name, (x, FACE, z), (w, .04, h), INK, .12, segments=1, rot=(0, math.radians(rot), 0))

def board(word):
    p = [A.box('Board', (0, 0, BH / 2 + .1), (BW, .12, BH), BOARD, .06, segments=1),
         A.box('Frame top', (0, -.02, BH + .14), (BW + .24, .18, .16), TIMBER, .15, segments=1),
         A.box('Frame bottom', (0, -.02, .06), (BW + .24, .18, .16), TIMBER, .15, segments=1)]
    for s in (-1, 1):
        p.append(A.box('Frame side', (s * (BW / 2 + .04), -.02, BH / 2 + .1), (.16, .18, BH + .3), TIMBER, .15, segments=1))
        p.append(A.box('Lantern arm', (s * (BW / 2 - .25), -.25, BH + .32), (.06, .45, .06), IRON, .2, segments=1))
        p.append(A.box('Lantern cap', (s * (BW / 2 - .25), -.45, BH + .3), (.2, .2, .06), IRON, .2, segments=1))
        p.append(A.box('Lantern glass', (s * (BW / 2 - .25), -.45, BH + .18), (.15, .15, .18), LANTERN, .2, segments=1))
    p.append(A.text_mesh('Word', word, (0, FACE, WORD_Z), .52, INK, depth=.04))
    return p

def icon_prison():
    """Barred cell window: rounded frame + 4 bars + cross bar."""
    p = [ink_box('Bars frame top', 0, ICON_Z + .36, .92, .1), ink_box('Bars frame bottom', 0, ICON_Z - .36, .92, .1),
         ink_box('Bars frame left', -.41, ICON_Z, .1, .82), ink_box('Bars frame right', .41, ICON_Z, .1, .82),
         ink_box('Cross bar', 0, ICON_Z + .02, .8, .07)]
    for k in range(4):
        p.append(ink_box('Bar', -.24 + k * .16, ICON_Z, .07, .72))
    return p

def icon_zoo():
    """Paw print: big pad + four toes (same mark as the zoo entrance sign)."""
    p = [A.ball('Paw pad', (0, FACE, ICON_Z - .1), (.27, .03, .22), INK, 14, 5, smooth=False)]
    for k, (dx, dz, s) in enumerate(((-.31, .17, .1), (-.12, .34, .11), (.12, .34, .11), (.31, .17, .1))):
        p.append(A.ball('Paw toe', (dx, FACE, ICON_Z + dz), (s, .03, s * 1.25), INK, 12, 5, smooth=False))
    return p

def flat_poly(name, pts, depth=.04):
    """Flat extruded ink shape from 2D points (x, z) on the board face."""
    import bmesh
    me = bpy.data.meshes.new(name); bm = bmesh.new()
    front = [bm.verts.new((x, FACE, z)) for x, z in pts]
    back = [bm.verts.new((x, FACE + depth, z)) for x, z in pts]
    bm.faces.new(front); bm.faces.new(list(reversed(back)))
    n = len(pts)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((front[j], front[i], back[i], back[j]))
    bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 4])   # concave caps: no ngons in the FBX
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(me); bm.free()
    ob = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(ob)
    me.materials.append(INK)
    return ob

def icon_mine():
    """Pickaxe: a tapered crescent head on a diagonal handle, rotated as one shape."""
    rot = math.radians(-35); cx, cz = .0, ICON_Z
    def tr(x, z):
        return (cx + x * math.cos(rot) - z * math.sin(rot), cz + x * math.sin(rot) + z * math.cos(rot))
    head = []
    for i in range(13):                         # outer arc, left tip -> right tip
        a = math.radians(-78 + 156 * i / 12)
        head.append((math.sin(a) * .56, .2 + math.cos(a) * .3))
    for i in range(13):                         # inner arc back, thinner at the tips
        a = math.radians(78 - 156 * i / 12)
        head.append((math.sin(a) * .5, .2 + math.cos(a) * .3 - .13 * math.cos(a) ** 2 - .012))
    handle = [(-.055, .42), (.055, .42), (.07, -.55), (-.07, -.55)]
    collar = [(-.1, .38), (.1, .38), (.1, .2), (-.1, .2)]
    return [flat_poly('Pick head', [tr(x, z) for x, z in head]),
            flat_poly('Pick handle', [tr(x, z) for x, z in handle]),
            flat_poly('Pick collar', [tr(x, z) for x, z in collar], depth=.05)]

signs = {'MD_Sign_Prison': ('감옥', icon_prison), 'MD_Sign_Zoo': ('동물원', icon_zoo), 'MD_Sign_Mine': ('광산', icon_mine)}
objs = {}
for i, (name, (word, icon)) in enumerate(signs.items()):
    ob = collect(name, board(word) + icon())
    objs[name] = ob
tris = {k: A.triangles([v]) for k, v in objs.items()}
print('SIGN_TRIS', tris)

A.write_palette('Signs')
for name, ob in objs.items():
    A.export_fbx(A.OUT_MODELS / (name + '.fbx'), [ob], armature=False)
    A.register_asset(A.OUT_MODELS / (name + '.fbx'), 'prop', 'Signs')
for i, ob in enumerate(objs.values()):
    ob.location.x = (i - 1) * 3.6
A.save_blend('ArtV2_Signs')

if '--no-render' not in A.args():
    A.studio()
    out = A.REVIEW / 'ForkSigns'
    for name, ob in objs.items():
        A.solo([ob]); A.render_turn(out, name, [ob], views=('front', 'three_quarter'), res=(900, 700), size=3.6, center=(ob.location.x, 0, 1.1))
    allobs = list(objs.values())
    A.solo(allobs)
    A.render(out / 'ForkSigns_front.png', allobs, 'front', res=(1800, 700), size=11.2, center=(0, 0, 1.1))
    # cart rider's view: three signs above three tunnel mouths, 20 m and 10 m ahead, eye 2.3 m (seated in the cart)
    # signs placed at y=+6, behind the studio area lights (one sits at (4,-1,4) and would slice a sign hung there)
    for i, ob in enumerate(allobs):
        ob.location = ((i - 1) * 4.2, 6, 3.4)
    for d in (20, 10):
        A.render_eye(out / f'ForkSigns_cart_view_{d}m.png', (0, 6 - d, 2.3), (0, 6, 3.6))
    # scale next to the monkey and police
    for i, ob in enumerate(allobs):
        ob.location = ((i - 1) * 3.6, 0, 0)
    rig = A.load_monkey_rig(); mm = [o for o in bpy.data.collections['Monkey'].objects if o.type == 'MESH']
    refs = A.append_reference_characters(offset_x=(99, -6.4))
    police = [o for o in bpy.data.collections['Police'].objects if o.type == 'MESH']
    rig.location = (6.2, -.4, 0)
    A.solo(allobs + mm + police)
    A.render(out / 'ForkSigns_compare.png', allobs + mm + police, 'three_quarter', res=(1800, 800), size=15, center=(0, 0, 1.2))
print('SIGNS_PASS', tris)
