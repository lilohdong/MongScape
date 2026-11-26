using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mongscape.Editor
{
    // Imports the art v2 Blender exports listed in Tools/AssetPipeline/art_v2_assets.json:
    // importer type (Humanoid / Generic / prop), clip names and loops, material remap to Unity
    // materials from art_v2_palette.json, and a preview scene with every asset side by side.
    // Touches only the listed FBX files. See docs/ART_STYLE_GUIDE.md section 9.
    public static class MongscapeArtV2Import
    {
        private const string Staging = "Assets/External/Staging/Blender/";
        private const string Materials = "Assets/Game/Content/ArtV2/Materials";
        private const string NewMaterials = Materials + "/ArtV2";
        private const string Preview = "Assets/Game/Content/ArtV2/Preview";
        private const string PreviewScene = Preview + "/ArtV2_Preview.unity";
        private static readonly string[] HumanoidBones = {"Hips","Spine","Chest","Neck","Head",
            "LeftShoulder","LeftUpperArm","LeftLowerArm","LeftHand","RightShoulder","RightUpperArm","RightLowerArm","RightHand",
            "LeftUpperLeg","LeftLowerLeg","LeftFoot","LeftToes","RightUpperLeg","RightLowerLeg","RightFoot","RightToes"};

        [Serializable] private class PaletteFile { public PaletteItem[] items; }
        [Serializable] private class PaletteItem { public string name, hex, emission; public float smoothness, metallic, emission_strength, alpha; }
        [Serializable] private class AssetFile { public AssetItem[] items; }
        [Serializable] private class AssetItem { public string name, kind, group, avatar_from; public bool loop; public int frames; }

        private static string ToolPath(string file) => Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../../Tools/AssetPipeline", file));

        [MenuItem("Mongscape/Art V2/Import Staging Assets")]
        public static void ImportAll()
        {
            AssetDatabase.Refresh();
            var palette = JsonUtility.FromJson<PaletteFile>(File.ReadAllText(ToolPath("art_v2_palette.json"))).items;
            var assets = JsonUtility.FromJson<AssetFile>(File.ReadAllText(ToolPath("art_v2_assets.json"))).items;
            var materials = palette.ToDictionary(p => p.name, EnsureMaterial);
            AssetDatabase.SaveAssets();
            // Models first so generic clips can copy their avatar.
            foreach (var item in assets.OrderBy(a => a.kind == "generic_model" ? 0 : 1).ThenBy(a => a.name))
                Configure(item, materials);
            BuildPreviewScene(assets);
            Debug.Log("MONGSCAPE_ART_V2_IMPORT_PASS: " + assets.Length + " FBX, " + materials.Count + " materials");
        }

        // Batch entry: Unity -batchmode -executeMethod Mongscape.Editor.MongscapeArtV2Import.ImportAllBatch -quit
        public static void ImportAllBatch()
        {
            try { ImportAll(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        private static Material EnsureMaterial(PaletteItem p)
        {
            string existing = Materials + "/" + p.name + ".mat";
            var reuse = AssetDatabase.LoadAssetAtPath<Material>(existing);
            if (reuse != null) return reuse;   // in-game material with the same name (ForgedIron, ZooCaution, ...)
            Directory.CreateDirectory(NewMaterials);
            string path = NewMaterials + "/" + p.name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            ColorUtility.TryParseHtmlString(p.hex, out var color);
            bool transparent = p.alpha >= 0;
            if (transparent) color.a = p.alpha;
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", p.smoothness);
            mat.SetFloat("_Metallic", p.metallic);
            if (!string.IsNullOrEmpty(p.emission))
            {
                ColorUtility.TryParseHtmlString(p.emission, out var e);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", e * Mathf.Max(1f, p.emission_strength));
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else mat.DisableKeyword("_EMISSION");
            if (transparent)
            {
                mat.SetFloat("_Surface", 1); mat.SetFloat("_Blend", 0); mat.SetFloat("_ZWrite", 0); mat.SetFloat("_Cull", 0);
                mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = (int)RenderQueue.Transparent;
                mat.SetOverrideTag("RenderType", "Transparent");
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void Configure(AssetItem item, Dictionary<string, Material> materials)
        {
            string path = Staging + item.name;
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            if (importer == null) throw new InvalidOperationException("Art v2 FBX missing: " + path);
            string id = Path.GetFileNameWithoutExtension(path);
            importer.importCameras = false; importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.preserveHierarchy = true;   // clip FBX (rig only) and model FBX keep the same "Rig/..." paths
            switch (item.kind)
            {
                case "humanoid":
                    importer.animationType = ModelImporterAnimationType.Human;
                    importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    var desc = importer.humanDescription;
                    desc.human = HumanoidBones.Select(n => new HumanBone { boneName = n, humanName = n, limit = new HumanLimit { useDefaultValues = true } }).ToArray();
                    desc.armStretch = .05f; desc.legStretch = .05f; desc.upperArmTwist = .5f; desc.lowerArmTwist = .5f;
                    desc.upperLegTwist = .5f; desc.lowerLegTwist = .5f; desc.feetSpacing = 0; desc.hasTranslationDoF = false;
                    importer.humanDescription = desc;
                    importer.importAnimation = true;
                    break;
                case "generic_model":
                    importer.animationType = ModelImporterAnimationType.Generic;
                    importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    importer.importAnimation = false;
                    break;
                case "generic_clip":
                    importer.animationType = ModelImporterAnimationType.Generic;
                    importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                    importer.sourceAvatar = AssetDatabase.LoadAllAssetsAtPath(Staging + item.avatar_from).OfType<Avatar>().FirstOrDefault()
                        ?? throw new InvalidOperationException("Source avatar missing: " + item.avatar_from);
                    importer.importAnimation = true;
                    break;
                case "prop_anim":
                    importer.animationType = ModelImporterAnimationType.Generic;
                    importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    importer.importAnimation = true;
                    break;
                default:
                    importer.animationType = ModelImporterAnimationType.None;
                    importer.importAnimation = false;
                    break;
            }
            importer.SaveAndReimport();
            if (importer.importAnimation)
            {
                var clips = importer.defaultClipAnimations;
                if (clips.Length == 0) throw new InvalidOperationException("No take in " + path);
                var c = clips[0];
                c.name = item.kind == "prop_anim" ? "AN_" + id.Substring(3) : id;
                c.loopTime = item.loop;
                c.lockRootRotation = true; c.lockRootHeightY = true; c.lockRootPositionXZ = true;
                c.keepOriginalOrientation = true; c.keepOriginalPositionY = true; c.keepOriginalPositionXZ = true;
                importer.clipAnimations = new[] { c };
            }
            foreach (var m in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>())
                if (materials.TryGetValue(m.name, out var target))
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), m.name), target);
            importer.SaveAndReimport();
            if (item.kind == "humanoid" || item.kind.StartsWith("generic"))
            {
                var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
                if (item.kind != "generic_clip" && (avatar == null || !avatar.isValid))
                    throw new InvalidOperationException("Invalid avatar: " + path);
            }
        }

        private static AnimationClip ClipOf(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__"));

        private static RuntimeAnimatorController Controller(string name, IEnumerable<AnimationClip> clips)
        {
            Directory.CreateDirectory(Preview);
            string path = Preview + "/" + name + ".controller";
            var wanted = clips.Where(c => c != null).ToList();
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (existing != null && existing.layers.Length == 1 &&
                existing.layers[0].stateMachine.states.Select(s => s.state.motion).SequenceEqual(wanted))
                return existing;   // unchanged: keep the file (and its ids) as is
            AssetDatabase.DeleteAsset(path);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            var sm = ctrl.layers[0].stateMachine;
            var list = clips.Where(c => c != null).ToList();
            AnimatorState first = null, prev = null;
            foreach (var clip in list)
            {
                var s = sm.AddState(clip.name); s.motion = clip;
                first ??= s;
                if (prev != null) { var t = prev.AddTransition(s); t.hasExitTime = true; t.exitTime = prev.motion is AnimationClip pc && pc.isLooping ? 3f : 1f; t.duration = .1f; }
                prev = s;
            }
            if (prev != null && first != null && prev != first) { var t = prev.AddTransition(first); t.hasExitTime = true; t.exitTime = 1f; t.duration = .1f; }
            if (first != null) sm.defaultState = first;
            return ctrl;
        }

        private static GameObject Place(string path, Vector3 position, RuntimeAnimatorController ctrl, string label)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            go.name = label; go.transform.position = position;
            if (ctrl != null)
            {
                var animator = go.GetComponent<Animator>();
                if (animator == null) animator = go.AddComponent<Animator>();
                animator.runtimeAnimatorController = ctrl;
                animator.applyRootMotion = false;
            }
            return go;
        }

        private static void BuildPreviewScene(AssetItem[] assets)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var light = new GameObject("Key light").AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.2f; light.color = new Color(1f, .93f, .82f); light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(45, -150, 0);
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.32f, .36f, .45f);
            var cam = new GameObject("Preview camera").AddComponent<Camera>();
            cam.transform.position = new Vector3(6, 5, -14); cam.transform.LookAt(new Vector3(6, 1, 6));
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.15f, .18f, .24f);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane); ground.name = "Ground"; ground.transform.localScale = new Vector3(8, 1, 8);
            ground.transform.position = new Vector3(10, 0, 20);
            // Reference characters for scale (the in-game prefabs).
            Place("Assets/Game/Content/Prefabs/CHR_Monkey_Prisoner.prefab", new Vector3(-3, 0, 0), null, "REF Monkey");
            Place("Assets/Game/Content/Prefabs/CHR_Police_Officer.prefab", new Vector3(-6, 0, 0), null, "REF Police");
            int row = 0;
            foreach (var group in assets.GroupBy(a => a.group))
            {
                var parent = new GameObject("Group " + group.Key).transform;
                float x = 0, z = row * 9f;
                foreach (var model in group.Where(a => a.kind == "generic_model"))
                {
                    var clips = group.Where(a => a.kind == "generic_clip" && a.avatar_from == model.name).OrderBy(a => a.loop ? 0 : 1).Select(a => ClipOf(Staging + a.name));
                    var go = Place(Staging + model.name, new Vector3(x, 0, z), Controller(group.Key, clips), Path.GetFileNameWithoutExtension(model.name));
                    if (go != null) go.transform.SetParent(parent); x += 5;
                }
                var humanoid = group.Where(a => a.kind == "humanoid").Select(a => ClipOf(Staging + a.name)).ToList();
                foreach (var clip in humanoid)
                {
                    var go = Place(Staging + "Characters/Monkey.fbx", new Vector3(x, 0, z), Controller(group.Key + "_" + clip.name, new[] { clip }), clip.name);
                    if (go != null) go.transform.SetParent(parent); x += 3.5f;
                }
                foreach (var prop in group.Where(a => a.kind == "prop" || a.kind == "prop_anim"))
                {
                    var ctrl = prop.kind == "prop_anim" ? Controller(Path.GetFileNameWithoutExtension(prop.name), new[] { ClipOf(Staging + prop.name) }) : null;
                    var go = Place(Staging + prop.name, new Vector3(x, 0, z), ctrl, Path.GetFileNameWithoutExtension(prop.name));
                    if (go != null) go.transform.SetParent(parent); x += 6;
                }
                row++;
            }
            Directory.CreateDirectory(Preview);
            EditorSceneManager.SaveScene(scene, PreviewScene);
            AssetDatabase.SaveAssets();
        }
    }
}
