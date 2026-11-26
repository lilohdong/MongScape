// Round rear-view mirror for the zoo lion (plan 1-2a): the rear camera's texture cut to a circle, with a rim that turns red
// as the lion closes in (_Danger 0..1).
Shader "Mongscape/ZooMirror"
{
    Properties { _MainTex ("Texture", 2D) = "white" {} _Danger ("Danger", Range(0,1)) = 0 }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" }
        ZWrite Off ZTest Always Cull Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex; float _Danger;
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert (appdata_img v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord; return o; }
            fixed4 frag (v2f i) : SV_Target
            {
                float2 d = i.uv - .5; float r = length(d) * 2;
                if (r > 1) discard;
                fixed4 c = tex2D(_MainTex, i.uv);
                float rimWidth = lerp(.08, .16, _Danger);
                float rim = smoothstep(1 - rimWidth, 1 - rimWidth + .02, r);
                fixed3 rimColor = lerp(fixed3(.92, .92, .9), fixed3(.95, .12, .08), _Danger);
                c.rgb = lerp(c.rgb, rimColor, rim);
                c.a = 1 - smoothstep(.97, 1, r);
                return c;
            }
            ENDCG
        }
    }
}
