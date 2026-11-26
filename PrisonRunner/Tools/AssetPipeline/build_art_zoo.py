"""Zoo chase set: squeeze bars, jump fence, water puddle, entrance/exit gates and the descending iron gate.

blender -b --python Tools/AssetPipeline/build_art_zoo.py [-- --no-render]
All face -Y (Unity +Z, the running direction comes from -Y), origin on the floor at the lane centre.
- MD_Zoo_SqueezeBars : two barred cage walls (Left/Right children) that slide in from both sides.
  Take AN_Zoo_SqueezeBars_Close 24f: open gap 3.6 m (frame 1) -> closed gap 0.9 m (frame 24). Play reversed to open.
- MD_Zoo_Fence       : 0.75 m wooden fence across the lane (jump).
- MD_Zoo_Puddle      : 2.6 x 1.7 m muddy water puddle (jump).
- MD_Zoo_Entrance / MD_Zoo_Exit : log archway with a sign word, gates swung open.
- MD_Zoo_IronGate    : posts + beam + barred portcullis (child 'Grate'), take AN_Zoo_IronGate_Descend 180f (6 s) 3.1 m -> 0.
Style: ART_STYLE_GUIDE.md (bevelled flat props, zoo palette, warning colours only on hazards).
"""
import sys, math, random
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent))
import bpy
from mathutils import Vector
import mong_art_common as A
import mong_anim as N

A.reset(); random.seed(1104)
BARK = A.mat('ZooLogBark', '#80664A', .78)
IRON = A.mat('ForgedIron', '#333D45', .64, .48)
CAUTION = A.mat('ZooCaution', '#FFBD14', .7)
EARTH = A.mat('ZooEarth', '#33542B', .9)
GRASS = A.mat('ZooGrass', '#245226', .8)
LEAF = A.mat('ZooFoliage', '#2B6629', .8)
WATER = A.mat('ZooWater', '#2F7393', .4, emit='#3A8FB8', strength=.35)   # smoothness 0.6 + faint glow: glints at night
RIPPLE = A.mat('ZooWaterRipple', '#7FC0D8', .2)
MUD = A.mat('ZooMud', '#4A3423', .9)
STONE = A.mat('blender_Stone_3', '#DACCB6', .78)
BOARD = A.mat('SignBoard', '#2A1D14', .8)
INK = A.mat('SignInk', '#FFD36B', .6)
WARN = A.mat('GateWarningRed', '#FF0904', .55, emit='#FF0904', strength=2.5)
BLACK = A.mat('Charcoal', '#060709', .67)
LANE = 3.6   # zoo path width the props span

def collect(name, objs, origin=(0, 0, 0)):
    ob = A.join(objs, name)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    A.set_origin(ob, origin)
    return ob

def parent_keep(child, parent):
    child.parent = parent; child.matrix_parent_inverse = parent.matrix_world.inverted()

def log(name, a, b, r, mat=BARK):
    return A.rod(name, a, b, r, mat, verts=8, smooth=False)

assets = {}

# ------------------------------------------------------------------ squeeze bars
def bar_wall(side):
    s = 1 if side == 'Right' else -1
    parts = []
    L, H = 3.0, 3.0
    parts.append(A.box('Bottom beam', (0, 0, .15), (.24, L, .3), BARK, .15, segments=1))
    parts.append(A.box('Top beam', (0, 0, H), (.26, L + .1, .26), BARK, .15, segments=1))
    parts.append(A.box('Hazard edge', (-s * .14, 0, H), (.04, L + .1, .14), CAUTION, .2, segments=1))
    for y in (-L / 2, L / 2):
        parts.append(A.box('Post', (0, y, H / 2), (.28, .28, H + .1), BARK, .15, segments=1))
    for k in range(7):
        parts.append(A.cyl('Bar', (0, -L / 2 + .375 + k * .375, H / 2 + .1), .05, H - .25, IRON, 8))
    # vertical caution strip on the front post's inner edge, floor to shoulder: the closing gap reads as two yellow lines
    parts.append(A.box('Post caution', (-s * .1, -L / 2 - .155, .85), (.14, .04, 1.7), CAUTION, .1, segments=1))   # faces the runner
    for k in range(6):
        parts.append(A.box('Post caution stripe', (-s * .1, -L / 2 - .177, .2 + k * .28), (.15, .005, .07), BLACK, 0, rot=(0, math.radians(-s * 30), 0)))
    # inward-pointing bar stubs make the squeeze read as dangerous to shoulders, not to the head
    for k in range(4):
        parts.append(A.cyl('Shoulder stub', (-s * .18, -1.1 + k * .73, 1.45), .045, .36, IRON, 8, rot=(0, math.radians(90), 0)))
    ob = collect('Bars' + side, parts)
    ob.location.x = 0
    return ob

left, right = bar_wall('Left'), bar_wall('Right')
root = bpy.data.objects.new('MD_Zoo_SqueezeBars', None); bpy.context.scene.collection.objects.link(root)
for w in (left, right): parent_keep(w, root)
for w, s in ((left, -1), (right, 1)):
    w.animation_data_create(); w.animation_data.action = bpy.data.actions.new('Squeeze_' + w.name)
    N.key_object(w, 1, loc=(s * 1.8, 0, 0)); N.key_object(w, 20, loc=(s * .42, 0, 0)); N.key_object(w, 24, loc=(s * .45, 0, 0))
assets['MD_Zoo_SqueezeBars'] = ([root, left, right], 24)

# ------------------------------------------------------------------ fence (jump)
fp = []
for x in (-1.6, -.53, .53, 1.6):
    fp.append(A.box('Fence post', (x, 0, .42), (.16, .16, .84), BARK, .15, segments=1))
    fp.append(A.box('Post cap', (x, 0, .87), (.2, .2, .06), BARK, .3, segments=1))
for z in (.32, .66):
    fp.append(A.box('Fence rail', (0, -.06, z), (LANE - .2, .07, .13), BARK, .2, segments=1))
for k in range(9):
    x = -1.45 + k * .3625
    fp.append(A.box('Picket', (x, -.11, .44), (.1, .05, .7), BARK, .2, segments=1))
fp.append(A.box('Caution plate', (0, -.15, .55), (.6, .03, .22), CAUTION, .2, segments=1))
for k, dx in enumerate((-.18, 0, .18)):
    fp.append(A.box('Plate stripe', (dx, -.168, .55), (.06, .01, .2), BLACK, 0, rot=(0, math.radians(35), 0)))
for i in range(6):
    fp.append(A.ball('Grass tuft', (random.uniform(-1.7, 1.7), random.uniform(-.25, .2), .05), (.16, .12, .1), GRASS, 6, 4, smooth=False))
assets['MD_Zoo_Fence'] = ([collect('MD_Zoo_Fence', fp)], 0)

# ------------------------------------------------------------------ water puddle (jump)
bpy.ops.mesh.primitive_circle_add(vertices=24, radius=1, fill_type='NGON', location=(0, 0, .04))
water = bpy.context.object; water.name = 'Water'
for i, v in enumerate(water.data.vertices):
    a = math.atan2(v.co.y, v.co.x); r = 1 + .12 * math.sin(a * 3 + 1) + .07 * math.sin(a * 5)
    v.co.x *= 1.3 * r; v.co.y *= .85 * r
water.data.materials.append(WATER)
pp = [water]
bpy.ops.mesh.primitive_circle_add(vertices=24, radius=1, fill_type='NGON', location=(0, 0, .02))
rim = bpy.context.object; rim.name = 'Mud rim'
for v in rim.data.vertices:
    a = math.atan2(v.co.y, v.co.x); r = 1.16 + .12 * math.sin(a * 3 + 1) + .07 * math.sin(a * 5)
    v.co.x *= 1.3 * r; v.co.y *= .85 * r
rim.data.materials.append(MUD); pp.append(rim)
for r in (.35, .7):
    bpy.ops.mesh.primitive_torus_add(major_radius=r, minor_radius=.018, major_segments=20, minor_segments=4, location=(.15, -.05, .045))
    t = bpy.context.object; t.scale = (1.3, .85, .3); t.data.materials.append(RIPPLE); pp.append(t)
for i in range(16):   # raised mud berm (12 cm) so the far and near edges read as lines from eye height
    a = 2 * math.pi * i / 16
    r = 1.12 + .12 * math.sin(a * 3 + 1) + .07 * math.sin(a * 5)
    b = A.ball('Mud berm', (math.cos(a) * 1.3 * r, math.sin(a) * .85 * r, .03), (.42, .26, .12), MUD, 10, 5, smooth=False)
    b.rotation_euler.z = a + math.pi / 2; pp.append(b)
for sx in (-1, 1):    # reed clumps marking both front ends of the puddle
    for k in range(4):
        h = .6 + .2 * random.random()
        pp.append(A.cyl('Reed clump', (sx * (1.45 + .08 * k), -.55 - .06 * k, h / 2), .028, h, LEAF, 5,
                        rot=(random.uniform(-.15, .15), sx * random.uniform(.05, .25), 0)))
        pp.append(A.ball('Reed head', (sx * (1.45 + .08 * k), -.55 - .06 * k, h + .05), (.04, .04, .1), MUD, 6, 4, smooth=False))
for i in range(9):
    a = 2 * math.pi * i / 9 + random.uniform(-.2, .2)
    pp.append(A.box('Edge stone', (math.cos(a) * 1.55, math.sin(a) * 1.0, .06), (.22, .18, .12), STONE, .3, segments=1,
                    rot=(0, 0, a + random.uniform(-.4, .4))))
for i in range(5):
    a = random.uniform(0, 2 * math.pi)
    pp.append(A.cyl('Reed', (math.cos(a) * 1.5, math.sin(a) * .95, .25), .025, .5, LEAF, 5, rot=(random.uniform(-.25, .25), random.uniform(-.25, .25), 0)))
assets['MD_Zoo_Puddle'] = ([collect('MD_Zoo_Puddle', pp)], 0)

# ------------------------------------------------------------------ entrance / exit archway
def archway(name, word):
    ap = []
    W = LANE + 1.2
    for s in (-1, 1):
        ap.append(log('Gate log', (s * W / 2, 0, 0), (s * W / 2, 0, 4.2), .26))
        ap.append(A.box('Log base stone', (s * W / 2, 0, .2), (.8, .8, .4), STONE, .2, segments=1))
        for k in range(3):  # open gate leaves folded against the posts
            ap.append(A.box('Gate leaf rail', (s * (W / 2 - .2), -.6, .6 + k * .9), (.08, 1.0, .1), BARK, .2, segments=1))
        for k in range(4):
            ap.append(A.cyl('Gate leaf bar', (s * (W / 2 - .2), -.2 - k * .27, 1.5), .03, 2.1, IRON, 6))
        ap.append(A.ball('Leaf cluster', (s * (W / 2 + .1), -.1, 4.3), (.55, .45, .4), LEAF, 8, 5, smooth=False))
    ap.append(log('Arch beam', (-W / 2 - .3, 0, 4.0), (W / 2 + .3, 0, 4.0), .22))
    ap.append(A.box('Sign board', (0, -.2, 4.55), (2.6, .12, .95), BOARD, .14, segments=1))
    ap.append(A.box('Sign frame', (0, -.15, 4.55), (2.8, .1, 1.1), BARK, .14, segments=1))
    for s in (-1, 1):
        ap.append(log('Sign hanger', (s * .9, -.15, 4.0), (s * .9, -.15, 4.15), .05))
    ap.append(A.text_mesh('Sign word', word, (0, -.27, 4.53), .62, INK, depth=.02))
    # paw print emblem left of the word (zoo icon language, same as the fork sign)
    ap.append(A.ball('Paw pad', (-.95, -.275, 4.47), (.13, .02, .11), INK, 10, 4, smooth=False))
    for k in range(4):
        a = math.radians(140 - k * 33)
        ap.append(A.ball('Paw toe', (-.95 + math.cos(a) * .2, -.275, 4.52 + math.sin(a) * .17), (.055, .02, .065), INK, 8, 4, smooth=False))
    return collect(name, ap)

assets['MD_Zoo_Entrance'] = ([archway('MD_Zoo_Entrance', '동물원')], 0)
assets['MD_Zoo_Exit'] = ([archway('MD_Zoo_Exit', '출구')], 0)

# ------------------------------------------------------------------ descending iron gate (brake section)
gp = []
GW, GH = LANE + .4, 3.4
for s in (-1, 1):
    gp.append(A.box('Gate post', (s * (GW / 2 + .2), 0, GH / 2 + .2), (.4, .5, GH + .4), BARK, .12, segments=1))
    gp.append(A.box('Rail slot', (s * GW / 2, 0, GH / 2), (.1, .2, GH), IRON, .2, segments=1))
    gp.append(A.box('Lamp housing', (s * (GW / 2 + .2), -.3, GH + .25), (.26, .14, .26), IRON, .2, segments=1))
    gp.append(A.ball('Warning lamp', (s * (GW / 2 + .2), -.39, GH + .25), (.11, .05, .11), WARN, 10, 6))
gp.append(A.box('Head beam', (0, 0, GH + .55), (GW + 1.0, .6, .55), BARK, .12, segments=1))
gp.append(A.box('Gatehouse', (0, .05, GH + .8 + GH / 2), (GW + .9, .5, GH + .2), BARK, .06, segments=1))   # the raised grate hides in here
for k in range(5):
    gp.append(A.box('Gatehouse plank seam', (-GW / 2 + .1 + k * GW / 4.2, -.21, GH + .8 + GH / 2), (.05, .02, GH + .1), BLACK, 0))
gp.append(A.box('Hazard band', (0, -.31, GH + .55), (GW + .6, .03, .22), CAUTION, .2, segments=1))
for k in range(8):
    gp.append(A.box('Band stripe', (-GW / 2 + .2 + k * GW / 7.3, -.33, GH + .55), (.12, .01, .22), BLACK, 0, rot=(0, math.radians(35), 0)))
frame = collect('MD_Zoo_IronGate', gp)
grate_parts = []
for k in range(9):
    grate_parts.append(A.cyl('Grate bar', (-GW / 2 + .25 + k * (GW - .5) / 8, 0, GH / 2), .05, GH, IRON, 8))
for z in (.6, 1.6, 2.6):
    grate_parts.append(A.box('Grate rail', (0, 0, z), (GW - .1, .1, .1), IRON, .2, segments=1))
for k in range(9):  # spiked bottom edge reads "do not be under this"
    bpy.ops.mesh.primitive_cone_add(vertices=6, radius1=.07, depth=.22, location=(-GW / 2 + .25 + k * (GW - .5) / 8, 0, -.08), rotation=(math.pi, 0, 0))
    c = bpy.context.object; c.data.materials.append(IRON); grate_parts.append(c)
grate_parts.append(A.box('Bottom hazard', (0, -.07, .12), (GW - .1, .04, .12), CAUTION, .2, segments=1))
grate = collect('Grate', grate_parts)
parent_keep(grate, frame)
grate.animation_data_create(); grate.animation_data.action = bpy.data.actions.new('Grate_Descend')
N.key_object(grate, 1, loc=(0, 0, GH - .3)); N.key_object(grate, 8, loc=(0, 0, GH - .38))   # jolt as it releases
N.key_object(grate, 176, loc=(0, 0, 0)); N.key_object(grate, 180, loc=(0, 0, .04))
for fc in N.fcurves(grate.animation_data.action):
    for kp in fc.keyframe_points: kp.interpolation = 'LINEAR'
assets['MD_Zoo_IronGate'] = ([frame, grate], 180)

tris = {k: A.triangles([o for o in v[0] if o.type == 'MESH']) for k, v in assets.items()}
print('ZOO_TRIS', tris)

# ------------------------------------------------------------------ export
A.write_palette('Zoo')
sc = bpy.context.scene
for name, (objs, frames) in assets.items():
    if frames:
        sc.frame_start, sc.frame_end = 1, frames
        A.export_fbx(A.OUT_MODELS / (name + '.fbx'), objs, animated=True, armature=False)
        A.register_asset(A.OUT_MODELS / (name + '.fbx'), 'prop_anim', 'Zoo', frames=frames)
    else:
        A.export_fbx(A.OUT_MODELS / (name + '.fbx'), objs, armature=False)
        A.register_asset(A.OUT_MODELS / (name + '.fbx'), 'prop', 'Zoo')
    col = bpy.data.collections.new(name); sc.collection.children.link(col)
    for o in objs:
        for c in list(o.users_collection): c.objects.unlink(o)
        col.objects.link(o)
A.save_blend('ArtV2_Zoo')

# ------------------------------------------------------------------ review renders
if '--no-render' not in A.args():
    A.studio()
    out = A.REVIEW / 'Zoo'
    rig = A.load_monkey_rig(); mm = [o for o in bpy.data.collections['Monkey'].objects if o.type == 'MESH']
    refs = A.append_reference_characters(offset_x=(99, 0))
    police = [o for o in bpy.data.collections['Police'].objects if o.type == 'MESH']
    def meshes(name): return [o for o in assets[name][0] if o.type == 'MESH']
    # each prop alone, then with the monkey (and police) for scale
    for name in assets:
        obs = meshes(name)
        sc.frame_start, sc.frame_end = 1, max(1, assets[name][1]); sc.frame_set(1)
        A.solo(obs)
        A.render_turn(out, name, obs, res=(900, 900))
    sc.frame_start, sc.frame_end = 1, 24
    A.solo(meshes('MD_Zoo_SqueezeBars'))
    A.render_frames(out, 'AN_Zoo_SqueezeBars_Close', meshes('MD_Zoo_SqueezeBars'), [1, 8, 16, 24], (.5, -1, .55), size=6.0, center=(0, 0, 1.6),
                    gif_frames=list(range(1, 25)))
    sc.frame_start, sc.frame_end = 1, 180
    A.solo(meshes('MD_Zoo_IronGate'))
    A.render_frames(out, 'AN_Zoo_IronGate_Descend', meshes('MD_Zoo_IronGate'), [1, 60, 120, 180], 'three_quarter', size=8.2, center=(0, 0, 3.3),
                    gif_frames=list(range(1, 181, 6)))
    # the squeeze closed around the monkey doing the hands-up (Cheer pose of RescuePop if available)
    src = A.BLEND_DIR / 'ArtV2_MonkeyMotions.blend'
    if src.exists():
        with bpy.data.libraries.load(str(src), link=False) as (f, t):
            t.actions = [a for a in f.actions if a == 'AN_Monkey_RescuePop']
        rig.animation_data_create(); rig.animation_data.action = bpy.data.actions['AN_Monkey_RescuePop']
        sc.frame_set(35); rig.location = (0, 1.7, 0)   # cheer frame, Hips travel cancelled
    sc.frame_set(24)
    if src.exists(): sc.frame_set(35)
    A.solo(meshes('MD_Zoo_SqueezeBars') + mm)
    left.location.x, right.location.x = -.45, .45
    A.render(out / 'MD_Zoo_SqueezeBars_closed_with_monkey.png', meshes('MD_Zoo_SqueezeBars') + mm, (.5, -1, .55), res=(1000, 900), size=4.8, center=(0, 0, 1.6))
    A.render(out / 'MD_Zoo_SqueezeBars_closed_with_monkey_runner_view.png', meshes('MD_Zoo_SqueezeBars') + mm, (0, -1, .12), res=(1000, 900), size=4.8, center=(0, 0, 1.6))
    left.location.x, right.location.x = -1.8, 1.8
    A.render(out / 'MD_Zoo_SqueezeBars_open_with_monkey.png', meshes('MD_Zoo_SqueezeBars') + mm, (.5, -1, .55), res=(1000, 900), size=5.6, center=(0, 0, 1.6))
    # first-person runner view (eye 1.8 m) at 10 m and 5 m before the puddle and the closed squeeze
    A.solo(meshes('MD_Zoo_Puddle'))
    for d in (10, 5):
        A.render_eye(out / f'MD_Zoo_Puddle_runner_view_{d}m.png', (0, -d, 1.8), (0, 2, 0))
    left.location.x, right.location.x = -.45, .45
    A.solo(meshes('MD_Zoo_SqueezeBars'))
    for d in (10, 5):
        A.render_eye(out / f'MD_Zoo_SqueezeBars_closed_runner_view_{d}m.png', (0, -1.5 - d, 1.8), (0, 2, 1.2))
    # lineup: everything along the path with the monkey and police
    rig.animation_data.action = None; N.apply_pose(rig, {}); rig.location = (0, 0, 0)
    xs = {'MD_Zoo_Fence': -6, 'MD_Zoo_Puddle': -1.5, 'MD_Zoo_IronGate': 3.5, 'MD_Zoo_Entrance': 9.5}
    for name, x in xs.items():
        for o in assets[name][0]:
            if o.parent is None: o.location.x = x
    rig.location = (-6, -1.6, 0); refs['Police'].location = (-1.5, -1.8, 0)
    sc.frame_set(60)
    obs = [o for n in xs for o in meshes(n)] + mm + police
    A.solo(obs)
    A.render(out / 'Zoo_compare.png', obs, 'three_quarter', res=(1800, 900), size=19, center=(1.5, 0, 2))
print('ZOO_PASS', tris)
