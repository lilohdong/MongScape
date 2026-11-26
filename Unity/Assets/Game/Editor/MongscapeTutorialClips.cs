using System.Linq;
using Mongscape.Domain;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Mongscape.Editor
{
    // Puts the 11 demo motions of art round 2 plus the X and the T of the pause and upper-body jump into Monkey.controller as states
    // (named like the clips), so the tutorial's mirror monkey can play them. Safe to run again.
    public static class MongscapeTutorialClips
    {
        const string Controller = "Assets/Game/Content/Animations/Monkey.controller", Characters = "Assets/External/Staging/Blender/Characters";

        [MenuItem("Mongscape/Add Tutorial Demo Clips")]
        public static void Apply()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Controller);
            if (controller == null) { Debug.LogError("Monkey.controller is missing"); return; }
            var machine = controller.layers[0].stateMachine; int added = 0;
            foreach (string name in TutorialScript.AllClips)
            {
                string state = TutorialScript.ClipPrefix + name, path = Characters + "/" + state + ".fbx";
                var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__"));
                if (clip == null) { Debug.LogWarning("No clip in " + path); continue; }
                var existing = machine.states.FirstOrDefault(s => s.state.name == state).state;
                if (existing == null) { existing = machine.AddState(state); added++; }
                existing.motion = clip;
            }
            EditorUtility.SetDirty(controller); AssetDatabase.SaveAssets();
            Debug.Log("MONGSCAPE_TUTORIAL_CLIPS added " + added + " states");
        }
    }
}
