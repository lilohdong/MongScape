using UnityEngine;

namespace Mongscape.Presentation.Map
{
    // The zoo's squeeze bars (an ArmsUp note): two barred walls slide in from both sides on the beat and leave a tall slot
    // only a body with the arms raised fits through. Yellow and black edges on the closing walls, and a sign above the slot
    // with a figure raising both arms, so the move reads from the shape alone. A hit springs the walls back open.
    public sealed class SqueezeGateView : MonoBehaviour
    {
        const float Height = 3.1f, OpenEdge = 1.55f, ClosedEdge = .3f, HalfWidth = 1.5f;
        Transform left, right;
        float shown = -1f;

        static Material iron, rust, hazard, dark, sign;
        static Material Mat(ref Material m, Color c, float smooth = .35f, float metal = .4f)
        {
            if (m == null)
            {
                m = new Material(RuntimeShader.Find(RuntimeShader.Lit)) { color = c };
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
                if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
            }
            return m;
        }

        public static SqueezeGateView Build()
        {
            var root = new GameObject("Zoo_SqueezeGate");
            var view = root.AddComponent<SqueezeGateView>();
            Mat(ref iron, new Color(.24f, .26f, .29f)); Mat(ref rust, new Color(.45f, .27f, .16f), .2f, .2f);
            Mat(ref hazard, new Color(1f, .78f, .06f), .3f, 0f); Mat(ref dark, new Color(.08f, .08f, .09f), .2f, 0f);
            Mat(ref sign, new Color(1f, .83f, .42f), .25f, 0f);
            view.left = view.Half(root.transform, -1); view.right = view.Half(root.transform, 1);
            // Fixed frame: two outer posts and a top beam (the walls ride under it).
            Box(root.transform, new Vector3(-3.15f, Height / 2, 0), new Vector3(.3f, Height, .3f), rust);
            Box(root.transform, new Vector3(3.15f, Height / 2, 0), new Vector3(.3f, Height, .3f), rust);
            Box(root.transform, new Vector3(0, Height + .12f, 0), new Vector3(6.6f, .24f, .34f), rust);
            // The sign above the slot: a figure with both arms raised on a round yellow plate.
            var plate = Prim(PrimitiveType.Cylinder, root.transform, new Vector3(0, Height + .78f, -.05f), new Vector3(1.05f, .04f, 1.05f), sign);
            plate.localRotation = Quaternion.Euler(90, 0, 0);
            Prim(PrimitiveType.Cylinder, root.transform, new Vector3(0, Height + .78f, -.02f), new Vector3(1.17f, .03f, 1.17f), dark).localRotation = Quaternion.Euler(90, 0, 0);
            var fig = new GameObject("ArmsUpFigure").transform; fig.SetParent(root.transform, false); fig.localPosition = new Vector3(0, Height + .74f, -.12f);
            Prim(PrimitiveType.Sphere, fig, new Vector3(0, .2f, 0), new Vector3(.13f, .13f, .04f), dark);
            Box(fig, new Vector3(0, -.02f, 0), new Vector3(.12f, .26f, .04f), dark);
            Box(fig, new Vector3(-.05f, -.27f, 0), new Vector3(.06f, .24f, .04f), dark).localRotation = Quaternion.Euler(0, 0, -8);
            Box(fig, new Vector3(.05f, -.27f, 0), new Vector3(.06f, .24f, .04f), dark).localRotation = Quaternion.Euler(0, 0, 8);
            Box(fig, new Vector3(-.12f, .2f, 0), new Vector3(.055f, .26f, .04f), dark).localRotation = Quaternion.Euler(0, 0, 18);
            Box(fig, new Vector3(.12f, .2f, 0), new Vector3(.055f, .26f, .04f), dark).localRotation = Quaternion.Euler(0, 0, -18);
            view.SetSqueeze(0f);
            return view;
        }

        Transform Half(Transform parent, int side)
        {
            // Built with its inner (closing) edge at local x 0, the bars reaching outward.
            var half = new GameObject(side < 0 ? "WallL" : "WallR").transform; half.SetParent(parent, false);
            float o = side;
            Box(half, new Vector3(o * HalfWidth / 2, .18f, 0), new Vector3(HalfWidth, .14f, .16f), iron);
            Box(half, new Vector3(o * HalfWidth / 2, Height - .2f, 0), new Vector3(HalfWidth, .14f, .16f), iron);
            Box(half, new Vector3(o * HalfWidth / 2, Height * .52f, 0), new Vector3(HalfWidth, .1f, .12f), iron);
            for (int i = 1; i <= 5; i++)
                Prim(PrimitiveType.Cylinder, half, new Vector3(o * i * .28f, Height / 2, 0), new Vector3(.08f, Height / 2 - .05f, .08f), iron);
            // The closing edge: a yellow post with black bands, the part that comes at the runner.
            Box(half, new Vector3(0, Height / 2, 0), new Vector3(.16f, Height - .1f, .22f), hazard);
            for (int b = 0; b < 6; b++)
                Box(half, new Vector3(0, .35f + b * .48f, 0), new Vector3(.17f, .18f, .23f), dark).localRotation = Quaternion.Euler(0, 0, o * 25);
            return half;
        }

        // 0 = wide open, 1 = closed to the slot.
        public void SetSqueeze(float s)
        {
            s = Mathf.Clamp01(s);
            if (Mathf.Approximately(s, shown)) return;
            shown = s;
            float edge = Mathf.Lerp(OpenEdge, ClosedEdge, s);
            left.localPosition = new Vector3(-edge, 0, 0); right.localPosition = new Vector3(edge, 0, 0);
        }

        static Transform Box(Transform parent, Vector3 pos, Vector3 size, Material m) => Prim(PrimitiveType.Cube, parent, pos, size, m);
        static Transform Prim(PrimitiveType type, Transform parent, Vector3 pos, Vector3 size, Material m)
        {
            var go = GameObject.CreatePrimitive(type);
            var c = go.GetComponent<Collider>(); if (c != null) Destroy(c);
            go.transform.SetParent(parent, false); go.transform.localPosition = pos; go.transform.localScale = size;
            var r = go.GetComponent<Renderer>(); r.sharedMaterial = m; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }
    }
}
