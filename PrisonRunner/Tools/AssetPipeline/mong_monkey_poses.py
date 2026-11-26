"""Pose vocabulary for the existing Monkey humanoid rig (MuhanokCharacters.blend 'Rig').

All poses are dicts for mong_anim.apply_pose. The rig rests in a T-pose facing -Y.
Arms: arm(side, down, forward, bend) - down = degrees below horizontal (90 = hanging, -90 = straight up),
forward = degrees swung toward -Y, bend = elbow flex. side: 'L' (+X) or 'R' (-X).
Legs: leg(side, lift, knee, foot) - lift = thigh raised forward, knee = flex.
"""
import math
from mathutils import Quaternion
import mong_anim as N

S = {'L': 1, 'R': -1}
NAME = {'L': 'Left', 'R': 'Right'}

def arm(side, down=80, forward=0, bend=15, hand=0, twist=0):
    s = S[side]; n = NAME[side]
    base = Quaternion((0, 1, 0), math.radians(s * down))
    # swing forward about Z (horizontal plane) after lowering; for a hanging arm the X axis does the swing
    hang = max(0.0, min(1.0, down / 90.0))
    fx = -forward * hang
    fz = -s * forward * (1 - hang)
    return {n + 'UpperArm': {'r': (fx, twist, fz), 'base': base},
            n + 'LowerArm': {'r': (0, 0, -s * bend)},   # elbow flex, same axis as the existing clips
            n + 'Hand': {'r': (0, 0, -s * hand)}}

def leg(side, lift=0, knee=4, foot=0, spread=0):
    n = NAME[side]; s = S[side]
    return {n + 'UpperLeg': {'r': (-lift, -s * spread, 0)},
            n + 'LowerLeg': {'r': (knee, 0, 0)},
            n + 'Foot': {'r': (foot, 0, 0)}}

def body(hips=(0, 0, 0), hips_rot=(0, 0, 0), spine=(0, 0, 0), chest=(0, 0, 0), neck=(0, 0, 0), head=(0, 0, 0), tail=0):
    p = {'Hips': {'r': hips_rot, 'l': hips}, 'Spine': {'r': spine}, 'Chest': {'r': chest},
         'Neck': {'r': neck}, 'Head': {'r': head}}
    for i in range(5):
        p['Tail' + str(i)] = {'r': (0, 0, tail)}
    return p

def merge(*ps):
    out = {}
    for p in ps: out = N.add(out, p)
    return out

def mirror(p):
    """Swap left/right and mirror rotations across the YZ plane."""
    out = {}
    for bone, v in p.items():
        nb = bone.replace('Left', '#').replace('Right', 'Left').replace('#', 'Right')
        o = {}
        if 'r' in v: o['r'] = (v['r'][0], -v['r'][1], -v['r'][2])
        if 'l' in v: o['l'] = (-v['l'][0], v['l'][1], v['l'][2])
        if 'base' in v:
            q = v['base']; o['base'] = Quaternion((q.w, q.x, -q.y, -q.z))
        out[nb] = o
    return out

STAND = merge(arm('L', 80, 0, 15), arm('R', 80, 0, 15), leg('L'), leg('R'), body())

def run(t, amp=1.0, lean=10):
    """Same run as the existing AN_Monkey_Run (build_blender_characters.action)."""
    w = math.sin(t * 2 * math.pi)
    p = {}
    for side in ('L', 'R'):
        s = S[side]
        p = merge(p, arm(side, 90, -w * s * 34 * amp, 65), leg(side, -w * s * 32 * amp, max(0, -w * s) * 55 * amp + 4, -max(0, -w * s) * 15))
    return merge(p, body(hips=(0, 0, .022 * math.cos(t * 4 * math.pi)), spine=(lean, 0, 0), tail=w * 10))
