"""Shared helpers for Mongscape art v2 assets (Blender 5.x, headless).

Run any builder with:  blender -b --python Tools/AssetPipeline/build_art_<name>.py
Conventions (see docs/ART_STYLE_GUIDE.md):
- Blender coordinates, Z up, characters and props face -Y. FBX export axis_forward='-Z', axis_up='Y'
  exactly like build_blender_characters.py / build_blender_modules.py.
- Colours are written as the sRGB HEX the player sees in Unity. Blender gets the linear value
  (so a Blender render matches the game) and Unity gets a URP/Lit material with the same HEX
  (Tools/AssetPipeline/art_v2_palette.json -> MongscapeArtV2Import.cs remaps the FBX material).
- Preview renders use one studio rig (STUDIO_*) so every asset is compared under the same light.
"""
import bpy, bmesh, json, math, os, sys
from pathlib import Path
from mathutils import Vector, Quaternion, Matrix, Euler
from mathutils.kdtree import KDTree

ROOT = Path(__file__).resolve().parents[2]                    # PrisonRunner/
BLEND_DIR = ROOT / 'References/Blender'
STAGING = ROOT / 'Unity/Assets/External/Staging/Blender'
OUT_CHAR = STAGING / 'Characters'
OUT_MODELS = STAGING / 'Models'
OUT_ANIMALS = STAGING / 'Animals'   # Generic rigs; kept out of Characters/ (Humanoid-only folder)
ASSETS_JSON = ROOT / 'Tools/AssetPipeline/art_v2_assets.json'
PALETTE_JSON = ROOT / 'Tools/AssetPipeline/art_v2_palette.json'
REVIEW = Path(os.environ.get('MONG_ART_REVIEW', str(ROOT.parents[1] / 'art-review')))
STYLE_IMAGES = ROOT.parent / 'docs/images/style'

# ---------------------------------------------------------------- colour

def s2l(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4

def hex_rgb(h):
    h = h.lstrip('#')
    return tuple(int(h[i:i + 2], 16) / 255 for i in (0, 2, 4))

def hex_linear(h):
    return tuple(s2l(c) for c in hex_rgb(h))

# Existing in-game colours (Unity _BaseColor, sRGB) for reference and reuse.
GAME = {
    'MonkeyFur': '#57250F', 'MonkeySkin': '#E6914A', 'PrisonOrange': '#E83806', 'OrangeSeam': '#871603',
    'PoliceNavy': '#0A142B', 'PoliceBlueGrey': '#5E7A9E', 'BootsBlack': '#040508', 'BadgeBrass': '#F0940F',
    'Ivory': '#F5EBC7', 'EyeWhite': '#FAF5DE', 'Pupil': '#080502', 'Mouth': '#1F0302', 'Tongue': '#BA1F21',
    'CartTimber': '#97693D', 'CartSteel': '#65737C', 'CartWheel': '#2A303A', 'Ore': '#F9CA45',
    'ForgedIron': '#333D45', 'PaintedSteel': '#8FA6AB', 'SecurityRed': '#CC160B', 'HazardYellow': '#FFC70F',
    'ZooCaution': '#FFBD14', 'ZooGrass': '#245226', 'ZooFoliage': '#2B6629', 'ColdLamp': '#70B8F0',
    'LanternGlass': '#FF9C30', 'Charcoal': '#060709', 'StoneMid': '#CFC2AD',
}

_registry = {}

def mat(name, hex_color, rough=.62, metal=0.0, emit=None, strength=0.0, alpha=None):
    """URP-friendly principled material. name is also the Unity material name (MAT_<name>)."""
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    p = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    lin = hex_linear(hex_color)
    p.inputs['Base Color'].default_value = (*lin, 1)
    p.inputs['Roughness'].default_value = rough
    p.inputs['Metallic'].default_value = metal
    if emit:
        p.inputs['Emission Color'].default_value = (*hex_linear(emit), 1)
        p.inputs['Emission Strength'].default_value = strength
    if alpha is not None:
        p.inputs['Alpha'].default_value = alpha
        try: m.surface_render_method = 'BLENDED'
        except Exception: pass
    m.diffuse_color = (*lin, 1)
    _registry[name] = {'hex': hex_color.upper(), 'smoothness': round(1 - rough, 2) if rough < .9 else 0.1,
                       'metallic': metal, 'emission': emit.upper() if emit else None,
                       'emission_strength': strength, 'alpha': alpha}
    return m

def _load_items(path):
    if not path.exists(): return {}
    return {i['name']: i for i in json.loads(path.read_text(encoding='utf-8'))['items']}

def _save_items(path, items):
    path.write_text(json.dumps({'items': [items[k] for k in sorted(items)]}, indent=1, ensure_ascii=False) + '\n', encoding='utf-8')

def write_palette(asset):
    """Merge this asset's materials into art_v2_palette.json (read by MongscapeArtV2Import.cs).
    A material whose name matches an existing Unity material in ArtV2/Materials reuses that material."""
    data = _load_items(PALETTE_JSON)
    for name, v in _registry.items():
        old = data.get(name, {})
        v = dict(v, name=name, emission=v['emission'] or '', alpha=-1 if v['alpha'] is None else v['alpha'])
        v['assets'] = sorted(set(old.get('assets', [])) | {asset})
        data[name] = v
    _save_items(PALETTE_JSON, data)

def register_asset(path, kind, group, loop=False, avatar_from=None, frames=None):
    """Record an exported FBX for MongscapeArtV2Import.cs.

    kind: 'humanoid' (Monkey rig clip), 'generic_model', 'generic_clip', 'prop' (static), 'prop_anim'.
    path is relative to Unity/Assets/External/Staging/Blender."""
    data = _load_items(ASSETS_JSON)
    rel = Path(path).resolve().relative_to(STAGING.resolve()).as_posix()
    data[rel] = {'name': rel, 'kind': kind, 'group': group, 'loop': loop, 'avatar_from': avatar_from or '', 'frames': frames or 0}
    _save_items(ASSETS_JSON, data)

# ---------------------------------------------------------------- scene / geometry

def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    _registry.clear()
    bpy.context.scene.render.fps = 30

def link(ob, collection=None):
    (collection or bpy.context.scene.collection).objects.link(ob)
    return ob

def _finish(ob, name, material, smooth):
    ob.name = name
    if material is not None:
        ob.data.materials.append(material)
    for p in ob.data.polygons:
        p.use_smooth = smooth
    return ob

def apply_scale(ob):
    bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)

def ball(name, pos, size, material, segments=20, rings=12, smooth=True, rot=None):
    """Rounded primitive used for characters (same as build_blender_characters.ball)."""
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, radius=1, location=pos)
    ob = bpy.context.object; ob.scale = size; apply_scale(ob)
    if rot: ob.rotation_euler = rot
    return _finish(ob, name, material, smooth)

def box(name, pos, size, material, bevel=.12, segments=3, rot=None, smooth=False):
    """Bevelled box, the basic prop/environment block (soft cartoon edges)."""
    bpy.ops.mesh.primitive_cube_add(size=1, location=pos)
    ob = bpy.context.object; ob.scale = size; apply_scale(ob)
    if bevel:
        mod = ob.modifiers.new('Soft cartoon edges', 'BEVEL'); mod.width = min(size) * bevel; mod.segments = segments
        mod.limit_method = 'NONE'
        bpy.ops.object.modifier_apply(modifier=mod.name)
    if rot: ob.rotation_euler = rot
    return _finish(ob, name, material, smooth)

def cyl(name, pos, radius, depth, material, verts=16, rot=None, bevel=0.0, smooth=False):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=radius, depth=depth, location=pos)
    ob = bpy.context.object
    if rot: ob.rotation_euler = rot
    if bevel:
        mod = ob.modifiers.new('Soft cartoon edges', 'BEVEL'); mod.width = bevel; mod.segments = 2
        mod.limit_method = 'ANGLE'
        bpy.context.view_layer.objects.active = ob
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return _finish(ob, name, material, smooth)

def rod(name, a, b, radius, material, verts=10, smooth=True):
    a, b = Vector(a), Vector(b)
    ob = cyl(name, (a + b) / 2, radius, (b - a).length, material, verts, smooth=smooth)
    ob.rotation_euler = (b - a).to_track_quat('Z', 'Y').to_euler()
    return ob

def limb(name, a, b, width, depth, material, segments=20, rings=12):
    a, b = Vector(a), Vector(b)
    ob = ball(name, (a + b) / 2, (width, depth, (b - a).length * .64), material, segments, rings)
    ob.rotation_euler = (b - a).to_track_quat('Z', 'Y').to_euler()
    return ob

def join(objects, name):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects: o.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join(); ob = bpy.context.object; ob.name = name
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    return ob

def set_origin(ob, point):
    """Move the object's origin (pivot) to a world point without moving geometry."""
    point = Vector(point)
    delta = ob.matrix_world.inverted() @ point
    ob.data.transform(Matrix.Translation(-delta))
    ob.location = ob.matrix_world @ delta if ob.parent is None else ob.location + delta

def triangles(objects):
    dg = bpy.context.evaluated_depsgraph_get(); n = 0
    for ob in objects:
        if ob.type != 'MESH': continue
        me = ob.evaluated_get(dg).to_mesh()
        n += sum(len(p.vertices) - 2 for p in me.polygons)
        ob.evaluated_get(dg).to_mesh_clear()
    return n

def fuse_surface(parts, name, voxel=.022, smooth=.5, iterations=3, decimate=.5, neighbours=4, weight_smooth=0):
    """Union rounded parts into one continuous skin and transfer their bone weights (character method)."""
    samples = []
    for ob in parts:
        for v in ob.data.vertices:
            samples.append((ob.matrix_world @ v.co, [(ob.vertex_groups[g.group].name, g.weight) for g in v.groups]))
    tree = KDTree(len(samples))
    for i, (p, _) in enumerate(samples): tree.insert(p, i)
    tree.balance()
    mesh = join(parts, name)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    mesh.data.remesh_voxel_size = voxel
    bpy.ops.object.voxel_remesh()
    m = mesh.modifiers.new('Continuous rounded silhouette', 'SMOOTH'); m.factor = smooth; m.iterations = iterations
    bpy.ops.object.modifier_apply(modifier=m.name)
    m = mesh.modifiers.new('Gameplay topology budget', 'DECIMATE'); m.ratio = decimate
    bpy.ops.object.modifier_apply(modifier=m.name)
    mesh.vertex_groups.clear(); groups = {}
    for v in mesh.data.vertices:
        w = {}
        for _, idx, dist in tree.find_n(v.co, neighbours):
            inf = 1 / max(dist, .005) ** 2
            for bone, bw in samples[idx][1]: w[bone] = w.get(bone, 0) + bw * inf
        tot = sum(w.values())
        for bone, bw in w.items():
            if bone not in groups: groups[bone] = mesh.vertex_groups.new(name=bone)
            groups[bone].add([v.index], bw / tot, 'REPLACE')
    if weight_smooth:  # soften joint creases on strongly bending limbs (lion shoulders, hips)
        bpy.ops.object.select_all(action='DESELECT'); mesh.select_set(True); bpy.context.view_layer.objects.active = mesh
        bpy.ops.object.mode_set(mode='WEIGHT_PAINT')
        bpy.ops.object.vertex_group_smooth(group_select_mode='ALL', factor=.5, repeat=weight_smooth)
        bpy.ops.object.vertex_group_normalize_all(lock_active=False)
        bpy.ops.object.mode_set(mode='OBJECT')
    for p in mesh.data.polygons: p.use_smooth = True
    return mesh

def weight_all(ob, bone):
    ob.vertex_groups.new(name=bone).add(list(range(len(ob.data.vertices))), 1, 'REPLACE')
    return ob

# ---------------------------------------------------------------- export

def export_fbx(path, objects, animated=False, nla=False, all_actions=False, armature=True):
    path = Path(path); path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects: o.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    kw = dict(filepath=str(path), use_selection=True, object_types={'ARMATURE', 'MESH', 'EMPTY'} if armature else {'MESH', 'EMPTY'},
              axis_forward='-Z', axis_up='Y', add_leaf_bones=False, bake_anim=animated,
              bake_anim_use_all_actions=all_actions, bake_anim_use_nla_strips=nla, bake_anim_simplify_factor=0,
              bake_anim_force_startend_keying=True, use_armature_deform_only=True, mesh_smooth_type='FACE',
              apply_unit_scale=True, path_mode='AUTO')
    bpy.ops.export_scene.fbx(**kw)
    print('ART_V2_EXPORTED', path.name, path.stat().st_size)

def save_blend(name):
    path = BLEND_DIR / (name + '.blend')
    bpy.ops.wm.save_as_mainfile(filepath=str(path), compress=True)
    print('ART_V2_BLEND', path)
    return path

# ---------------------------------------------------------------- characters for comparison

def append_reference_characters(offset_x=(-2.4, 2.4), facing=None):
    """Append the existing Monkey and Police from MuhanokCharacters.blend for side-by-side renders.

    The character materials are imported into Unity 1:1 as sRGB, so their raw linear values are
    re-encoded here (raw -> linear) to look as they do in the game."""
    src = str(BLEND_DIR / 'MuhanokCharacters.blend')
    with bpy.data.libraries.load(src, link=False) as (data_from, data_to):
        data_to.collections = [c for c in data_from.collections if c in ('Monkey', 'Police')]
    rigs = {}
    for col, dx in zip(data_to.collections, offset_x):
        bpy.context.scene.collection.children.link(col)
        for o in col.objects:
            if o.type == 'ARMATURE':
                o.location.x = dx; rigs[col.name] = o
                if facing is not None: o.rotation_euler.z = facing
            if o.type == 'MESH':
                for m in o.data.materials:
                    if m and not m.get('mong_game_colour'):
                        p = next((n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None)
                        if p:
                            c = p.inputs['Base Color'].default_value
                            p.inputs['Base Color'].default_value = (s2l(c[0]), s2l(c[1]), s2l(c[2]), 1)
                            p.inputs['Roughness'].default_value = .78
                        m['mong_game_colour'] = True
    return rigs

def load_monkey_rig():
    """Append only the Monkey rig + mesh (for new humanoid clips on the existing skeleton)."""
    rigs = append_reference_characters(offset_x=(0, 99))
    police = bpy.data.collections.get('Police')
    if police:
        for o in list(police.objects): bpy.data.objects.remove(o, do_unlink=True)
        bpy.data.collections.remove(police)
    return rigs['Monkey']

# ---------------------------------------------------------------- studio and renders

STUDIO_BG = '#262E3D'
STUDIO_GROUND = '#1C222C'
VIEWS = {  # direction from target to eye (characters face -Y)
    'front': (0, -1, .18),
    'three_quarter': (.75, -1, .38),
    'side': (1, 0, .12),
    'back': (0, 1, .25),
    'top': (0, -.12, 1),
}

def studio(ground=True):
    sc = bpy.context.scene
    for o in [o for o in sc.objects if o.get('mong_studio')]:
        bpy.data.objects.remove(o, do_unlink=True)
    world = sc.world or bpy.data.worlds.new('Studio'); sc.world = world; world.use_nodes = True
    bg = next(n for n in world.node_tree.nodes if n.type == 'BACKGROUND')
    bg.inputs[0].default_value = (*hex_linear(STUDIO_BG), 1); bg.inputs[1].default_value = 1.0
    if ground:
        bpy.ops.mesh.primitive_plane_add(size=60, location=(0, 0, 0))
        g = bpy.context.object; g.name = 'Studio ground'; g['mong_studio'] = True
        g.data.materials.append(mat('Studio ground', STUDIO_GROUND, .9))
        _registry.pop('Studio ground', None)
    # The same three-light rig as the character preview in build_blender_characters.py
    for pos, power, size in [((-3, -4, 6), 800, 5), ((4, -1, 4), 600, 4), ((0, 3, 5), 1000, 3)]:
        bpy.ops.object.light_add(type='AREA', location=pos); o = bpy.context.object
        o.data.energy = power; o.data.shape = 'DISK'; o.data.size = size; o['mong_studio'] = True
        o.rotation_euler = (Vector((0, 0, 1)) - o.location).to_track_quat('-Z', 'Y').to_euler()
    for eng in ('BLENDER_EEVEE', 'BLENDER_EEVEE_NEXT'):
        try: sc.render.engine = eng; break
        except TypeError: continue
    try:
        sc.eevee.taa_render_samples = 32
    except Exception: pass
    sc.view_settings.view_transform = 'Standard' if 'Standard' in [i.identifier for i in bpy.types.ColorManagedViewSettings.bl_rna.properties['view_transform'].enum_items] else sc.view_settings.view_transform
    sc.render.film_transparent = False
    sc.render.image_settings.file_format = 'PNG'

def _bounds(objects, frame=None):
    pts = []
    dg = bpy.context.evaluated_depsgraph_get()
    for ob in objects:
        if ob.type == 'MESH':
            ev = ob.evaluated_get(dg)
            pts += [ev.matrix_world @ Vector(c) for c in ev.bound_box]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return lo, hi

def camera_for(objects, view, margin=1.18, size=None, center=None):
    sc = bpy.context.scene
    cam = sc.objects.get('Studio camera')
    if cam is None:
        cam = bpy.data.objects.new('Studio camera', bpy.data.cameras.new('Studio camera'))
        sc.collection.objects.link(cam); cam['mong_studio'] = True
    cam.data.type = 'ORTHO'
    lo, hi = _bounds(objects)
    c = Vector(center) if center else (lo + hi) / 2
    d = Vector(VIEWS[view] if isinstance(view, str) else view).normalized()
    cam.location = c + d * 30
    cam.rotation_euler = (c - cam.location).to_track_quat('-Z', 'Y').to_euler()
    cam.data.clip_end = 200
    ext = (hi - lo)
    cam.data.ortho_scale = size or max(ext.x, ext.y, ext.z) * margin
    sc.camera = cam
    return cam

def render(path, objects, view, res=(900, 900), size=None, center=None):
    sc = bpy.context.scene
    sc.render.resolution_x, sc.render.resolution_y = res; sc.render.resolution_percentage = 100
    camera_for(objects, view, size=size, center=center)
    path = Path(path); path.parent.mkdir(parents=True, exist_ok=True)
    sc.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)
    return path

def render_turn(folder, prefix, objects, views=('front', 'three_quarter', 'side'), res=(900, 900), size=None, center=None):
    return [render(Path(folder) / f'{prefix}_{v}.png', objects, v, res, size, center) for v in views]

def render_frames(folder, prefix, objects, frames, view='three_quarter', res=(640, 640), size=None, center=None, gif=True, gif_frames=None):
    """Four key frames as PNG (+ a contact strip) and a short GIF of the whole range."""
    sc = bpy.context.scene; folder = Path(folder); folder.mkdir(parents=True, exist_ok=True)
    outs = []
    for f in frames:
        sc.frame_set(f)
        outs.append(render(folder / f'{prefix}_f{f:03d}.png', objects, view, res, size, center))
    if gif:  # Blender's Python has no PIL: frames go to _gif/<prefix>/, make_review_media.py builds strip + GIF
        gf = gif_frames or list(range(sc.frame_start, sc.frame_end + 1, 2))
        for f in gf:
            sc.frame_set(f)
            render(folder / '_gif' / prefix / f'{f:04d}.png', objects, view, (360, 360), size, center)
        (folder / '_gif' / prefix / 'fps.txt').write_text(str(sc.render.fps / max(1, (gf[1] - gf[0]) if len(gf) > 1 else 1)))
    return outs

def args():
    return sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
