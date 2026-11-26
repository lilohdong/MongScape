"""Pose/keyframe helpers for art v2 clips (works for the Monkey humanoid rig and the Lion rig).

Rotations are given in degrees about ARMATURE axes (X = right, Y = back, Z = up; characters face -Y)
and converted to each bone's local frame, the same convention as build_blender_characters.pose().
Positive X turns a downward leg backwards and tips a forward-pointing bone's end down.
"""
import math
import bpy
from mathutils import Quaternion, Vector, Euler

def ease(t):
    return t * t * (3 - 2 * t)

def _rest_rot(rig, bone):
    return rig.data.bones[bone].matrix_local.to_quaternion()

def apply_pose(rig, pose):
    """pose = {bone: {'r': (x,y,z) degrees armature axes, 'l': (x,y,z) metres armature axes}}"""
    for pb in rig.pose.bones:
        pb.rotation_mode = 'QUATERNION'
        pb.rotation_quaternion = Quaternion(); pb.location = (0, 0, 0)
    for bone, v in pose.items():
        if bone not in rig.pose.bones: continue
        pb = rig.pose.bones[bone]; basis = _rest_rot(rig, bone)
        if 'r' in v:
            x, y, z = (math.radians(a) for a in v['r'])
            q = Quaternion((0, 0, 1), z) @ Quaternion((0, 1, 0), y) @ Quaternion((1, 0, 0), x)
            if 'base' in v: q = q @ v['base']
            pb.rotation_quaternion = basis.inverted() @ q @ basis
        elif 'base' in v:
            pb.rotation_quaternion = basis.inverted() @ v['base'] @ basis
        if 'l' in v:
            pb.location = basis.inverted() @ Vector(v['l'])

def mix(a, b, t):
    """Blend two pose dicts (component-wise, missing = 0)."""
    out = {}
    for bone in set(a) | set(b):
        va, vb = a.get(bone, {}), b.get(bone, {})
        o = {}
        for k in ('r', 'l'):
            if k in va or k in vb:
                pa, pb_ = va.get(k, (0, 0, 0)), vb.get(k, (0, 0, 0))
                o[k] = tuple(pa[i] + (pb_[i] - pa[i]) * t for i in range(3))
        base = va.get('base') or vb.get('base')
        if base is not None: o['base'] = base
        out[bone] = o
    return out

def add(a, b, w=1.0):
    out = {k: dict(v) for k, v in a.items()}
    for bone, vb in b.items():
        o = out.setdefault(bone, {})
        for k in ('r', 'l'):
            if k in vb:
                pa = o.get(k, (0, 0, 0)); o[k] = tuple(pa[i] + vb[k][i] * w for i in range(3))
        if 'base' in vb and 'base' not in o: o['base'] = vb['base']
    return out

def sample_keys(keys, frame):
    """keys = [(frame, pose)], eased blend between neighbours, held at the ends."""
    if frame <= keys[0][0]: return keys[0][1]
    for (f0, p0), (f1, p1) in zip(keys, keys[1:]):
        if f0 <= frame <= f1:
            return mix(p0, p1, ease((frame - f0) / max(1e-6, f1 - f0)))
    return keys[-1][1]

def bake(rig, name, frames, pose_at, extra=None):
    """Create action `name` and key every frame from pose_at(frame) (same as the existing clips)."""
    act = bpy.data.actions.new(name)
    rig.animation_data_create(); rig.animation_data.action = act
    for f in range(1, frames + 1):
        apply_pose(rig, pose_at(f))
        if extra: extra(f)
        for pb in rig.pose.bones:
            pb.keyframe_insert('rotation_quaternion', frame=f)
            pb.keyframe_insert('location', frame=f)
    sc = bpy.context.scene; sc.frame_start = 1; sc.frame_end = frames; sc.frame_set(1)
    act.use_fake_user = True
    return act

def key_object(ob, frame, loc=None, rot=None, scale=None):
    if loc is not None: ob.location = loc; ob.keyframe_insert('location', frame=frame)
    if rot is not None: ob.rotation_euler = Euler([math.radians(a) for a in rot]); ob.keyframe_insert('rotation_euler', frame=frame)
    if scale is not None: ob.scale = scale; ob.keyframe_insert('scale', frame=frame)
