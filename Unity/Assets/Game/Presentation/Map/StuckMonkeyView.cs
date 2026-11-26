using UnityEngine;

namespace Mongscape.Presentation.Map
{
    // A monkey stuck in a broken mine wall (plan 3-0): it waves from the hole until the runner reaches for it, then pops out
    // toward the track and runs off to join the troop. A missed monkey keeps waving as the runner passes (no penalty).
    public sealed class StuckMonkeyView : MonoBehaviour
    {
        // The broken wall stands this far from the route centre, just past the outer lane.
        public const float WallX = 4.1f;
        public const float PopSeconds = 1.4f;
        private GameObject wallLeft, wallRight, monkey;
        private Transform anchorLeft, anchorRight;
        private Animator animator;
        private AudioSource voice;
        private MineArt art;
        private float poppedAt = -1f;
        private Vector3 monkeyHome;
        public int Sequence { get; private set; }
        public int Side { get; private set; }
        public bool Popped => poppedAt >= 0f;

        public static StuckMonkeyView Build(MineArt art)
        {
            var root = new GameObject("StuckMonkey");
            var view = root.AddComponent<StuckMonkeyView>(); view.art = art;
            view.wallLeft = Wall(art != null ? art.WallLeft : null, root.transform, "BrokenWall_L", out view.anchorLeft);
            view.wallRight = Wall(art != null ? art.WallRight : null, root.transform, "BrokenWall_R", out view.anchorRight);
            if (art != null && art.Monkey != null)
            {
                view.monkey = Instantiate(art.Monkey, root.transform); view.monkey.name = "WallMonkey";
                foreach (var c in view.monkey.GetComponentsInChildren<Collider>(true)) c.enabled = false;
                foreach (var b in view.monkey.GetComponentsInChildren<MonoBehaviour>(true)) b.enabled = false;
                view.animator = view.monkey.GetComponentInChildren<Animator>(true);
                if (view.animator != null) { view.animator.enabled = true; view.animator.applyRootMotion = false; view.animator.cullingMode = AnimatorCullingMode.CullCompletely; }
            }
            else
            {
                view.monkey = GameObject.CreatePrimitive(PrimitiveType.Capsule); view.monkey.name = "WallMonkey";
                Destroy(view.monkey.GetComponent<Collider>()); view.monkey.transform.SetParent(root.transform, false);
                view.monkey.transform.localScale = new Vector3(.5f, .6f, .5f);
                view.monkey.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(.55f, .36f, .2f) };
            }
            if (art != null && art.Rescue != null)
            {
                view.voice = root.AddComponent<AudioSource>(); view.voice.playOnAwake = false; view.voice.spatialBlend = 0; view.voice.clip = art.Rescue;
            }
            root.SetActive(false);
            return view;
        }
        private static GameObject Wall(GameObject model, Transform parent, string name, out Transform anchor)
        {
            GameObject wall;
            if (model != null)
            {
                wall = Instantiate(model, parent); wall.name = name;
                foreach (var c in wall.GetComponentsInChildren<Collider>(true)) Destroy(c);
            }
            else
            {
                wall = new GameObject(name); wall.transform.SetParent(parent, false);
                var slab = GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(slab.GetComponent<Collider>());
                slab.transform.SetParent(wall.transform, false); slab.transform.localPosition = new Vector3(0, 1.6f, .35f); slab.transform.localScale = new Vector3(3f, 3.2f, .5f);
                slab.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(.72f, .67f, .6f) };
            }
            anchor = FindDeep(wall.transform, "MonkeyAnchor");
            if (anchor == null) { anchor = new GameObject("MonkeyAnchor").transform; anchor.SetParent(wall.transform, false); }
            wall.SetActive(false);
            return wall;
        }
        private static Transform FindDeep(Transform node, string name)
        {
            if (node.name == name) return node;
            for (int i = 0; i < node.childCount; i++) { var hit = FindDeep(node.GetChild(i), name); if (hit != null) return hit; }
            return null;
        }

        // Stands the wall on the side of the route at this point, its front (the hole) toward the track.
        public void Place(Vector3 routePoint, int side, int sequence)
        {
            Sequence = sequence; Side = side; poppedAt = -1f;
            transform.SetPositionAndRotation(routePoint + Vector3.right * side * WallX, Quaternion.Euler(0, side < 0 ? 90 : -90, 0));
            wallLeft.SetActive(side < 0); wallRight.SetActive(side > 0);
            var anchor = side < 0 ? anchorLeft : anchorRight;
            monkey.transform.SetParent(anchor, false);
            monkey.transform.localPosition = Vector3.zero; monkey.transform.localRotation = Quaternion.identity;
            monkeyHome = monkey.transform.localPosition;
            monkey.SetActive(true); gameObject.SetActive(true);
            Play(side < 0 ? art?.WaveRight : art?.WaveLeft);
        }
        public void Rescue()
        {
            if (Popped) return;
            poppedAt = Time.time;
            Play(art?.RescuePop);
            if (voice != null) voice.Play();
        }
        private void Play(RuntimeAnimatorController controller)
        {
            if (animator == null || controller == null) return;
            animator.runtimeAnimatorController = controller;
            animator.Rebind(); animator.Update(0f);
        }
        private void Update()
        {
            if (!Popped || monkey == null) return;
            float t = (Time.time - poppedAt) / PopSeconds;
            // Out of the hole toward the track (the anchor faces it), then gone to the troop behind the runner.
            monkey.transform.localPosition = monkeyHome + Vector3.forward * Mathf.SmoothStep(0, 1.6f, Mathf.Clamp01(t * 1.6f));
            if (t >= 1f) monkey.SetActive(false);
        }
    }
}
