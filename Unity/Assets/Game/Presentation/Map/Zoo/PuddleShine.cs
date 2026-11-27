using UnityEngine;

namespace Mongscape.Presentation.Map
{
    // Makes a zoo puddle read as water to jump over at night: a pale moonlit surface over the dark stone, a bright wet rim,
    // ripple rings spreading from the middle and a few drops hopping up. Cheap: unlit-looking emissive pieces, no lights.
    public sealed class PuddleShine : MonoBehaviour
    {
        const int Ripples = 3, Drops = 5;
        static Material water, rim, ripple, drop;
        static Mesh ring;
        Transform[] ripples = new Transform[Ripples], drops = new Transform[Drops];
        Vector3[] dropHome = new Vector3[Drops];
        float rx, rz, seed;

        static Material Glow(ref Material m, Color baseColor, Color emission, float smooth)
        {
            if (m != null) return m;
            m = new Material(RuntimeShader.Find(RuntimeShader.Lit)) { color = baseColor };
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            m.EnableKeyword("_EMISSION"); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", emission);
            return m;
        }

        // Dresses a spawned puddle prop, sized to its renderers.
        public static void Attach(GameObject puddle)
        {
            if (puddle == null) return;
            var rs = puddle.GetComponentsInChildren<Renderer>();
            Bounds b = rs.Length > 0 ? rs[0].bounds : new Bounds(puddle.transform.position, new Vector3(1.6f, .05f, 2f));
            foreach (var r in rs) b.Encapsulate(r.bounds);
            var root = new GameObject("PuddleShine").AddComponent<PuddleShine>();
            root.transform.SetParent(puddle.transform, true);
            root.transform.SetPositionAndRotation(new Vector3(b.center.x, Mathf.Min(b.max.y, puddle.transform.position.y + .08f), b.center.z), Quaternion.identity);
            // Imported models can carry odd bounds (the 100x FBX import); keep the water to a lane-sized pool.
            float hx = Mathf.Clamp(b.extents.x, .6f, 1.4f), hz = Mathf.Clamp(b.extents.z, .6f, 1.4f);
            root.transform.localScale = new Vector3(1f / Mathf.Max(1e-4f, root.transform.parent.lossyScale.x), 1f / Mathf.Max(1e-4f, root.transform.parent.lossyScale.y), 1f / Mathf.Max(1e-4f, root.transform.parent.lossyScale.z));
            root.Build(hx, hz);
        }

        void Build(float halfX, float halfZ)
        {
            rx = halfX; rz = halfZ; seed = Random.value * 10f;
            Glow(ref rim, new Color(.75f, .93f, 1f), new Color(.3f, .62f, .85f) * .55f, .9f);
            Glow(ref water, new Color(.22f, .48f, .72f), new Color(.04f, .13f, .24f), .95f);
            Glow(ref ripple, new Color(.85f, .97f, 1f), new Color(.45f, .75f, .95f) * .6f, .9f);
            Glow(ref drop, new Color(.85f, .97f, 1f), new Color(.45f, .75f, .95f) * .5f, .9f);
            if (ring == null) ring = RingMesh(.86f, 1f, 40);
            // Bright wet rim (a ring just outside the water), then the moonlit surface inside it.
            Piece("Rim", ring, rim, new Vector3(0, .012f, 0), new Vector3(rx * 1.08f, 1, rz * 1.08f));
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Destroy(disc.GetComponent<Collider>());
            disc.name = "Surface"; disc.transform.SetParent(transform, false);
            disc.transform.localPosition = new Vector3(0, .008f, 0); disc.transform.localScale = new Vector3(rx * 1.86f, .004f, rz * 1.86f);
            Setup(disc.GetComponent<Renderer>(), water);
            for (int i = 0; i < Ripples; i++) ripples[i] = Piece("Ripple", ring, ripple, new Vector3(0, .016f, 0), Vector3.zero).transform;
            for (int i = 0; i < Drops; i++)
            {
                var d = GameObject.CreatePrimitive(PrimitiveType.Sphere); Destroy(d.GetComponent<Collider>());
                d.name = "Drop"; d.transform.SetParent(transform, false); d.transform.localScale = Vector3.one * .07f;
                float a = i * Mathf.PI * 2f / Drops + .4f;
                dropHome[i] = new Vector3(Mathf.Cos(a) * rx * .55f, 0, Mathf.Sin(a) * rz * .55f);
                Setup(d.GetComponent<Renderer>(), drop);
                drops[i] = d.transform;
            }
        }

        GameObject Piece(string name, Mesh mesh, Material m, Vector3 pos, Vector3 scale)
        {
            var go = new GameObject(name); go.transform.SetParent(transform, false);
            go.transform.localPosition = pos; go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            Setup(go.AddComponent<MeshRenderer>(), m);
            return go;
        }
        static void Setup(Renderer r, Material m)
        {
            r.sharedMaterial = m; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        }

        void Update()
        {
            float t = Time.time + seed;
            // Ripples: rings grow from the middle to the rim and thin out, one after another.
            for (int i = 0; i < Ripples; i++)
            {
                float k = Mathf.Repeat(t / 1.6f + i / (float)Ripples, 1f);
                float s = Mathf.Lerp(.12f, .92f, k);
                ripples[i].localScale = new Vector3(rx * s, 1, rz * s);
                ripples[i].gameObject.SetActive(k < .9f);
            }
            // Drops: short hops out of the water, each on its own beat.
            for (int i = 0; i < Drops; i++)
            {
                float k = Mathf.Repeat(t * 1.3f + i * .37f, 1f);
                float h = k < .45f ? Mathf.Sin(k / .45f * Mathf.PI) * .28f : 0f;
                drops[i].localPosition = dropHome[i] + Vector3.up * (h + .02f);
                drops[i].gameObject.SetActive(k < .45f);
            }
        }

        // A flat ring in XZ, inner and outer radius as fractions of 1.
        static Mesh RingMesh(float inner, float outer, int segments)
        {
            var v = new Vector3[segments * 2]; var tri = new int[segments * 12];
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments; float c = Mathf.Cos(a), s = Mathf.Sin(a);
                v[i * 2] = new Vector3(c * inner, 0, s * inner); v[i * 2 + 1] = new Vector3(c * outer, 0, s * outer);
                int n = (i + 1) % segments, o = i * 6;
                tri[o] = i * 2; tri[o + 1] = n * 2; tri[o + 2] = i * 2 + 1;
                tri[o + 3] = i * 2 + 1; tri[o + 4] = n * 2; tri[o + 5] = n * 2 + 1;
                // Both windings, so the ring shows whichever way is up.
                for (int k = 0; k < 6; k += 3) { int b = segments * 6 + o + k; tri[b] = tri[o + k]; tri[b + 1] = tri[o + k + 2]; tri[b + 2] = tri[o + k + 1]; }
            }
            var m = new Mesh { name = "PuddleRing", vertices = v, triangles = tri };
            // Explicit up normals: with both windings on shared vertices, recalculated normals cancel to zero and light as NaN.
            var up = new Vector3[v.Length]; for (int i = 0; i < up.Length; i++) up[i] = Vector3.up;
            m.normals = up; m.RecalculateBounds();
            return m;
        }
    }
}
