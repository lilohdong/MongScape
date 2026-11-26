"""Two more tutorial demo motions on the existing humanoid rig (same method as build_art_tutorial_demo.py).

blender -b --python Tools/AssetPipeline/build_art_tutorial_demo_v2.py [-- --no-render]
Each clip starts and ends on READY (the same pose as the other demo clips) so it loops in the demo window. 30 fps.
- AN_Monkey_Demo_ArmsCrossX  61f  pause move (plan 6-7a): both forearms cross in an X in front of the chest,
                                  held ~1.3 s (the game needs a 1 s hold with the legs still), then back
- AN_Monkey_Demo_ArmsT       31f  upper-body mode jump (plan 5-1): both arms snap straight out to the sides
                                  at shoulder height (T), short hold, back down; legs and hips stay still
Both moves are left/right symmetric, so the clip reads the same in the mirrored tutorial window.
"""
import sys, math
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent))
import bpy
import mong_art_common as A
import mong_anim as N
from mong_monkey_poses import arm, leg, body, merge

A.reset()
rig = A.load_monkey_rig()
mesh = next(o for o in bpy.data.collections['Monkey'].objects if o.type == 'MESH')
rig.location = (0, 0, 0)
READY = merge(arm('L', 78, 8, 25), arm('R', 78, 8, 25), leg('L', 0, 6), leg('R', 0, 6), body(spine=(3, 0, 0)))

# art review 1: the wrists cross in front of the mid chest and the forearms open up and out to about 45 deg,
# hands near the shoulder line, elbows low; the forearms cross over the light belly so the X reads from 2-3 m.
# The upper arms swing forward and across, the Shoulder bones roll ~10 deg forward with them (no shoulder bump).
XD, XF, XB, XT, XS = 55, 145, 100, 55, 10   # below horizontal, swung forward, elbow flex, upper-arm twist, shoulder roll
CROSS = merge(arm('L', XD, XF, XB, twist=XT), arm('R', XD + 3, XF + 5, XB, twist=-XT),   # right forearm just in front
              leg('L', 0, 6), leg('R', 0, 6), body(spine=(2, 0, 0), neck=(-4, 0, 0)))
CROSS = N.add(CROSS, {'LeftShoulder': {'r': (0, 0, -XS)}, 'RightShoulder': {'r': (0, 0, XS)}})
def arms_cross_x(f):
    return N.sample_keys([(1, READY), (10, CROSS), (50, CROSS), (58, READY), (61, READY)], f)

T = merge(arm('L', 0, 0, 0), arm('R', 0, 0, 0), leg('L', 0, 6), leg('R', 0, 6), body(spine=(0, 0, 0), neck=(-3, 0, 0)))
def arms_t(f):
    return N.sample_keys([(1, READY), (6, T), (19, T), (27, READY), (31, READY)], f)

clips = {
    'AN_Monkey_Demo_ArmsCrossX': (61, arms_cross_x),
    'AN_Monkey_Demo_ArmsT': (31, arms_t),
}
actions = {}
for name, (frames, fn) in clips.items():
    actions[name] = N.bake(rig, name, frames, fn)
    A.export_fbx(A.OUT_CHAR / (name + '.fbx'), [rig], animated=True)
    A.register_asset(A.OUT_CHAR / (name + '.fbx'), 'humanoid', 'TutorialDemo', loop=True, frames=frames)
rig.animation_data.action = actions['AN_Monkey_Demo_ArmsCrossX']
A.save_blend('ArtV2_TutorialDemo2')

if '--no-render' not in A.args():
    A.studio()
    out = A.REVIEW / 'TutorialDemo'
    keyframes = {'AN_Monkey_Demo_ArmsCrossX': [1, 5, 30, 56], 'AN_Monkey_Demo_ArmsT': [1, 4, 12, 24]}
    for name, frames in keyframes.items():
        rig.animation_data.action = actions[name]
        bpy.context.scene.frame_start, bpy.context.scene.frame_end = 1, clips[name][0]
        A.render_frames(out, name, [mesh], frames, 'front', size=3.6, center=(0, 0, 1.25),
                        gif_frames=list(range(1, clips[name][0] + 1, 2)))
        A.render_frames(out, name + '_side', [mesh], [frames[2]], 'side', size=3.6, center=(0, 0, 1.25), gif=False)
        A.render_frames(out, name + '_three_quarter', [mesh], [frames[2]], 'three_quarter', size=3.6, center=(0, 0, 1.25), gif=False)
print('TUTORIAL_DEMO2_PASS', ', '.join(clips))
