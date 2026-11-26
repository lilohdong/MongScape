"""Tutorial demo motions for the monkey shown in the tutorial window (existing humanoid rig).

blender -b --python Tools/AssetPipeline/build_art_tutorial_demo.py [-- --no-render]
Every clip starts and ends on the same pose so it loops in the demo window, and the body height,
travel and tilt are keyed on Hips (Unity bakes root Y/XZ/rotation into the pose), so the clip alone
shows the exact move without code moving the model (ART_STYLE_GUIDE 6). 30 fps.
- AN_Monkey_Demo_RunInPlace     21f  jog in place, knees ~50 deg
- AN_Monkey_Demo_HalfSquat      45f  half squat (hips -0.34 m, knee ~100 deg) and back up
- AN_Monkey_Demo_Jump           37f  crouch, real take-off (feet 0.5 m off the floor), land
- AN_Monkey_Demo_SideStep_L/_R  31f  one big step sideways (0.6 m) and back
- AN_Monkey_Demo_HighKnee       21f  knees to hip height, alternating
- AN_Monkey_Demo_ArmReach_L/_R  31f  one arm straight out to the side at shoulder height, shoulders level
- AN_Monkey_Demo_HandsUp        31f  both arms up (hands-up) and down
- AN_Monkey_Demo_CartLean_L/_R  37f  seated in the cart, lean the body to one side and back
_L = the monkey's left (+X). The tutorial window mirrors like the camera image; pick the side there.
"""
import sys, math
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent))
import bpy
import mong_art_common as A
import mong_anim as N
from mong_monkey_poses import arm, leg, body, merge, mirror, STAND

A.reset()
rig = A.load_monkey_rig()
mesh = next(o for o in bpy.data.collections['Monkey'].objects if o.type == 'MESH')
rig.location = (0, 0, 0)
READY = merge(arm('L', 78, 8, 25), arm('R', 78, 8, 25), leg('L', 0, 6), leg('R', 0, 6), body(spine=(3, 0, 0)))

def flat_foot(lift, knee):
    """Ankle angle that keeps the sole on the floor for a given thigh lift / knee flex."""
    return lift - knee

def jog(t, knee_lift=50, bob=.05):
    w = math.sin(2 * math.pi * t); p = {}
    for side, s in (('L', 1), ('R', -1)):
        up = max(0, w * s)
        lift = knee_lift * up; knee = 10 + (knee_lift + 30) * up
        p = merge(p, leg(side, lift, knee, -10 * up), arm(side, 85, -s * w * 40, 75))
    return merge(p, body(hips=(0, 0, bob * abs(math.sin(2 * math.pi * t))), spine=(6, 0, 0), tail=10 * w))

def run_in_place(f):
    return jog((f - 1) / 20)

def high_knee(f):
    return jog((f - 1) / 20, knee_lift=95, bob=.07)

SQUAT = merge(arm('L', -18, 85, 10), arm('R', -18, 85, 10),            # arms forward for balance
              leg('L', 72, 102, flat_foot(72, 102)), leg('R', 72, 102, flat_foot(72, 102)),
              body(hips=(0, .12, -.34), spine=(28, 0, 0), chest=(4, 0, 0), neck=(-22, 0, 0), head=(-8, 0, 0), tail=-15))
def half_squat(f):
    return N.sample_keys([(1, READY), (12, SQUAT), (28, SQUAT), (40, READY), (45, READY)], f)

CROUCH = merge(arm('L', 70, -40, 30), arm('R', 70, -40, 30), leg('L', 45, 70, flat_foot(45, 70)), leg('R', 45, 70, flat_foot(45, 70)),
               body(hips=(0, .06, -.2), spine=(22, 0, 0), neck=(-12, 0, 0)))
TAKEOFF = merge(arm('L', -30, 30, 10), arm('R', -30, 30, 10), leg('L', 0, 4, 30), leg('R', 0, 4, 30),
                body(hips=(0, 0, .12), spine=(-2, 0, 0)))
APEX = merge(arm('L', -40, 10, 15), arm('R', -40, 10, 15), leg('L', 30, 55, 10), leg('R', 30, 55, 10),
             body(hips=(0, 0, .55), spine=(4, 0, 0), tail=-20))                      # feet ~0.5 m off the floor
LAND = merge(arm('L', 40, 30, 30), arm('R', 40, 30, 30), leg('L', 50, 80, flat_foot(50, 80)), leg('R', 50, 80, flat_foot(50, 80)),
             body(hips=(0, .06, -.22), spine=(20, 0, 0), neck=(-10, 0, 0), tail=10))
def jump(f):
    return N.sample_keys([(1, READY), (8, CROUCH), (12, TAKEOFF), (19, APEX), (25, TAKEOFF), (29, LAND), (37, READY)], f)

def side_step(side):
    s = 1 if side == 'L' else -1
    # hips travel 0.45 m toward the step and the upper body leans 9 deg the same way; the trailing leg
    # opens 42 deg so its foot stays planted, the leading foot lands ~0.65 m out; the leading arm opens up
    lead, trail = side, ('R' if side == 'L' else 'L')
    STEP = merge(arm(lead, 40, 0, 15), arm(trail, 75, 0, 25),
                 leg(lead, 6, 14, 0, spread=18), leg(trail, 2, 6, 0, spread=42),
                 body(hips=(s * .45, 0, -.13), spine=(5, s * 9, 0), chest=(0, s * 3, 0), head=(0, -s * 6, 0)),
                 {'Tail0': {'r': (25, 0, 0)}, 'Tail1': {'r': (10, 0, 0)}})   # tail up, off the floor
    def fn(f):
        return N.sample_keys([(1, READY), (10, STEP), (19, STEP), (28, READY), (31, READY)], f)
    return fn

def arm_reach(side):
    REACH = merge(arm(side, 0, 0, 0, 0), arm('R' if side == 'L' else 'L', 80, 5, 20),
                  leg('L', 0, 6), leg('R', 0, 6), body(spine=(2, 0, 0)))           # shoulders stay level
    def fn(f):
        return N.sample_keys([(1, READY), (10, REACH), (21, REACH), (29, READY), (31, READY)], f)
    return fn

UP = merge(arm('L', -50, 0, 5, 10), arm('R', -50, 0, 5, 10), leg('L', 0, 6), leg('R', 0, 6), body(spine=(-4, 0, 0), neck=(-8, 0, 0), tail=-15))
def hands_up(f):
    return N.sample_keys([(1, READY), (9, UP), (21, UP), (29, READY), (31, READY)], f)

# seated in the cart (same base as AN_Monkey_CartRide): thighs forward, knees bent, hands on the rim
SEAT = merge(arm('L', 62, 30, 45), arm('R', 62, 30, 45), leg('L', 42, 70), leg('R', 42, 70),
             body(hips=(0, 0, -.12), spine=(14, 0, 0)))
def cart_lean(side):
    s = 1 if side == 'L' else -1
    # bend at the waist toward the monkey's own side (+X = left); the seat and legs stay in the cart
    # hips and legs stay in the seat; only the spine leans, both hands keep the rim (upper arms counter-rotate)
    LEAN = N.add(SEAT, {'Spine': {'r': (0, s * 18, 0)}, 'Chest': {'r': (0, s * 10, 0)},
                        'Neck': {'r': (0, -s * 6, 0)}, 'Head': {'r': (0, -s * 6, 0)},
                        'LeftUpperArm': {'r': (0, -s * 22, 0)}, 'RightUpperArm': {'r': (0, -s * 22, 0)}})
    def fn(f):
        return N.sample_keys([(1, SEAT), (12, LEAN), (24, LEAN), (34, SEAT), (37, SEAT)], f)
    return fn

clips = {
    'AN_Monkey_Demo_RunInPlace': (21, run_in_place),
    'AN_Monkey_Demo_HalfSquat': (45, half_squat),
    'AN_Monkey_Demo_Jump': (37, jump),
    'AN_Monkey_Demo_SideStep_L': (31, side_step('L')),
    'AN_Monkey_Demo_SideStep_R': (31, side_step('R')),
    'AN_Monkey_Demo_HighKnee': (21, high_knee),
    'AN_Monkey_Demo_ArmReach_L': (31, arm_reach('L')),
    'AN_Monkey_Demo_ArmReach_R': (31, arm_reach('R')),
    'AN_Monkey_Demo_HandsUp': (31, hands_up),
    'AN_Monkey_Demo_CartLean_L': (37, cart_lean('L')),
    'AN_Monkey_Demo_CartLean_R': (37, cart_lean('R')),
}
actions = {}
for name, (frames, fn) in clips.items():
    actions[name] = N.bake(rig, name, frames, fn)
    A.export_fbx(A.OUT_CHAR / (name + '.fbx'), [rig], animated=True)
    A.register_asset(A.OUT_CHAR / (name + '.fbx'), 'humanoid', 'TutorialDemo', loop=True, frames=frames)
rig.animation_data.action = actions['AN_Monkey_Demo_RunInPlace']
A.save_blend('ArtV2_TutorialDemo')

if '--no-render' not in A.args():
    A.studio()
    out = A.REVIEW / 'TutorialDemo'
    keyframes = {'AN_Monkey_Demo_RunInPlace': [1, 6, 11, 16], 'AN_Monkey_Demo_HalfSquat': [1, 8, 20, 40],
                 'AN_Monkey_Demo_Jump': [1, 8, 19, 29], 'AN_Monkey_Demo_SideStep_L': [1, 6, 14, 28],
                 'AN_Monkey_Demo_SideStep_R': [1, 6, 14, 28], 'AN_Monkey_Demo_HighKnee': [1, 6, 11, 16],
                 'AN_Monkey_Demo_ArmReach_L': [1, 6, 15, 29], 'AN_Monkey_Demo_ArmReach_R': [1, 6, 15, 29],
                 'AN_Monkey_Demo_HandsUp': [1, 5, 15, 29], 'AN_Monkey_Demo_CartLean_L': [1, 8, 18, 34],
                 'AN_Monkey_Demo_CartLean_R': [1, 8, 18, 34]}
    for name, frames in keyframes.items():
        rig.animation_data.action = actions[name]
        bpy.context.scene.frame_start, bpy.context.scene.frame_end = 1, clips[name][0]
        # the tutorial window shows the monkey from the front; squat/jump/high-knee also from the side for depth
        A.render_frames(out, name, [mesh], frames, 'front', size=3.6, center=(0, 0, 1.25),
                        gif_frames=list(range(1, clips[name][0] + 1, 2)))
        if name in ('AN_Monkey_Demo_HalfSquat', 'AN_Monkey_Demo_Jump', 'AN_Monkey_Demo_HighKnee', 'AN_Monkey_Demo_CartLean_L'):
            A.render_frames(out, name + '_side', [mesh], frames, 'side', size=3.6, center=(0, 0, 1.25), gif=False)
    # CartLean seated in the existing mine cart (MuhanokModules.blend 'MineCartEmpty'), front and side
    with bpy.data.libraries.load(str(A.BLEND_DIR / 'MuhanokModules.blend'), link=False) as (f_, t_):
        t_.collections = [c for c in f_.collections if c == 'MineCartEmpty']
    cart_col = t_.collections[0]; bpy.context.scene.collection.children.link(cart_col)
    cart = [o for o in cart_col.objects if o.type == 'MESH']
    rig.location.z = .45   # preview only: seat the monkey on the cart bed (the game places it in the cart)
    for name in ('AN_Monkey_Demo_CartLean_L', 'AN_Monkey_Demo_CartLean_R'):
        rig.animation_data.action = actions[name]
        bpy.context.scene.frame_start, bpy.context.scene.frame_end = 1, clips[name][0]
        A.solo([mesh] + cart)
        A.render_frames(out, name + '_in_cart', [mesh] + cart, [1, 8, 18, 34], 'front', size=3.6, center=(0, 0, 1.2),
                        gif_frames=list(range(1, clips[name][0] + 1, 2)))
        A.render_frames(out, name + '_in_cart_side', [mesh] + cart, [1, 18], 'side', size=3.6, center=(0, 0, 1.2), gif=False)
    for o in cart: o.hide_render = True
    rig.location.z = 0
    A.solo([mesh])
    refs = A.append_reference_characters(offset_x=(-2.2, -4.4))
    others = [o for c in bpy.data.collections if c.name.startswith(('Monkey.', 'Police')) for o in c.objects if o.type == 'MESH']
    rig.animation_data.action = actions['AN_Monkey_Demo_Jump']; bpy.context.scene.frame_set(19)
    A.render(out / 'AN_Monkey_Demo_Jump_compare.png', [mesh] + others, 'three_quarter', res=(1400, 900), size=6.6, center=(-2.2, 0, 1.2))
print('TUTORIAL_DEMO_PASS', ', '.join(clips))
