"""Print art-style facts (triangles, sizes, materials, bones) of a .blend.

blender -b <file.blend> --python analyze_art_style.py -- [out.json]
Used to write docs/ART_STYLE_GUIDE.md. Read-only: never saves the .blend.
"""
import bpy, json, sys
from mathutils import Vector

def tris(ob):
    dg = bpy.context.evaluated_depsgraph_get()
    me = ob.evaluated_get(dg).to_mesh()
    n = sum(len(p.vertices) - 2 for p in me.polygons)
    smooth = sum(1 for p in me.polygons if p.use_smooth)
    total = len(me.polygons)
    ob.evaluated_get(dg).to_mesh_clear()
    return n, (smooth / total if total else 0)

def bbox(ob):
    pts = [ob.matrix_world @ Vector(c) for c in ob.bound_box]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return [round(v, 3) for v in (hi - lo)], [round(v, 3) for v in lo]

out = {'file': bpy.data.filepath, 'collections': {}, 'materials': {}, 'armatures': {}}
for col in bpy.data.collections:
    rows = []
    for ob in col.all_objects:
        if ob.type == 'MESH':
            n, sm = tris(ob)
            size, lo = bbox(ob)
            rows.append({'name': ob.name, 'tris': n, 'smooth': round(sm, 2), 'size': size, 'min': lo,
                         'mats': [m.name for m in ob.data.materials if m]})
    out['collections'][col.name] = {'tris': sum(r['tris'] for r in rows), 'objects': rows}
for m in bpy.data.materials:
    p = next((n for n in (m.node_tree.nodes if m.node_tree else []) if n.type == 'BSDF_PRINCIPLED'), None)
    if p:
        c = p.inputs['Base Color'].default_value
        out['materials'][m.name] = {'linear': [round(c[i], 3) for i in range(3)],
            'hex_as_unity_srgb': '#%02X%02X%02X' % tuple(int(min(1, c[i]) * 255 + .5) for i in range(3)),
            'roughness': round(p.inputs['Roughness'].default_value, 2), 'metallic': round(p.inputs['Metallic'].default_value, 2)}
for a in bpy.data.armatures:
    out['armatures'][a.name] = {b.name: [round(v, 3) for v in b.head_local] + [round(b.length, 3)] for b in a.bones}
args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
text = json.dumps(out, indent=1, ensure_ascii=False)
if args:
    open(args[0], 'w', encoding='utf-8').write(text)
for k, v in out['collections'].items():
    print('COLLECTION', k, v['tris'])
