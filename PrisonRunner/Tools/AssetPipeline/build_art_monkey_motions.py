"""Monkey rescue motions on the existing humanoid rig (MuhanokCharacters.blend 'Rig').

blender -b --python Tools/AssetPipeline/build_art_monkey_motions.py [-- --no-render]
Clips (rig-only FBX in Staging/Blender/Characters, Humanoid in Unity):
- AN_Monkey_WallStuck_Wave_L / _R (loop, 37f): stuck in a broken mine wall, waving with the free hand.
  _R waves the right hand: use it in the lane-1 LEFT wall (MD_Mine_BrokenWall_L), where the monkey faces the track and
  its right hand is the one nearer the approaching runner. _L is the mirror for the lane-3 right wall.
- AN_Monkey_RescuePop (40f): grabbed by the hand, yanked out of the wall, lands and cheers.
- AN_Monkey_CellEscape (54f): peeks at the open cell door, then bursts out running forward 3.6 m.
  Its last frame (f054) is the same pose as AN_Monkey_Run f001, so Run can follow without a blend.
Body height and travel are in the Hips keys (Unity bakes root Y/XZ into the pose, ART_STYLE_GUIDE 6);
the code moves the object to the end position after a travelling clip.
Arms never go higher than 50 deg above the shoulder and the Shoulder bone shares the raise (skin creases otherwise).
"""
import sys, math
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent))
import bpy
import mong_art_common as A
import mong_anim as N
import mong_monkey_poses as P
from mong_monkey_poses import arm, leg, body, merge, mirror

A.reset()
rig = A.load_monkey_rig()
mesh = next(o for o in bpy.data.collections['Monkey'].objects if o.type == 'MESH')
rig.location = (0, 0, 0)

# ------------------------------------------------------------------ wall stuck wave (right hand)
def wall_wave_R(f, frames=37):
    t = (f - 1) / (frames - 1); ph = 2 * math.pi * t
    w2 = math.sin(2 * ph)   # two waves per loop
    p = merge(
        arm('R', -38 + 10 * w2, 12, 0), {'RightLowerArm': {'r': (0, -(18 + 32 * w2), 0)}, 'RightHand': {'r': (0, -22 * w2, 0)}},
        arm('L', 35, -35, 70),                      # braced against the wall edge
        leg('L', 20, 35), leg('R', 12, 30),         # hidden in the wall, slightly bent
        body(hips=(0, .05, -.04), hips_rot=(0, 0, 0), spine=(-6 + 3 * math.sin(ph), 5 * math.sin(ph), 0),
             chest=(-4, 4 * math.sin(ph + .5), 6 * math.sin(ph)), neck=(-4, 0, 0),
             head=(2, -4 + 3 * math.sin(ph + 1), 6 * math.sin(ph + .3)), tail=0))   # eyes stay visible from the track
    return p

def wall_wave_L(f):
    return mirror(wall_wave_R(f))

# ------------------------------------------------------------------ rescue pop (grab -> yank -> land -> cheer)
REACH = merge(arm('R', -25, 70, 5), arm('L', 30, -30, 60), leg('L', 20, 35), leg('R', 12, 30),
              body(hips=(0, .05, -.04), spine=(-4, 6, 0), chest=(0, 0, 10), neck=(-6, 0, 0), head=(-4, 0, 0)))
TUG = merge(arm('R', -10, 85, 0), arm('L', 40, -45, 40), leg('L', 35, 60), leg('R', 25, 50),
            body(hips=(0, .25, -.10), spine=(-12, 0, 0), chest=(-6, 0, 6), neck=(-8, 0, 0), head=(-6, 0, 0)))
YANK = merge(arm('R', -20, 95, 0), arm('L', -10, -20, 20), leg('L', 70, 110, -20), leg('R', 75, 115, -20),
             body(hips=(0, -.55, .55), spine=(18, 0, 0), chest=(8, 0, 0), neck=(-12, 0, 0), head=(-8, 0, 0), tail=20))
AIR = merge(arm('R', -35, 30, 10), arm('L', -35, 30, 10), leg('L', 80, 120, -20), leg('R', 85, 120, -20),
            body(hips=(0, -1.25, .75), spine=(25, 0, 0), chest=(10, 0, 0), neck=(-14, 0, 0), head=(-10, 0, 0), tail=-15))
LAND = merge(arm('R', 20, 40, 50), arm('L', 20, 40, 50), leg('L', 70, 110, -35), leg('R', 70, 110, -35),
             body(hips=(0, -1.7, -.30), spine=(30, 0, 0), chest=(10, 0, 0), neck=(-20, 0, 0), head=(-10, 0, 0), tail=10))
CHEER = merge(arm('R', -48, 0, 25, 10), arm('L', -48, 0, 25, 10), leg('L', 5, 10), leg('R', 5, 10),
              body(hips=(0, -1.7, .06), spine=(-8, 0, 0), chest=(-6, 0, 0), neck=(-12, 0, 0), head=(-10, 0, 0), tail=-20))
READY = merge(arm('R', 80, 10, 25), arm('L', 80, 10, 25), leg('L', 0, 6), leg('R', 0, 6),
              body(hips=(0, -1.7, 0), spine=(4, 0, 0), tail=5))

def rescue_pop(f):
    keys = [(1, REACH), (6, REACH), (10, TUG), (14, YANK), (22, AIR), (27, LAND), (31, LAND), (35, CHEER), (40, READY)]
    p = N.sample_keys(keys, f)
    if f <= 10:  # wriggle while being pulled
        p = N.add(p, {'Spine': {'r': (0, 7 * math.sin(f * 1.4), 0)}, 'Head': {'r': (0, 0, 8 * math.sin(f * 1.1))}})
    if 31 <= f <= 37:  # little cheer bounce
        p = N.add(p, {'Hips': {'l': (0, 0, .06 * math.sin((f - 31) / 6 * math.pi))}})
    return p

# ------------------------------------------------------------------ cell escape (peek -> burst -> run)
PEEK = merge(arm('R', 60, 45, 70), arm('L', 70, 30, 60), leg('L', 50, 85, -20), leg('R', 45, 80, -20),
             body(hips=(0, .05, -.22), spine=(22, 0, 0), chest=(6, 0, 0), neck=(-14, 0, 0)))
COIL = merge(arm('R', 70, -45, 60), arm('L', 70, -45, 60), leg('L', 55, 95, -25), leg('R', 40, 75, -20),
             body(hips=(0, .15, -.25), spine=(30, 0, 0), chest=(8, 0, 0), neck=(-20, 0, 0), tail=10))

def cell_escape(f):
    if f <= 14:
        p = N.sample_keys([(1, PEEK), (9, PEEK), (14, COIL)], f)
        if f <= 10:  # look left and right through the door
            p = N.add(p, {'Head': {'r': (0, 0, 28 * math.sin((f - 1) / 9 * 2 * math.pi))}})
        return p
    t = (f - 14) / 20.0                     # existing run cycle length (21 frames): f054 = Run f001
    accel = min(1.0, (f - 14) / 8)
    run = P.run(t % 1, amp=1.0, lean=10 + 12 * (1 - accel))
    run = N.add(run, {'Tail0': {'r': (12, 0, 0)}, 'Tail1': {'r': (6, 0, 0)}})   # keep the tail up, off the feet
    y = -3.6 * ((f - 14) / 40) ** 1.15      # 0 -> -3.6 m over the run part
    run = N.add(run, {'Hips': {'l': (0, y, 0)}})
    if f < 18:
        run = N.mix(N.add(COIL, {'Hips': {'l': (0, 0, 0)}}), run, (f - 14) / 4)
    return run

clips = {
    'AN_Monkey_WallStuck_Wave_R': (37, wall_wave_R, True),
    'AN_Monkey_WallStuck_Wave_L': (37, wall_wave_L, True),
    'AN_Monkey_RescuePop': (40, rescue_pop, False),
    'AN_Monkey_CellEscape': (54, cell_escape, False),
}
actions = {}
for name, (frames, fn, loop) in clips.items():
    actions[name] = N.bake(rig, name, frames, fn)
    A.export_fbx(A.OUT_CHAR / (name + '.fbx'), [rig], animated=True)
    A.register_asset(A.OUT_CHAR / (name + '.fbx'), 'humanoid', 'MonkeyRescue', loop=loop, frames=frames)
rig.animation_data.action = actions['AN_Monkey_WallStuck_Wave_R']
A.save_blend('ArtV2_MonkeyMotions')

if '--no-render' not in A.args():
    out = A.REVIEW / 'MonkeyMotions'
    A.studio()
    views = {'AN_Monkey_WallStuck_Wave_R': ('front', [5, 10, 14, 23], 3.2, (0, 0, 1.2)),
             'AN_Monkey_WallStuck_Wave_L': ('front', [5, 10, 14, 23], 3.2, (0, 0, 1.2)),
             'AN_Monkey_RescuePop': ('side', [1, 14, 22, 35], 4.6, (0, -.9, 1.3)),
             'AN_Monkey_CellEscape': ('three_quarter', [1, 12, 30, 54], 5.6, (0, -1.7, 1.1))}
    for name, (view, frames, size, center) in views.items():
        rig.animation_data.action = actions[name]
        bpy.context.scene.frame_start, bpy.context.scene.frame_end = 1, clips[name][0]
        A.render_frames(out, name, [mesh], frames, view, size=size, center=center,
                        gif_frames=list(range(1, clips[name][0] + 1, 2)))
    # wave inside the real broken wall (ArtV2_PrisonMine.blend) seen from the runner's approach
    src = A.BLEND_DIR / 'ArtV2_PrisonMine.blend'
    if src.exists():
        with bpy.data.libraries.load(str(src), link=False) as (f_, t_):
            t_.objects = [n for n in f_.objects if n in ('MD_Mine_BrokenWall_L', 'MD_Mine_BrokenWall_R')]
        for wname, act, view in (('MD_Mine_BrokenWall_L', 'AN_Monkey_WallStuck_Wave_R', (.55, -1, .15)),
                                 ('MD_Mine_BrokenWall_R', 'AN_Monkey_WallStuck_Wave_L', (-.55, -1, .15))):
            w = bpy.data.objects[wname]; bpy.context.scene.collection.objects.link(w); w.location = (0, 0, 0)
            rig.location = (0, .02, 0); rig.animation_data.action = actions[act]   # = MonkeyAnchor of the wall
            bpy.context.scene.frame_start, bpy.context.scene.frame_end = 1, 37
            A.render_frames(out, act + '_in_wall', [mesh, w], [5, 10, 14, 23], view, size=4.0, center=(0, 0, 1.5),
                            gif_frames=list(range(1, 38, 2)))
            bpy.data.objects.remove(w, do_unlink=True)
        rig.location = (0, 0, 0)
    # key poses beside the reference Police (same light, same camera)
    refs = A.append_reference_characters(offset_x=(-2.0, -4.2))   # existing Monkey (T-pose) and Police
    police = [o for c in bpy.data.collections if c.name.startswith(('Monkey.', 'Police')) for o in c.objects if o.type == 'MESH']
    for name, f in (('AN_Monkey_WallStuck_Wave_R', 5), ('AN_Monkey_RescuePop', 35)):
        rig.animation_data.action = actions[name]; bpy.context.scene.frame_set(f)
        rig.location.y = 1.7 if name == 'AN_Monkey_RescuePop' else 0
        A.render(out / f'{name}_compare.png', [mesh] + police, 'three_quarter', res=(1400, 900), size=6.6, center=(-2.0, 0, 1.1))
    rig.location.y = 0
print('MONKEY_MOTIONS_PASS', ', '.join(clips))
