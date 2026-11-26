"""Zoo chaser lion: model, rig, run/roar/pounce/cage-breakout clips and the lion cage with bursting door.

blender -b --python Tools/AssetPipeline/build_art_lion.py [-- --no-render]
Outputs
- Unity/Assets/External/Staging/Blender/Animals/Lion.fbx, AN_Lion_Run/Roar/Pounce/CageBreakout.fbx (Generic)
- Unity/Assets/External/Staging/Blender/Models/MD_Zoo_LionCage.fbx (door burst take, 60 frames)
- References/Blender/ArtV2_Lion.blend
- review renders in art-review/Lion/
Style: docs/ART_STYLE_GUIDE.md (rounded fused body like the Monkey, big head + mane, short legs).
"""
import sys, math
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent))
import bpy
from mathutils import Vector, Quaternion
import mong_art_common as A
import mong_anim as N

A.reset()
COAT = A.mat('LionCoat', '#C9822F', .78)
MANE = A.mat('LionMane', '#7A3313', .85)
CREAM = A.mat('LionMuzzle', '#F2D49E', .78)
NOSE = A.mat('LionNose', '#3B1A14', .6)
IVORY = A.mat('LionClaw', '#F5EBC7', .55)
EYE = A.mat('LionEyeWhite', '#FAF5DE', .5)
PUPIL = A.mat('LionPupil', '#080502', .4)
MOUTH = A.mat('LionMouth', '#1F0302', .8)
TONGUE = A.mat('LionTongue', '#BA1F21', .6)
IRIS = A.mat('LionIris', '#E0A21C', .45)
# Cage (reuses in-game zoo / prison materials by name, see palette 'unity' links)
IRON = A.mat('ForgedIron', '#333D45', .64, .48)
STEEL = A.mat('PaintedSteel', '#8FA6AB', .66, .35)
BARK = A.mat('ZooLogBark', '#80664A', .78)
CAUTION = A.mat('ZooCaution', '#FFBD14', .7)
SLAB = A.mat('blender_Stone_2', '#CFC2AD', .78)

# ------------------------------------------------------------------ rig
SIDES = [(-1, 'R'), (1, 'L')]

def build_rig():
    arm = bpy.data.armatures.new('LionArmature'); rig = bpy.data.objects.new('Rig', arm)
    bpy.context.scene.collection.objects.link(rig); bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode='EDIT'); eb = arm.edit_bones
    spec = {
        'Root': ((0, 0, 0), (0, 0, .35), None),
        'Spine': ((0, .22, 1.05), (0, -.12, 1.08), 'Root'),
        'Hips': ((0, .22, 1.05), (0, .72, 1.02), 'Spine'),
        'Chest': ((0, -.12, 1.08), (0, -.58, 1.18), 'Spine'),
        'Neck': ((0, -.58, 1.18), (0, -.82, 1.48), 'Chest'),
        'Head': ((0, -.82, 1.48), (0, -1.36, 1.56), 'Neck'),
        'Jaw': ((0, -.98, 1.42), (0, -1.36, 1.36), 'Head'),
    }
    for s, l in SIDES:
        spec.update({
            'FrontUpper.' + l: ((s * .27, -.48, 1.02), (s * .28, -.52, .56), 'Chest'),
            'FrontLower.' + l: ((s * .28, -.52, .56), (s * .28, -.55, .16), 'FrontUpper.' + l),
            'FrontPaw.' + l: ((s * .28, -.55, .16), (s * .28, -.80, .07), 'FrontLower.' + l),
            'HindUpper.' + l: ((s * .27, .62, .98), (s * .28, .50, .56), 'Hips'),
            'HindLower.' + l: ((s * .28, .50, .56), (s * .28, .78, .30), 'HindUpper.' + l),
            'HindFoot.' + l: ((s * .28, .78, .30), (s * .28, .72, .12), 'HindLower.' + l),
            'HindPaw.' + l: ((s * .28, .72, .12), (s * .28, .48, .07), 'HindFoot.' + l),
            'Ear.' + l: ((s * .26, -.92, 1.86), (s * .32, -.92, 2.04), 'Head'),
        })
    for i in range(5):
        a = Vector((0, .74 + i * .2, 1.02 - i * .07 + max(0, i - 2) * .12))
        b = Vector((0, .74 + (i + 1) * .2, 1.02 - (i + 1) * .07 + max(0, i - 1) * .12))
        spec['Tail' + str(i)] = (tuple(a), tuple(b), 'Hips' if i == 0 else 'Tail' + str(i - 1))
    for name, (h, t, p) in spec.items():
        b = eb.new(name); b.head = h; b.tail = t; b.roll = 0
        if p: b.parent = eb[p]
        b.use_connect = False
    bpy.ops.object.mode_set(mode='OBJECT')
    rig.show_in_front = True
    return rig

# ------------------------------------------------------------------ body
fur, mane, cream, details = [], [], [], []

def part(lst, ob, bone):
    A.weight_all(ob, bone); lst.append(ob); return ob

def build_body():
    # Coat: chest / middle / rump, like the Monkey's rounded torso + hips
    part(fur, A.ball('Chest', (0, -.36, 1.08), (.44, .48, .44), COAT), 'Chest')
    part(fur, A.ball('Barrel', (0, .10, 1.06), (.40, .42, .38), COAT), 'Spine')
    part(fur, A.ball('Rump', (0, .56, 1.04), (.40, .36, .38), COAT), 'Hips')
    part(fur, A.ball('Head', (0, -1.0, 1.64), (.40, .36, .38), COAT), 'Head')
    for s, l in SIDES:
        part(fur, A.ball('Shoulder', (s * .25, -.46, .98), (.19, .26, .30), COAT), 'FrontUpper.' + l)
        part(fur, A.limb('Foreleg', (s * .28, -.50, .78), (s * .28, -.53, .44), .17, .20, COAT), 'FrontUpper.' + l)
        part(fur, A.limb('Forearm', (s * .28, -.52, .54), (s * .28, -.55, .14), .14, .16, COAT), 'FrontLower.' + l)
        part(fur, A.ball('FrontPaw', (s * .28, -.66, .10), (.17, .22, .10), COAT), 'FrontPaw.' + l)
        part(fur, A.ball('Thigh', (s * .25, .60, .84), (.20, .32, .32), COAT), 'HindUpper.' + l)
        part(fur, A.limb('Shin', (s * .28, .52, .58), (s * .28, .78, .30), .14, .16, COAT), 'HindLower.' + l)
        part(fur, A.limb('Hock', (s * .28, .78, .30), (s * .28, .72, .12), .12, .14, COAT), 'HindFoot.' + l)
        part(fur, A.ball('HindPaw', (s * .28, .60, .09), (.16, .22, .09), COAT), 'HindPaw.' + l)
        part(fur, A.ball('Ear', (s * .27, -.95, 1.98), (.13, .07, .13), COAT), 'Ear.' + l)
    for i in range(5):
        pb = 'Tail' + str(i)
        part(fur, A.limb('Tail', (0, .74 + i * .2, 1.02 - i * .07 + max(0, i - 2) * .12),
                         (0, .74 + (i + 1) * .2, 1.02 - (i + 1) * .07 + max(0, i - 1) * .12), .055, .055, COAT), pb)
    # Mane: one big disc + lumpy ring for a chunky, readable silhouette (head+mane ~= monkey head size)
    part(mane, A.ball('ManeDisc', (0, -.80, 1.62), (.60, .30, .62), MANE), 'Neck')
    for i in range(14):
        a = 2 * math.pi * i / 14
        r = .56
        part(mane, A.ball('ManeTuft', (math.cos(a) * r, -.84 + .05 * math.sin(a * 3), 1.62 + math.sin(a) * r * 1.02),
                          (.21, .22, .21), MANE), 'Neck' if math.sin(a) < -.2 else 'Head')
    part(mane, A.ball('ManeChin', (0, -1.08, 1.24), (.24, .18, .17), MANE), 'Head')
    part(mane, A.ball('ManeNape', (0, -.55, 1.40), (.36, .32, .36), MANE), 'Neck')
    part(mane, A.ball('TailTuft', (0, 1.74, 1.08), (.12, .16, .13), MANE), 'Tail4')
    # Light face plate, chest bib and belly (same role as the monkey's golden muzzle and belly)
    part(cream, A.ball('Muzzle', (0, -1.30, 1.53), (.27, .19, .17), CREAM), 'Head')
    part(cream, A.ball('Brow plate', (0, -1.20, 1.72), (.24, .12, .14), CREAM), 'Head')
    part(cream, A.ball('Bib', (0, -.66, .92), (.27, .18, .27), CREAM), 'Chest')
    part(cream, A.ball('Belly', (0, .10, .78), (.28, .40, .12), CREAM), 'Spine')
    # Separate details (not fused, like the monkey's eyes and teeth)
    jaw = A.ball('Lower jaw', (0, -1.30, 1.39), (.20, .16, .075), CREAM); part(details, jaw, 'Jaw')
    part(details, A.ball('Mouth', (0, -1.40, 1.44), (.17, .05, .07), MOUTH), 'Jaw')
    part(details, A.ball('Tongue', (0, -1.43, 1.415), (.09, .03, .025), TONGUE), 'Jaw')
    part(details, A.ball('Nose', (0, -1.475, 1.61), (.10, .06, .065), NOSE), 'Head')
    for s, l in SIDES:
        part(details, A.ball('Inner ear', (s * .275, -1.005, 1.97), (.08, .02, .08), CREAM), 'Ear.' + l)
        part(details, A.ball('Eye white', (s * .145, -1.30, 1.75), (.095, .05, .11), EYE), 'Head')
        part(details, A.ball('Iris', (s * .14, -1.345, 1.74), (.055, .012, .07), IRIS), 'Head')
        part(details, A.ball('Pupil', (s * .14, -1.355, 1.74), (.026, .008, .05), PUPIL), 'Head')
        part(details, A.ball('Glint', (s * .14 - .015, -1.362, 1.775), (.012, .005, .016), EYE), 'Head')
        b = A.ball('Brow', (s * .15, -1.30, 1.86), (.11, .035, .03), MANE); b.rotation_euler.y = -s * .32
        part(details, b, 'Head')
        part(details, A.ball('Whisker pad', (s * .10, -1.43, 1.52), (.09, .04, .065), CREAM), 'Head')
        c = A.ball('Upper fang', (s * .085, -1.45, 1.46), (.022, .018, .045), IVORY); part(details, c, 'Head')
        c = A.ball('Lower fang', (s * .085, -1.42, 1.40), (.018, .016, .035), IVORY); part(details, c, 'Jaw')
        for k in range(3):
            part(details, A.ball('Front claw', (s * .28 + (k - 1) * .065, -.86, .07), (.025, .04, .025), IVORY), 'FrontPaw.' + l)
            part(details, A.ball('Front toe', (s * .28 + (k - 1) * .068, -.80, .11), (.055, .06, .055), COAT), 'FrontPaw.' + l)
            part(details, A.ball('Hind toe', (s * .28 + (k - 1) * .062, .40, .09), (.05, .055, .05), COAT), 'HindPaw.' + l)

rig = build_rig()
build_body()
skins = [A.fuse_surface(fur, 'Coat', voxel=.036, decimate=.38, neighbours=8, weight_smooth=6),
         A.fuse_surface(mane, 'Mane', voxel=.04, decimate=.38, neighbours=8, weight_smooth=3),
         A.fuse_surface(cream, 'Cream', voxel=.03, decimate=.42, neighbours=8, weight_smooth=3)]
mesh = A.join(skins + details, 'Lion_SkinnedMesh')
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
mod = mesh.modifiers.new('Lion skin', 'ARMATURE'); mod.object = rig; mesh.parent = rig
TRIS = A.triangles([mesh])
print('LION_TRIS', TRIS)

# ------------------------------------------------------------------ poses
def leg(side, upper, lower, paw, front=True):
    """Leg angles (degrees, + = backwards) for one side."""
    p = 'Front' if front else 'Hind'
    if front:
        return {p + 'Upper.' + side: {'r': (upper, 0, 0)}, p + 'Lower.' + side: {'r': (lower, 0, 0)}, p + 'Paw.' + side: {'r': (paw, 0, 0)}}
    return {p + 'Upper.' + side: {'r': (upper, 0, 0)}, p + 'Lower.' + side: {'r': (lower, 0, 0)},
            'HindFoot.' + side: {'r': (paw * .5, 0, 0)}, p + 'Paw.' + side: {'r': (paw * .5, 0, 0)}}

def merge(*ds):
    out = {}
    for d in ds:
        out = N.add(out, d)
    return out

def gallop(t, intensity=1.0):
    ph = 2 * math.pi * t
    p = {}
    for side, off in (('L', 0.0), ('R', .55)):
        a = ph + off
        fu = -38 * math.cos(a) * intensity
        fl = 70 * max(0, math.sin(a)) * intensity
        fp = 30 * max(0, math.sin(a + .6)) * intensity - 10 * max(0, -math.sin(a))
        b = ph + math.pi + off
        hu = 34 * math.cos(b) * intensity
        hl = -55 * max(0, math.sin(b)) * intensity
        hp = 50 * max(0, math.sin(b + .5)) * intensity
        p = merge(p, leg(side, fu, fl, fp, True), leg(side, hu, hl, hp, False))
    flex = math.sin(ph + math.pi / 2)
    p = merge(p, {
        'Spine': {'r': (5 * math.sin(ph) * intensity, 0, 0), 'l': (0, 0, .08 * math.sin(ph) * intensity)},
        'Hips': {'r': (12 * flex * intensity, 0, 0)},
        'Chest': {'r': (8 * flex * intensity, 0, 0)},
        'Neck': {'r': (-6 * flex * intensity, 0, 0)},
        'Head': {'r': (-4 * math.sin(ph) * intensity, 0, 0)},
        'Jaw': {'r': (10 + 4 * math.sin(2 * ph), 0, 0)},
        'Ear.L': {'r': (10, 0, 0)}, 'Ear.R': {'r': (10, 0, 0)},
    })
    for i in range(5):
        p = merge(p, {'Tail' + str(i): {'r': (-6 + 10 * math.sin(ph - i * .6), 0, 6 * math.sin(ph * .5 - i * .5))}})
    return p

STAND = merge(leg('L', 0, 0, 0), leg('R', 0, 0, 0), leg('L', 0, 0, 0, False), leg('R', 0, 0, 0, False),
              {'Tail0': {'r': (-10, 0, 0)}, 'Tail1': {'r': (6, 0, 0)}, 'Tail2': {'r': (8, 0, 0)}})
CROUCH = merge(  # low, head forward, front legs bent, rump up: about to roar
    leg('L', -20, 75, -35), leg('R', -14, 70, -30), leg('L', 18, -45, 40, False), leg('R', 18, -45, 40, False),
    {'Root': {'l': (0, .10, -.28)}, 'Spine': {'r': (8, 0, 0)}, 'Chest': {'r': (6, 0, 0)}, 'Neck': {'r': (-18, 0, 0)},
     'Head': {'r': (-6, 0, 0)}, 'Ear.L': {'r': (25, 0, -20)}, 'Ear.R': {'r': (25, 0, 20)},
     'Tail0': {'r': (-25, 0, 0)}, 'Tail1': {'r': (10, 0, 0)}, 'Tail2': {'r': (15, 0, 0)}, 'Tail3': {'r': (15, 0, 0)}})
ROAR = merge(  # head thrown up and forward, jaw wide open
    leg('L', -26, 50, -25), leg('R', -22, 48, -22), leg('L', 14, -38, 32, False), leg('R', 14, -38, 32, False),
    {'Root': {'l': (0, .05, -.12)}, 'Spine': {'r': (-4, 0, 0)}, 'Chest': {'r': (-6, 0, 0)}, 'Neck': {'r': (-12, 0, 0)},
     'Head': {'r': (-2, 0, 0)}, 'Jaw': {'r': (40, 0, 0)}, 'Ear.L': {'r': (35, 0, -25)}, 'Ear.R': {'r': (35, 0, 25)},
     'Tail0': {'r': (-45, 0, 0)}, 'Tail1': {'r': (-20, 0, 0)}, 'Tail2': {'r': (-10, 0, 0)}, 'Tail3': {'r': (20, 0, 0)}})
COIL = merge(  # weight back, hind legs folded under: the pose right before the leap
    leg('L', -10, 85, -40), leg('R', -6, 82, -38), leg('L', 40, -85, 70, False), leg('R', 40, -85, 70, False),
    {'Root': {'l': (0, .22, -.40)}, 'Spine': {'r': (-8, 0, 0)}, 'Hips': {'r': (18, 0, 0)}, 'Chest': {'r': (4, 0, 0)},
     'Neck': {'r': (-14, 0, 0)}, 'Head': {'r': (-4, 0, 0)}, 'Jaw': {'r': (14, 0, 0)},
     'Ear.L': {'r': (40, 0, -30)}, 'Ear.R': {'r': (40, 0, 30)},
     'Tail0': {'r': (-30, 0, 0)}, 'Tail1': {'r': (25, 0, 0)}, 'Tail2': {'r': (25, 0, 0)}, 'Tail3': {'r': (20, 0, 0)}})
LAUNCH = merge(  # fully stretched, hind legs pushing back, front paws reaching
    leg('L', -75, 10, -10), leg('R', -70, 15, -10), leg('L', -10, 40, -20, False), leg('R', -5, 42, -20, False),
    {'Root': {'l': (0, -.5, .35)}, 'Spine': {'r': (-10, 0, 0)}, 'Hips': {'r': (-15, 0, 0)}, 'Chest': {'r': (-8, 0, 0)},
     'Neck': {'r': (-6, 0, 0)}, 'Jaw': {'r': (30, 0, 0)}, 'Ear.L': {'r': (45, 0, -10)}, 'Ear.R': {'r': (45, 0, 10)},
     'Tail0': {'r': (-15, 0, 0)}, 'Tail1': {'r': (-10, 0, 0)}})
AIR = merge(  # claws out, front legs high, body arched over the target
    leg('L', -105, -10, -40), leg('R', -100, -5, -40), leg('L', 30, -40, 50, False), leg('R', 34, -42, 50, False),
    {'Root': {'l': (0, -2.0, .85)}, 'Spine': {'r': (-6, 0, 0)}, 'Hips': {'r': (6, 0, 0)}, 'Chest': {'r': (-12, 0, 0)},
     'Neck': {'r': (8, 0, 0)}, 'Head': {'r': (10, 0, 0)}, 'Jaw': {'r': (45, 0, 0)},
     'Ear.L': {'r': (45, 0, -10)}, 'Ear.R': {'r': (45, 0, 10)},
     'Tail0': {'r': (-30, 0, 0)}, 'Tail1': {'r': (-15, 0, 0)}, 'Tail2': {'r': (-10, 0, 0)}})
STRIKE = merge(  # front paws slam down, head dives in to bite
    leg('L', -55, 35, 10), leg('R', -50, 30, 10), leg('L', 25, -60, 45, False), leg('R', 28, -62, 45, False),
    {'Root': {'l': (0, -3.0, -.10)}, 'Spine': {'r': (10, 0, 0)}, 'Hips': {'r': (12, 0, 0)}, 'Chest': {'r': (12, 0, 0)},
     'Neck': {'r': (14, 0, 0)}, 'Head': {'r': (12, 0, 0)}, 'Jaw': {'r': (8, 0, 0)},
     'Tail0': {'r': (-40, 0, 0)}, 'Tail1': {'r': (-20, 0, 0)}, 'Tail2': {'r': (10, 0, 0)}})
LAND = N.add(STAND, {'Root': {'l': (0, -3.2, 0)}, 'Jaw': {'r': (18, 0, 0)}, 'Neck': {'r': (-8, 0, 0)}})

def roar_at(f):
    keys = [(1, STAND), (9, CROUCH), (16, ROAR), (34, ROAR), (42, COIL), (46, COIL)]
    p = N.sample_keys(keys, f)
    if 16 <= f <= 34:  # head shake and mane shudder while roaring
        k = math.sin((f - 16) / 18 * math.pi)
        p = N.add(p, {'Head': {'r': (0, 9 * math.sin(f * 1.7) * k, 7 * math.sin(f * 1.3) * k)},
                      'Chest': {'r': (0, 0, 2 * math.sin(f * 1.1) * k)}})
    if f >= 36:  # tail tip twitch while coiled
        p = N.add(p, {'Tail3': {'r': (0, 0, 18 * math.sin(f * .9))}, 'Tail4': {'r': (0, 0, 24 * math.sin(f * .9 - .6))}})
    return p

def pounce_at(f):
    keys = [(1, COIL), (5, LAUNCH), (12, AIR), (18, STRIKE), (24, LAND), (28, LAND)]
    return N.sample_keys(keys, f)

# Cage breakout (60 frames ~ 2 s): back up, ram the door at f20, leap out, land, start running.
def breakout_at(f):
    if f < 20:
        p = N.sample_keys([(1, N.add(STAND, {'Root': {'l': (0, .6, 0)}})), (8, N.add(CROUCH, {'Root': {'l': (0, .75, 0)}})),
                           (14, N.add(COIL, {'Root': {'l': (0, .85, 0)}})), (20, N.add(LAUNCH, {'Root': {'l': (0, -.95, -.35)}}))], f)
        if f < 14:  # growl shake
            p = N.add(p, {'Head': {'r': (0, 0, 6 * math.sin(f * 1.6))}, 'Jaw': {'r': (20 * math.sin(f / 14 * math.pi), 0, 0)}})
        return p
    if f < 42:
        t = (f - 20) / 22
        base = N.sample_keys([(20, LAUNCH), (30, AIR), (38, STRIKE), (42, N.add(STAND, {'Root': {'l': (0, -4.05, 0)}}))], f)
        y = -.45 - 3.6 * N.ease(t); z = .25 + 1.0 * math.sin(math.pi * min(1, t * 1.15))
        base = N.add(base, {'Root': {'l': (0, 0, 0)}})
        base['Root'] = {'l': (0, y, max(-.15, z - .25))}
        return base
    t = (f - 42) / 18  # land and break into the gallop
    g = gallop((f - 42) / 16, intensity=min(1, .4 + t))
    g = N.add(g, {'Root': {'l': (0, -4.05 - 2.6 * t, 0)}})
    if f < 46:
        g = N.mix(N.add(STRIKE, {'Root': {'l': (0, 3.0 - 4.05, 0)}}), g, (f - 42) / 4)
    return g

clips = {
    'AN_Lion_Run': (17, lambda f: gallop((f - 1) / 16)),
    'AN_Lion_Roar': (46, roar_at),
    'AN_Lion_Pounce': (28, pounce_at),
    'AN_Lion_CageBreakout': (60, breakout_at),
}

# ------------------------------------------------------------------ cage prop (zoo enclosure front)
cage_parts = []
def cp(ob): cage_parts.append(ob); return ob
W, D, H = 2.6, 4.0, 2.7          # inner width (x), depth (y), height
FRONT = -1.25                    # door plane (y); the lion starts at y = +.6 inside
cp(A.box('Cage floor slab', (0, .35, .08), (W + .5, D + .4, .16), SLAB, .12))
for x in (-W / 2 - .05, W / 2 + .05):
    for y in (FRONT, FRONT + D):
        cp(A.box('Cage post', (x, y, H / 2 + .1), (.2, .2, H), STEEL, .14))
for y in (FRONT, FRONT + D):
    cp(A.box('Cage top beam', (0, y, H + .15), (W + .32, .22, .22), STEEL, .14))
for x in (-W / 2 - .05, W / 2 + .05):
    cp(A.box('Cage side beam', (x, FRONT + D / 2, H + .15), (.22, D + .2, .22), STEEL, .14))
cp(A.box('Cage roof', (0, FRONT + D / 2, H + .32), (W + .6, D + .5, .14), BARK, .14))
for x in (-W / 2 - .05, W / 2 + .05):
    for k in range(9):
        cp(A.cyl('Side bar', (x, FRONT + .35 + k * .42, H / 2 + .12), .045, H - .1, IRON, 10))
for k in range(9):
    cp(A.cyl('Back bar', (-W / 2 + .2 + k * (W - .4) / 8, FRONT + D, H / 2 + .12), .045, H - .1, IRON, 10))
cp(A.box('Warning plate', (W / 2 - .2, FRONT - .14, H + .15), (.7, .05, .3), CAUTION, .2))
cage = A.join(cage_parts, 'MD_Zoo_LionCage')
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
door_parts = []
for k in range(7):
    door_parts.append(A.cyl('Door bar', (-W / 2 + .3 + k * (W - .6) / 6, FRONT, H / 2 + .12), .05, H - .2, IRON, 10))
for z in (.35, H - .1):
    door_parts.append(A.box('Door rail', (0, FRONT, z), (W - .3, .12, .14), STEEL, .16))
door_parts.append(A.box('Door lock', (0, FRONT - .09, H * .5), (.26, .09, .32), A.mat('LockBrass', '#F0940F', .45, .4), .2))
door = A.join(door_parts, 'Door')
A.set_origin(door, (0, FRONT, .3))   # pivot at the bottom rail: tips forward and slides when rammed
door.parent = cage
door.animation_data_create()
door_act = bpy.data.actions.new('AN_LionCage_DoorBurst'); door.animation_data.action = door_act
N.key_object(door, 1, loc=(0, FRONT, .3), rot=(0, 0, 0))
N.key_object(door, 19, loc=(0, FRONT, .3), rot=(0, 0, 0))
N.key_object(door, 20, loc=(0, FRONT - .05, .3), rot=(-4, 0, 1))        # impact shudder
N.key_object(door, 24, loc=(.15, FRONT - 1.1, .9), rot=(-40, 6, 10))    # torn off, flying
N.key_object(door, 30, loc=(.35, FRONT - 2.4, .55), rot=(-78, 10, 18))
N.key_object(door, 34, loc=(.45, FRONT - 3.0, .12), rot=(-90, 8, 22))  # lands flat
N.key_object(door, 38, loc=(.5, FRONT - 3.2, .16), rot=(-86, 8, 22))   # bounce
N.key_object(door, 42, loc=(.5, FRONT - 3.25, .12), rot=(-90, 8, 22))
N.key_object(door, 60, loc=(.5, FRONT - 3.25, .12), rot=(-90, 8, 22))
door_act.use_fake_user = True
CAGE_TRIS = A.triangles([cage, door])
print('CAGE_TRIS', CAGE_TRIS)

# ------------------------------------------------------------------ export
A.write_palette('Lion')
A.export_fbx(A.OUT_ANIMALS / 'Lion.fbx', [rig, mesh])
A.register_asset(A.OUT_ANIMALS / 'Lion.fbx', 'generic_model', 'Lion')
actions = {}
for name, (frames, fn) in clips.items():
    actions[name] = N.bake(rig, name, frames, fn)
    A.export_fbx(A.OUT_ANIMALS / (name + '.fbx'), [rig], animated=True)
    A.register_asset(A.OUT_ANIMALS / (name + '.fbx'), 'generic_clip', 'Lion', loop=name == 'AN_Lion_Run', avatar_from='Animals/Lion.fbx', frames=frames)
rig.animation_data.action = None
N.apply_pose(rig, {})
bpy.context.scene.frame_start, bpy.context.scene.frame_end = 1, 60
A.export_fbx(A.OUT_MODELS / 'MD_Zoo_LionCage.fbx', [cage, door], animated=True, armature=False)
A.register_asset(A.OUT_MODELS / 'MD_Zoo_LionCage.fbx', 'prop_anim', 'Lion', frames=60)

# keep the cage beside the lion in the .blend; preview breakout via NLA (rig + door)
col = bpy.data.collections.new('Lion'); bpy.context.scene.collection.children.link(col)
for o in (rig, mesh):
    for c in list(o.users_collection): c.objects.unlink(o)
    col.objects.link(o)
col2 = bpy.data.collections.new('LionCage'); bpy.context.scene.collection.children.link(col2)
for o in (cage, door):
    for c in list(o.users_collection): c.objects.unlink(o)
    col2.objects.link(o)
rig.animation_data.action = actions['AN_Lion_Run']
A.save_blend('ArtV2_Lion')

# ------------------------------------------------------------------ review renders
if '--no-render' not in A.args():
    out = A.REVIEW / 'Lion'
    A.studio()
    cage.hide_render = True; door.hide_render = True
    rig.animation_data.action = None; N.apply_pose(rig, STAND)
    A.render_turn(out, 'Lion_model', [mesh], res=(900, 900), size=3.4, center=(0, -.1, 1.05))
    for name, frames in (('AN_Lion_Run', [1, 5, 9, 13]), ('AN_Lion_Roar', [1, 12, 24, 44]), ('AN_Lion_Pounce', [1, 6, 12, 20])):
        rig.animation_data.action = actions[name]
        bpy.context.scene.frame_start, bpy.context.scene.frame_end = 1, clips[name][0]
        A.render_frames(out, name, [mesh], frames, 'side' if name != 'AN_Lion_Roar' else 'three_quarter',
                        size=6.6 if name == 'AN_Lion_Pounce' else 3.6, center=(0, -1.6, 1.3) if name == 'AN_Lion_Pounce' else (0, -.2, 1.05),
                        gif_frames=list(range(1, clips[name][0] + 1, 1 if name == 'AN_Lion_Run' else 2)))
    cage.hide_render = False; door.hide_render = False
    rig.animation_data.action = actions['AN_Lion_CageBreakout']
    door.animation_data.action = door_act
    bpy.context.scene.frame_start, bpy.context.scene.frame_end = 1, 60
    A.render_frames(out, 'AN_Lion_CageBreakout', [mesh, cage, door], [1, 14, 22, 34], (0.9, -1, .35), res=(800, 600),
                    size=9.5, center=(0, -2.2, 1.2), gif_frames=list(range(1, 61, 2)))
    A.render_turn(out, 'MD_Zoo_LionCage', [cage, door], views=('front', 'three_quarter'), size=5.0, center=(0, .3, 1.4))
    # comparison with the existing Monkey and Police, same light and camera
    cage.hide_render = True; door.hide_render = True
    rig.animation_data.action = None; N.apply_pose(rig, STAND); rig.location.x = 0
    refs = A.append_reference_characters(offset_x=(-1.9, 2.0))
    allm = [mesh] + [o for c in ('Monkey', 'Police') for o in bpy.data.collections[c].objects if o.type == 'MESH']
    A.render(out / 'Lion_compare_three_quarter.png', allm, 'three_quarter', res=(1400, 900), size=6.0, center=(0, 0, 1.1))
    A.render(out / 'Lion_compare_front.png', allm, 'front', res=(1400, 900), size=6.0, center=(0, 0, 1.1))
    A.render(out / 'Lion_compare_side.png', allm, 'side', res=(1400, 900), size=6.0, center=(0, 0, 1.1))
print('LION_PASS tris=%d cage_tris=%d' % (TRIS, CAGE_TRIS))
