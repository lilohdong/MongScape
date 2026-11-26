using UnityEngine;

namespace Mongscape.Presentation
{
    // Shaders for materials made in code. Shader.Find returns null in a player build for a shader no built material uses
    // (URP Unlit was stripped and broke the bootstrap), and `??` does not see a destroyed Unity object as null, so every
    // runtime material goes through here: the first shader found by name, else the shader of a primitive's default
    // material, else the error shader. Never null.
    public static class RuntimeShader
    {
        // URP Lit is used by the project's materials, so it is always in the build.
        public const string Lit = "Universal Render Pipeline/Lit";
        private static Shader primitive;
        public static Shader Find(params string[] names)
        {
            foreach (var name in names)
            {
                var shader = Shader.Find(name);
                if (shader != null) return shader;
            }
            return Fallback();
        }
        // Prefers a loaded shader (a Resources shader), else the named ones.
        public static Shader Find(Shader preferred, params string[] names) => preferred != null ? preferred : Find(names);
        private static Shader Fallback()
        {
            if (primitive != null) return primitive;
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var material = cube.GetComponent<Renderer>().sharedMaterial;
            primitive = material != null && material.shader != null ? material.shader : Shader.Find("Hidden/InternalErrorShader");
            if (UnityEngine.Application.isPlaying) Object.Destroy(cube); else Object.DestroyImmediate(cube);
            return primitive;
        }
    }
}
