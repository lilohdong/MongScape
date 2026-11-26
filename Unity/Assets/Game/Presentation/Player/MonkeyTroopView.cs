using System.Collections.Generic;
using Mongscape.Domain;
using Mongscape.Presentation.Map;
using UnityEngine;

namespace Mongscape.Presentation
{
    // The rescued monkeys running behind the runner (plan 4): at most MonkeyTroop.MaxVisible on screen, the rest is the
    // HUD number. A new monkey plays the rescue pop at its place in the troop, then runs; a monkey taken away (loyalty)
    // drops out. Five pooled models, one Animator each, culled when off screen, no shadows: light enough for the run.
    public sealed class MonkeyTroopView : MonoBehaviour
    {
        public const float PopSeconds = 1f;
        // Slots behind the runner: metres back and to the side, in two staggered rows.
        private static readonly Vector2[] Slots = { new Vector2(-.7f, 1.8f), new Vector2(.7f, 2.3f), new Vector2(0f, 3.1f), new Vector2(-1.1f, 3.8f), new Vector2(1.1f, 4.2f) };
        private RunSession session;
        private Transform hero;
        private readonly List<Follower> followers = new List<Follower>(MonkeyTroop.MaxVisible);
        private RuntimeAnimatorController pop, run;
        private int shownVersion = -1, shown;

        private sealed class Follower
        {
            public GameObject Root;
            public Animator Animator;
            public float PoppedAt = -1f;
            public bool Running;
        }

        public int Shown => shown;

        public void Configure(RunSession runSession, RunnerView runner)
        {
            session = runSession; hero = runner != null ? runner.transform : null;
            var art = MineArt.Load();
            pop = art != null ? art.RescuePop : null;
            if (pop != null && art.Run != null)
            {
                // A copy of the pop controller with every clip swapped for the run loop: no editor-only controller needed.
                var overrides = new AnimatorOverrideController(pop) { name = "TroopRun" };
                foreach (var clip in pop.animationClips) overrides[clip] = art.Run;
                run = overrides;
            }
            for (int i = 0; i < MonkeyTroop.MaxVisible; i++) followers.Add(Build(art, i));
        }

        private Follower Build(MineArt art, int index)
        {
            var follower = new Follower();
            if (art != null && art.Monkey != null)
            {
                follower.Root = Instantiate(art.Monkey, transform); follower.Root.name = "TroopMonkey_" + index;
                foreach (var c in follower.Root.GetComponentsInChildren<Collider>(true)) c.enabled = false;
                foreach (var b in follower.Root.GetComponentsInChildren<MonoBehaviour>(true)) b.enabled = false;
                follower.Animator = follower.Root.GetComponentInChildren<Animator>(true);
                if (follower.Animator != null) { follower.Animator.enabled = true; follower.Animator.applyRootMotion = false; follower.Animator.cullingMode = AnimatorCullingMode.CullCompletely; }
            }
            else
            {
                follower.Root = GameObject.CreatePrimitive(PrimitiveType.Capsule); follower.Root.name = "TroopMonkey_" + index;
                Destroy(follower.Root.GetComponent<Collider>()); follower.Root.transform.SetParent(transform, false);
                follower.Root.transform.localScale = new Vector3(.4f, .5f, .4f);
                var shader = RuntimeShader.Find("Universal Render Pipeline/Lit");
                if (shader != null) follower.Root.GetComponent<Renderer>().sharedMaterial = new Material(shader) { color = new Color(.55f, .36f, .2f) };
            }
            foreach (var r in follower.Root.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            follower.Root.SetActive(false);
            return follower;
        }

        private void Play(Follower follower, RuntimeAnimatorController controller)
        {
            if (follower.Animator == null || controller == null || follower.Animator.runtimeAnimatorController == controller) return;
            follower.Animator.runtimeAnimatorController = controller;
            follower.Animator.Rebind(); follower.Animator.Update(0f);
        }

        private void LateUpdate()
        {
            if (session == null || hero == null) return;
            var state = session.Flow.State;
            bool live = !session.IsTutorial && (state == GameFlowState.Running || state == GameFlowState.Caught);
            int target = live ? session.Troop.Visible : 0;
            if (session.Troop.Version != shownVersion || target != shown)
            {
                // A new monkey pops in at its slot; one taken away (or a new run) drops out from the back.
                for (int i = 0; i < followers.Count; i++)
                {
                    var f = followers[i]; bool on = i < target;
                    if (on && !f.Root.activeSelf)
                    {
                        f.Root.SetActive(true); f.Running = false;
                        f.PoppedAt = shownVersion >= 0 && live ? Time.time : -PopSeconds;
                        if (f.PoppedAt >= 0f) Play(f, pop); else { Play(f, run); f.Running = true; }
                        f.Root.transform.position = SlotPosition(i);
                    }
                    else if (!on && f.Root.activeSelf) f.Root.SetActive(false);
                }
                shown = target; shownVersion = session.Troop.Version;
            }
            if (shown == 0) return;
            float follow = 1f - Mathf.Exp(-10f * Time.deltaTime);
            for (int i = 0; i < shown; i++)
            {
                var f = followers[i];
                if (!f.Running && Time.time - f.PoppedAt >= PopSeconds) { Play(f, run); f.Running = true; }
                var t = f.Root.transform;
                // Popping monkeys stay put for the pop; the others catch up with their slot.
                if (f.Running) t.position = Vector3.Lerp(t.position, SlotPosition(i), follow);
                t.rotation = hero.rotation;
                if (f.Animator != null) f.Animator.speed = state == GameFlowState.Running && !session.Paused ? 1f : 0f;
            }
        }

        private Vector3 SlotPosition(int index)
        {
            var slot = Slots[index];
            var p = hero.position + hero.right * slot.x - hero.forward * slot.y;
            // On the route surface behind the runner, not in the runner's air (jumps, the cart).
            RouteSurface.Sample(p.z, out _, out float y);
            p.y = y; return p;
        }
    }
}
