"""Separate the Monkey's two feet, which the voxel remesh fused with a thin skin bridge.

blender -b --python Tools/AssetPipeline/fix_monkey_feet_bridge.py
Found in the art v2 review (2026-11-26): when the legs part (side step, high knee) the bridge
stretches into a skin-coloured strip between the soles. This script, on the existing
References/Blender/MuhanokCharacters.blend:
1. deletes the faces that cross the centre line below 0.3 m (the bridge between the feet),
2. caps the two small openings left on the inner sides of the feet,
3. gives low vertices only their own side's leg/foot weights (x < 0 = Right*, x > 0 = Left*),
then re-exports Characters/Monkey.fbx with the same settings as build_blender_characters.py and
saves the .blend. Rig, bone names, shape keys (Blink, JawOpen) and materials are unchanged.
"""
import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent))
import bpy, bmesh
import mong_art_common as A

src = A.BLEND_DIR / 'MuhanokCharacters.blend'
bpy.ops.wm.open_mainfile(filepath=str(src))
mesh = bpy.data.objects['Monkey_SkinnedMesh']
rig = mesh.parent
me = mesh.data
before = len(me.vertices)

bm = bmesh.new(); bm.from_mesh(me)
LOW = .3
bridge = [f for f in bm.faces
          if all(v.co.z < LOW for v in f.verts) and min(v.co.x for v in f.verts) < 0 < max(v.co.x for v in f.verts)]
bmesh.ops.delete(bm, geom=bridge, context='FACES')
boundary = [e for e in bm.edges if e.is_boundary and all(v.co.z < LOW for v in e.verts)]
filled = bmesh.ops.holes_fill(bm, edges=boundary, sides=0)['faces']
bmesh.ops.triangulate(bm, faces=[f for f in filled if len(f.verts) > 4])
bm.to_mesh(me); bm.free()

names = {g.index: g.name for g in mesh.vertex_groups}
cleaned = 0
for v in me.vertices:
    if v.co.z >= LOW or abs(v.co.x) > .12: continue
    keep = 'Right' if v.co.x < 0 else 'Left'
    drop = 'Left' if keep == 'Right' else 'Right'
    removed = 0.0
    for g in list(v.groups):
        n = names[g.group]
        if n.startswith(drop):
            removed += g.weight; mesh.vertex_groups[n].remove([v.index]); cleaned += 1
    if removed:
        total = sum(g.weight for g in v.groups)
        for g in v.groups:
            mesh.vertex_groups[names[g.group]].add([v.index], g.weight / max(total, 1e-6), 'REPLACE')
for p in me.polygons: p.use_smooth = True
me.update()
print('MONKEY_FEET_FIX faces_removed=%d holes_filled=%d weights_cleaned=%d verts %d -> %d'
      % (len(bridge), len(filled), cleaned, before, len(me.vertices)))

# same export as build_blender_characters.export(OUT/'Monkey.fbx', [rig, mesh])
preview_loc = rig.location.copy(); rig.location = (0, 0, 0)   # the .blend keeps the rig at x=-1.5 for its preview
if rig.animation_data: rig.animation_data.action = None
bpy.ops.object.select_all(action='DESELECT')
for o in (rig, mesh): o.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.export_scene.fbx(filepath=str(A.OUT_CHAR / 'Monkey.fbx'), use_selection=True, object_types={'ARMATURE', 'MESH'},
                         axis_forward='-Z', axis_up='Y', add_leaf_bones=False, bake_anim=False,
                         bake_anim_use_all_actions=False, bake_anim_use_nla_strips=False, bake_anim_simplify_factor=0,
                         use_armature_deform_only=True, mesh_smooth_type='FACE')
rig.location = preview_loc
bpy.ops.wm.save_as_mainfile(filepath=str(src))
print('MONKEY_FEET_FIX_PASS')
