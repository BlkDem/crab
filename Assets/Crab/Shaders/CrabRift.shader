// The membrane of a rift: a slow green vortex with bright veins and a burning rim, drawn on an
// upright disc (uv 0..1 across it).
Shader "Crab/Rift"
{
    Properties
    {
        _Color("Colour", Color) = (0.45, 1, 0.65, 1)
        _Core("Core colour", Color) = (0.02, 0.06, 0.04, 1)
        _Speed("Speed", Float) = 0.6
        _Intensity("Intensity", Float) = 2.5
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color, _Core;
                half _Speed, _Intensity;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float vnoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(hash(i), hash(i + float2(1, 0)), f.x), lerp(hash(i + float2(0, 1)), hash(i + 1), f.x), f.y);
            }
            V vert(A i)
            {
                V o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.uv = i.uv;
                return o;
            }
            half4 frag(V i) : SV_Target
            {
                float2 p = i.uv * 2 - 1;
                float r = length(p);
                if (r > 1) discard;
                float a = atan2(p.y, p.x);
                float t = _Time.y * _Speed;
                float swirl = a + (1 - r) * 4 + t;
                float n = vnoise(float2(swirl * 2.5, r * 6 - t * 2)) * 0.6 + vnoise(float2(swirl * 6, r * 14 - t * 3)) * 0.4;
                float veins = smoothstep(0.62, 0.8, n);
                float rim = smoothstep(0.75, 0.98, r) * (0.6 + 0.4 * vnoise(float2(a * 8, t * 4)));
                float3 col = lerp(_Core.rgb, _Color.rgb * _Intensity, saturate(veins * (1.2 - r) + rim));
                col += _Color.rgb * pow(1 - r, 3) * 0.6 * (0.7 + 0.3 * sin(t * 6));
                float alpha = saturate(1 - pow(r, 6));
                return half4(col * alpha, alpha * 0.92);
            }
            ENDHLSL
        }
    }
}
