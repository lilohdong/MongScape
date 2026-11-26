"""Mine broken wall (stuck monkey) + prison searchlight + cell door with lock.

blender -b --python Tools/AssetPipeline/build_art_prison_mine.py [-- --no-render]
Outputs (Staging/Blender/Models):
- MD_Mine_BrokenWall_L.fbx / _R.fbx : lane-1 left wall / lane-3 right wall section with a broken hole.
  Front faces -Y in Blender (Unity +Z); the child empty 'MonkeyAnchor' is where the monkey root goes
  (monkey faces the same way as the wall front). _L pairs with AN_Monkey_WallStuck_Wave_R (the hand nearer
  the approaching runner), _R with AN_Monkey_WallStuck_Wave_L.
- MD_Prison_Searchlight.fbx : Base / Pan (yaw pivot, sweep take) / Head / Beam (light cone, transparent).
- MD_Prison_CellDoor.fbx : frame + concrete jambs, Door (hinge pivot) + Lock / Shackle, open take 40f.
Style: ART_STYLE_GUIDE.md - bevelled flat-shaded props, in-game materials reused by name.
"""
import sys, math, random
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent))
import bpy
from mathutils import Vector
import mong_art_common as A
import mong_anim as N

A.reset()
STONE = [A.mat('blender_Stone_%d' % i, h, .78) for i, h in enumerate(['#B8AD9C', '#C3B8A4', '#CFC2AD', '#DACCB6', '#E6D6BF', '#F1E0C8'])]
DEPTH = A.mat('CavernDepth', '#0E171F', .9)
TIMBER = A.mat('Timber', '#C7B08F', .78)
LANTERN = A.mat('LanternGlass', '#FF9C30', .65, emit='#FF9C30', strength=3)
IRON = A.mat('ForgedIron', '#333D45', .64, .48)
STEEL = A.mat('PaintedSteel', '#8FA6AB', .66, .35)
CONCRETE = A.mat('PrisonConcrete', '#B8D1EB', .78)
BRASS = A.mat('LockBrass', '#F0940F', .45, .4)
CHAR = A.mat('Charcoal', '#060709', .67)
LENS = A.mat('SearchlightLens', '#FFF4D6', .3, emit='#FFE7A3', strength=6)
BEAM = A.mat('SearchlightBeam', '#FFE7A3', .9, emit='#FFE7A3', strength=1.2, alpha=.1)   # faint: whoever is caught stays readable
SPOT = A.mat('SearchlightSpot', '#FFE7A3', .9, emit='#FFE7A3', strength=2.0, alpha=.45)   # bright floor ellipse = danger zone
RED = A.mat('SecurityRed', '#CC160B', .72, .1)

def collect(name, objs, origin=(0, 0, 0)):
    ob = A.join(objs, name)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    A.set_origin(ob, origin)
    return ob

def to_collection(name, objs):
    col = bpy.data.collections.new(name); bpy.context.scene.collection.children.link(col)
    for o in objs:
        for c in list(o.users_collection): c.objects.unlink(o)
        col.objects.link(o)
    return col

# ================================================================== mine broken wall
HOLE_C = Vector((0, 0, 1.45)); HOLE_RX, HOLE_RZ = .78, .72

def in_hole(x, z, pad=0.0):
    return ((x - HOLE_C.x) / (HOLE_RX + pad)) ** 2 + ((z - HOLE_C.z) / (HOLE_RZ + pad)) ** 2 < 1

def broken_wall(mirror):
    random.seed(4107 if mirror else 723)
    m = -1 if mirror else 1
    parts = []
    parts.append(A.box('Recessed rock bed', (0, .55, 1.8), (3.4, .35, 3.6), STONE[0], .06))
    for row in range(5):
        for col in range(5):
            x = -1.35 + col * .68 + (row % 2) * .2 + random.uniform(-.08, .08)
            z = .34 + row * .72 + random.uniform(-.1, .1)
            if in_hole(x, z, .18): continue
            ob = A.box('FracturedSlab', (x, .12 + random.uniform(-.06, .04), z),
                       (random.uniform(.55, .78), random.uniform(.5, .7), random.uniform(.6, .8)), random.choice(STONE), .2, segments=1)
            for v in ob.data.vertices:
                v.co += Vector((random.uniform(-.05, .05), random.uniform(-.04, .04), random.uniform(-.05, .05)))
            ob.rotation_euler = (random.uniform(-.12, .12), random.uniform(-.12, .12), random.uniform(-.15, .15))
            parts.append(ob)
    # broken rim: chunks pushed outwards around the hole, bigger on the lower edge
    for i in range(13):
        a = 2 * math.pi * i / 13 + .2
        if 1.75 < a < 3.5: continue   # keep the waving hand's path (monkey's right, -X) clear of rim rocks
        x = HOLE_C.x + math.cos(a) * (HOLE_RX + .12); z = HOLE_C.z + math.sin(a) * (HOLE_RZ + .1)
        s = .30 + .12 * random.random() + (.1 if math.sin(a) < -.3 else 0)
        ob = A.box('BrokenRimChunk', (x, -.08 - .12 * random.random(), z), (s, s * .9, s * .8), random.choice(STONE[2:]), .22, segments=1)
        ob.rotation_euler = (random.uniform(-.5, .5), random.uniform(-.6, .6), a)
        parts.append(ob)
    # dark inside of the hole and cracks running out of it (CavernDepth = mine darkness)
    parts.append(A.ball('Hole darkness', (HOLE_C.x, .42, HOLE_C.z), (HOLE_RX * .98, .18, HOLE_RZ * .98), DEPTH, 12, 6, smooth=False))
    for a in (.35, 1.2, 2.3, 3.7, 5.2, 5.9):
        L = random.uniform(.5, .9)
        p0 = Vector((HOLE_C.x + math.cos(a) * HOLE_RX, -.17, HOLE_C.z + math.sin(a) * HOLE_RZ))
        ob = A.box('Crack', p0 + Vector((math.cos(a) * L / 2, 0, math.sin(a) * L / 2)), (L, .04, .045), DEPTH, 0)
        ob.rotation_euler = (0, -a, 0); parts.append(ob)
    # rubble in front at the base (also hides where the monkey's waist meets the wall)
    for i in range(4):
        parts.append(A.box('Rubble', (random.uniform(-1.1, 1.1), random.uniform(-.55, -.15), .1 + random.uniform(0, .08)),
                           (random.uniform(.18, .34), random.uniform(.16, .3), random.uniform(.14, .24)), random.choice(STONE), .25, segments=1,
                           rot=(random.uniform(-.4, .4), random.uniform(-.4, .4), random.uniform(0, 3))))
    # rubble mound under the hole: hides the monkey's legs now that it sits forward (anchor y .02)
    for i in range(5):
        x = -.72 + i * .36 + random.uniform(-.05, .05)
        parts.append(A.box('Mound chunk', (x, -.42 + random.uniform(-.05, .05), .38 + .12 * math.sin(i * 1.7) + random.uniform(0, .1)),
                           (.42, .38, .52 + random.uniform(0, .18)), random.choice(STONE[1:]), .22, segments=1,
                           rot=(random.uniform(-.25, .25), random.uniform(-.3, .3), random.uniform(-.4, .4))))
    # snapped timber brace and a lantern so the spot reads from 2 s away
    b = A.box('Snapped brace', (m * 1.05, -.12, 2.6), (.22, .2, 1.3), TIMBER, .12); b.rotation_euler.y = m * .55; parts.append(b)
    b = A.box('Brace stub', (m * -1.25, -.1, .7), (.22, .2, 1.4), TIMBER, .12); parts.append(b)
    parts.append(A.box('Lantern bracket', (m * -1.0, -.18, 2.75), (.08, .3, .08), IRON, .2))
    parts.append(A.box('Lantern frame', (m * -1.0, -.34, 2.6), (.2, .2, .26), IRON, .18))
    parts.append(A.box('Lantern glass', (m * -1.0, -.34, 2.6), (.15, .22, .2), LANTERN, .2))
    wall = collect('MD_Mine_BrokenWall_' + ('R' if mirror else 'L'), parts)
    if mirror:  # mirror the whole crack pattern for the other side
        wall.scale.x = -1
        bpy.ops.object.select_all(action='DESELECT'); wall.select_set(True); bpy.context.view_layer.objects.active = wall
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT'); bpy.ops.mesh.flip_normals()
        bpy.ops.object.mode_set(mode='OBJECT')
    anchor = bpy.data.objects.new('MonkeyAnchor', None); bpy.context.scene.collection.objects.link(anchor)
    anchor.empty_display_type = 'ARROWS'; anchor.location = (0, .02, 0); anchor.parent = wall   # shoulders and waving arm in front of the pale stone
    return wall, anchor

wall_L, anchor_L = broken_wall(False)
wall_R, anchor_R = broken_wall(True)
wall_R.location.x = 5

# ================================================================== searchlight
sl = []
sl.append(A.box('Wall plate', (0, .1, 0), (.7, .12, .7), STEEL, .15))
sl.append(A.box('Arm', (0, -.25, 0), (.22, .6, .22), STEEL, .15))
sl.append(A.cyl('Yoke bearing', (0, -.55, -.05), .2, .22, IRON, 16, bevel=.03))
base = collect('Base', sl)
pan_parts = [A.box('Yoke', (0, -.55, -.42), (.9, .16, .14), IRON, .2),
             A.cyl('Pan axle', (0, -.55, -.27), .09, .26, IRON, 12)]   # joins the bearing to the yoke
for s in (-1, 1):
    pan_parts.append(A.box('Yoke side', (s * .42, -.55, -.7), (.1, .16, .6), IRON, .2))
pan = collect('Pan', pan_parts, origin=(0, -.55, -.05))   # yaw axis through the bearing
head_parts = [A.cyl('Lamp drum', (0, -.55, -.8), .36, .7, STEEL, 20, rot=(math.radians(90), 0, 0), bevel=.04),
              A.cyl('Lamp rim', (0, -.92, -.8), .4, .08, IRON, 20, rot=(math.radians(90), 0, 0), bevel=.02),
              A.cyl('Lamp back', (0, -.17, -.8), .26, .12, IRON, 16, rot=(math.radians(90), 0, 0), bevel=.02),
              A.box('Warning band', (0, -.55, -.43), (.12, .5, .04), RED, .2)]
lens = A.cyl('Lens', (0, -.955, -.8), .32, .02, LENS, 20, rot=(math.radians(90), 0, 0))
head = collect('Head', head_parts + [lens], origin=(0, -.55, -.8))   # tilt axis
# light cone: 9 m long, 1.9 m radius at the far end, open-ended, tip at the lens
MOUNT = 4.2; TILT = 32
REACH = (MOUNT - .8) / math.sin(math.radians(TILT))           # lens to floor along the beam
bpy.ops.mesh.primitive_cone_add(vertices=20, radius1=1.3, radius2=.3, depth=REACH, end_fill_type='NOTHING', location=(0, -.98 - REACH / 2, -.8),
                                rotation=(math.radians(-90), 0, 0))   # wide end away from the lamp
beam = bpy.context.object; beam.name = 'Beam'; beam.data.materials.append(BEAM)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True); A.set_origin(beam, (0, -.55, -.8))
for child, parent in ((pan, base), (head, pan), (beam, head)):   # keep world placement when parenting
    child.parent = parent; child.matrix_parent_inverse = parent.matrix_world.inverted()
head.rotation_euler.x = math.radians(TILT)      # aims down at the floor
base.location = (0, 0, MOUNT)                    # Base origin goes 4.2 m above the floor (the floor spot assumes it)
# floor spot: flat ellipse where the beam meets the floor, child of Pan so it sweeps with the light
dist = (MOUNT - .8) / math.tan(math.radians(TILT))
bpy.ops.mesh.primitive_circle_add(vertices=24, radius=1, fill_type='NGON', location=(0, -.55 - dist - .3, .03))
spot = bpy.context.object; spot.name = 'FloorSpot'; spot.scale = (1.35, 1.35 / math.sin(math.radians(TILT)) * .8, 1)
spot.data.materials.append(SPOT); bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
spot.parent = pan; spot.matrix_parent_inverse = pan.matrix_world.inverted()
searchlight = [base, pan, head, beam, spot]
pan.animation_data_create(); sweep = bpy.data.actions.new('AN_Prison_Searchlight_Sweep'); pan.animation_data.action = sweep
for f, yaw in ((1, -38), (61, 38), (121, -38)):   # 4 s loop, eased at the ends (slow at the turn)
    N.key_object(pan, f, rot=(0, 0, yaw))
for fc in N.fcurves(sweep):
    for k in fc.keyframe_points: k.interpolation = 'BEZIER'
SEARCH_TRIS = A.triangles(searchlight)

# ================================================================== cell door + lock
cd = []
W, H = 1.5, 2.6
for s in (-1, 1):
    cd.append(A.box('Concrete jamb', (s * (W / 2 + .55), 0, H / 2 + .2), (.9, .5, H + .4), CONCRETE, .08))
    cd.append(A.box('Door post', (s * (W / 2 + .06), -.05, H / 2), (.14, .22, H), STEEL, .15))
cd.append(A.box('Concrete lintel', (0, 0, H + .3), (W + 2.0, .5, .6), CONCRETE, .08))
cd.append(A.box('Door header', (0, -.05, H + .03), (W + .3, .22, .14), STEEL, .15))
cd.append(A.box('Floor sill', (0, -.05, .03), (W + .3, .3, .06), IRON, .2))
cd.append(A.box('Cell number plate', (W / 2 + .55, -.27, H - .25), (.5, .04, .34), A.mat('SignBoard', '#2A1D14', .8), .2))
cd.append(A.text_mesh('Cell number', '07', (W / 2 + .55, -.295, H - .25), .26, A.mat('SignInk', '#FFD36B', .6), depth=.015))
frame = collect('MD_Prison_CellDoor', cd)
dp = []
for k in range(5):
    dp.append(A.cyl('Door bar', (-W / 2 + .2 + k * (W - .4) / 4, -.05, H / 2), .04, H - .2, IRON, 10))
for z in (.18, 1.25, H - .14):
    dp.append(A.box('Door rail', (0, -.05, z), (W - .1, .1, .12), STEEL, .18))
dp.append(A.box('Lock plate', (W / 2 - .22, -.12, 1.22), (.3, .05, .45), IRON, .2))
door = collect('Door', dp, origin=(-W / 2 + .02, -.05, 0))     # hinge on the left post
lock = collect('Lock', [A.box('Padlock body', (W / 2 - .22, -.23, 1.03), (.3, .13, .3), BRASS, .25),   # 1.5x: readable while running
                        A.box('Keyhole', (W / 2 - .22, -.3, 1.0), (.05, .012, .1), CHAR, 0)], origin=(W / 2 - .22, -.23, 1.18))
bpy.ops.mesh.primitive_torus_add(major_radius=.1, minor_radius=.027, major_segments=16, minor_segments=6,
                                 location=(W / 2 - .22, -.23, 1.21), rotation=(math.radians(90), 0, 0))
shackle = bpy.context.object; shackle.name = 'Shackle'; shackle.data.materials.append(IRON)
door.parent = frame; door.matrix_parent_inverse = frame.matrix_world.inverted()
lock.parent = door; shackle.parent = lock
lock.matrix_parent_inverse = door.matrix_world.inverted(); shackle.matrix_parent_inverse = lock.matrix_world.inverted()
door_obs = [frame, door, lock, shackle]
for o, act in ((door, 'AN_Prison_CellDoor_Open'),):
    pass
# one take drives door, lock and shackle (frame 1 closed .. 40 open)
for o in (door, lock, shackle): o.animation_data_create()
door.animation_data.action = bpy.data.actions.new('Door_Open')
lock.animation_data.action = bpy.data.actions.new('Lock_Open')
shackle.animation_data.action = bpy.data.actions.new('Shackle_Open')
sh0 = shackle.location.copy(); lk0 = lock.location.copy()
N.key_object(shackle, 1, loc=sh0); N.key_object(shackle, 6, loc=sh0)
N.key_object(shackle, 9, loc=sh0 + Vector((0, 0, .09)))                    # pops open
N.key_object(lock, 1, loc=lk0, rot=(0, 0, 0)); N.key_object(lock, 9, loc=lk0, rot=(0, 0, 0))
N.key_object(lock, 13, loc=lk0 + Vector((0, -.03, -.02)), rot=(0, 25, 0))  # drops and dangles
N.key_object(lock, 17, loc=lk0 + Vector((0, -.03, -.02)), rot=(0, -12, 0))
N.key_object(lock, 21, loc=lk0 + Vector((0, -.03, -.02)), rot=(0, 5, 0))
N.key_object(lock, 40, loc=lk0 + Vector((0, -.03, -.02)), rot=(0, 0, 0))
d0 = door.rotation_euler.copy()
N.key_object(door, 1, rot=(0, 0, 0)); N.key_object(door, 11, rot=(0, 0, 0))
N.key_object(door, 14, rot=(0, 0, -8))                                      # nudge
N.key_object(door, 30, rot=(0, 0, -108))                                    # swings out (towards -Y, the corridor)
N.key_object(door, 35, rot=(0, 0, -98)); N.key_object(door, 40, rot=(0, 0, -100))
DOOR_TRIS = A.triangles(door_obs)
frame.location.x = 10

WALL_TRIS = A.triangles([wall_L])
print('TRIS wall=%d searchlight=%d celldoor=%d' % (WALL_TRIS, SEARCH_TRIS, DOOR_TRIS))

# ================================================================== export
A.write_palette('PrisonMine')
sc = bpy.context.scene
for w, a in ((wall_L, anchor_L), (wall_R, anchor_R)):
    loc = w.location.copy(); w.location = (0, 0, 0)
    A.export_fbx(A.OUT_MODELS / (w.name + '.fbx'), [w, a], armature=False)
    A.register_asset(A.OUT_MODELS / (w.name + '.fbx'), 'prop', 'PrisonMine')
    w.location = loc
base.location = (0, 0, 0)
sc.frame_start, sc.frame_end = 1, 121
A.export_fbx(A.OUT_MODELS / 'MD_Prison_Searchlight.fbx', searchlight, animated=True, armature=False)
A.register_asset(A.OUT_MODELS / 'MD_Prison_Searchlight.fbx', 'prop_anim', 'PrisonMine', loop=True, frames=121)
base.location = (0, 0, 4.2)
frame.location.x = 0
sc.frame_start, sc.frame_end = 1, 40
A.export_fbx(A.OUT_MODELS / 'MD_Prison_CellDoor.fbx', door_obs, animated=True, armature=False)
A.register_asset(A.OUT_MODELS / 'MD_Prison_CellDoor.fbx', 'prop_anim', 'PrisonMine', frames=40)
to_collection('Mine broken wall L', [wall_L, anchor_L]); to_collection('Mine broken wall R', [wall_R, anchor_R])
to_collection('Prison searchlight', searchlight); to_collection('Prison cell door', door_obs)
A.save_blend('ArtV2_PrisonMine')

# ================================================================== review renders
if '--no-render' not in A.args():
    A.studio()
    monkey_rig = A.load_monkey_rig()
    mm = [o for o in bpy.data.collections['Monkey'].objects if o.type == 'MESH']
    refs = A.append_reference_characters(offset_x=(99, 2.6))
    police = [o for o in bpy.data.collections['Police'].objects if o.type == 'MESH']
    src = A.BLEND_DIR / 'ArtV2_MonkeyMotions.blend'
    if src.exists():
        with bpy.data.libraries.load(str(src), link=False) as (f, t):
            t.actions = [a for a in f.actions if a.startswith('AN_Monkey_WallStuck_Wave')]
    monkey_rig.animation_data_create()
    def wave(name, frame=5):
        if name in bpy.data.actions:
            monkey_rig.animation_data.action = bpy.data.actions[name]; sc.frame_set(frame)
    # ---- mine wall
    out = A.REVIEW / 'MineBrokenWall'
    wall_L.location = (0, 0, 0); wall_R.location = (0, 0, 0)
    A.solo([wall_L]); A.render_turn(out, 'MD_Mine_BrokenWall_L', [wall_L], res=(900, 900), size=4.2, center=(0, 0, 1.6))
    monkey_rig.location = anchor_L.matrix_world.translation; wave('AN_Monkey_WallStuck_Wave_R')
    A.solo([wall_L] + mm)
    A.render_turn(out, 'MD_Mine_BrokenWall_L_with_monkey', [wall_L] + mm, views=('front', 'three_quarter', 'side'), size=4.2, center=(0, 0, 1.6))
    A.render(out / 'MD_Mine_BrokenWall_L_runner_view.png', [wall_L] + mm, (.55, -1, .15), size=4.2, center=(0, 0, 1.6))
    wave('AN_Monkey_WallStuck_Wave_L'); monkey_rig.location = anchor_R.matrix_world.translation
    A.solo([wall_R] + mm)
    A.render(out / 'MD_Mine_BrokenWall_R_with_monkey_front.png', [wall_R] + mm, 'front', size=4.2, center=(0, 0, 1.6))
    A.render(out / 'MD_Mine_BrokenWall_R_runner_view.png', [wall_R] + mm, (-.55, -1, .15), size=4.2, center=(0, 0, 1.6))
    wall_L.location.x = -4.2; monkey_rig.location.x = 0
    refs['Police'].location = (2.6, -.8, 0)
    A.solo([wall_L, wall_R] + mm + police)
    A.render(out / 'MD_Mine_BrokenWall_compare.png', [wall_L, wall_R] + mm + police, 'three_quarter', res=(1600, 900), size=9.5, center=(-.6, 0, 1.6))
    # ---- searchlight
    out = A.REVIEW / 'PrisonSearchlight'
    base.location = (0, 0, 4.2)
    pan.animation_data.action = sweep; sc.frame_start, sc.frame_end = 1, 121; sc.frame_set(61)
    A.solo([base, pan, head])
    A.render_turn(out, 'MD_Prison_Searchlight', [base, pan, head], res=(900, 900), size=2.4, center=(0, -.5, 3.5))
    A.solo(searchlight)
    A.render_frames(out, 'MD_Prison_Searchlight_sweep', searchlight, [1, 31, 61, 91], 'top', res=(800, 800), size=13, center=(0, -5, 2),
                    gif_frames=list(range(1, 122, 4)))
    sc.frame_set(31)   # sweep centre: the monkey stands caught inside the floor spot
    refs['Police'].location = (-2.2, -7.4, 0); monkey_rig.animation_data.action = None; N.apply_pose(monkey_rig, {})
    monkey_rig.location = (0, -6.1, 0)
    A.solo(searchlight + police + mm)
    A.render(out / 'MD_Prison_Searchlight_beam_three_quarter.png', searchlight + police + mm, 'three_quarter', res=(1200, 900), size=11, center=(0, -4.5, 2.2))
    A.render(out / 'MD_Prison_Searchlight_compare.png', searchlight + police + mm, 'side', res=(1400, 900), size=10, center=(0, -4, 2.3))
    A.render(out / 'MD_Prison_Searchlight_runner_view.png', searchlight + police + mm, (.35, -1, .3), res=(1400, 900), size=9, center=(0, -5, 1.8))
    # ---- cell door
    out = A.REVIEW / 'PrisonCellDoor'
    sc.frame_start, sc.frame_end = 1, 40; sc.frame_set(1)
    A.solo(door_obs)
    A.render_turn(out, 'MD_Prison_CellDoor', door_obs, res=(900, 900), size=4.4, center=(0, 0, 1.5))
    A.render_frames(out, 'MD_Prison_CellDoor_open', door_obs, [1, 9, 20, 40], 'three_quarter', res=(700, 700), size=4.6, center=(0, -.4, 1.5),
                    gif_frames=list(range(1, 41)))
    refs['Police'].location = (-2.6, -.8, 0); monkey_rig.location = (2.6, -.8, 0); sc.frame_set(40)
    A.solo(door_obs + police + mm)
    A.render(out / 'MD_Prison_CellDoor_compare.png', door_obs + police + mm, 'three_quarter', res=(1400, 900), size=7.5, center=(0, 0, 1.4))
print('PRISON_MINE_PASS wall=%d searchlight=%d celldoor=%d' % (WALL_TRIS, SEARCH_TRIS, DOOR_TRIS))
