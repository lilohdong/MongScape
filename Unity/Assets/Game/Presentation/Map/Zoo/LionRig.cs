using Mongscape.Domain;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Mongscape.Presentation.Map
{
    // Plays the lion's art clips (ZooContent) on the lion model. The caller gives the action and its time every frame, so the
    // animation follows the song clock and stops with the pause.
    public sealed class LionRig : MonoBehaviour
    {
        private PlayableGraph graph;
        private AnimationPlayableOutput output;
        private AnimationClip[] clips;
        private AnimationClipPlayable[] states;
        public LionAction Current { get; private set; }

        // Clips in the order of LionAction; a missing one leaves the model in its last pose.
        public void Configure(params AnimationClip[] actions)
        {
            Release();
            var animator = GetComponent<Animator>();
            if (animator == null) animator = gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = null; animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            clips = actions; states = new AnimationClipPlayable[actions.Length];
            graph = PlayableGraph.Create("Lion"); graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            output = AnimationPlayableOutput.Create(graph, "Lion", animator);
            for (int i = 0; i < actions.Length; i++) if (actions[i] != null) states[i] = AnimationClipPlayable.Create(graph, actions[i]);
        }
        // Run loops; the others hold their last frame.
        public void Show(LionAction action, float time)
        {
            int i = (int)action;
            if (!graph.IsValid() || i >= clips.Length || clips[i] == null) return;
            Current = action;
            float length = clips[i].length;
            output.SetSourcePlayable(states[i]);
            states[i].SetTime(action == LionAction.Run ? Mathf.Repeat(time, length) : Mathf.Clamp(time, 0f, length - .001f));
            graph.Evaluate(0f);
        }
        private void Release() { if (graph.IsValid()) graph.Destroy(); }
        private void OnDestroy() => Release();
    }
}
