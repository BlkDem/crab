// Lava: slow-moving plates of dark crust with glowing cracks between them, bright where it is
// molten. Drawn in world space on any flat mesh (a river strip, a lake), with fog.
Shader "Crab/Lava"
{
    Properties
    {
        _Hot("Molten colour", Color) = (1, 0.42, 0.08, 1)
        _Crust("Crust colour", Color) = (0.12, 0.05, 0.03, 1)
        _Glow("Glow", Float) = 3
        _Scale("Scale", Float) = 0.35
        _Flow("Flow (xz speed)", Vector) = (0, 0.15, 0, 0)
        _Crusted("Crusted share", Range(0, 1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Hot, _Crust;
                half _Glow, _Scale, _Crusted;
                float4 _Flow;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; };
            struct V { float4 positionCS : SV_POSITION; float3 world : TEXCOORD0; float fog : TEXCOORD1; };
            float2 hash2(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }
            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float vnoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(hash(i), hash(i + float2(1, 0)), f.x), lerp(hash(i + float2(0, 1)), hash(i + 1), f.x), f.y);
            }
            // distance to the nearest cell edge (crust plates and the cracks between them)
            float edges(float2 p, float t)
            {
                float2 i = floor(p), f = frac(p);
                float d1 = 8, d2 = 8;
                for (int y = -1; y <= 1; y++)
                for (int x = -1; x <= 1; x++)
                {
                    float2 g = float2(x, y);
                    float2 o = hash2(i + g);
                    o = 0.5 + 0.4 * sin(t + 6.2831 * o);
                    float d = length(g + o - f);
                    if (d < d1) { d2 = d1; d1 = d; } else if (d < d2) d2 = d;
                }
                return d2 - d1;
            }
            V vert(A i)
            {
                V o;
                o.world = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.world);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }
            float fbm(float2 p)
            {
                return vnoise(p) * 0.5 + vnoise(p * 2.1 + 5.3) * 0.3 + vnoise(p * 4.3 + 1.7) * 0.2;
            }
            half4 frag(V i) : SV_Target
            {
                float t = _Time.y;
                float2 w = i.world.xz * _Scale;
                float2 p = w - _Flow.xy * t * _Scale;
                float2 warp = float2(fbm(p * 0.6 + t * 0.04), fbm(p * 0.6 + 17 - t * 0.035)) - 0.5;
                // molten lava: slow streaks of brighter and cooler flow
                float flow = fbm(p * float2(1.4, 0.5) + warp * 1.5 + float2(0, t * 0.08));
                float molten = 0.45 + 0.75 * smoothstep(0.25, 0.75, flow);
                // plates of crust drifting on it, cracked, glowing at the seams
                float e = edges(p * 1.3 + warp * 0.9, t * 0.25);
                float big = fbm(w * 0.18 + warp * 0.3 + t * 0.01);
                float crusted = smoothstep(_Crusted - 0.12, _Crusted + 0.06, big);
                float crack = 1 - smoothstep(0.015, 0.12, e);
                float heat = lerp(molten, crack * 0.9 + 0.08, crusted);
                float pulse = 0.88 + 0.12 * sin(t * 1.3 + big * 15);
                float3 crust = _Crust.rgb * (0.6 + 0.8 * vnoise(p * 5)) * (1 + 0.5 * (1 - smoothstep(0, 0.3, e)));
                float3 hot = lerp(_Hot.rgb * 0.7, float3(1, 0.8, 0.35), saturate(heat - 0.6) * 1.6) * _Glow * heat * pulse;
                float3 col = lerp(crust, hot, saturate(heat * 1.4 - crusted * 0.2));
                col = MixFog(col, i.fog);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
