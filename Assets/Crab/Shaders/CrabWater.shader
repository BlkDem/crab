// The sea: gentle rolling waves in the vertices, a colour that deepens away from the shore,
// the sky reflected at grazing angles (fresnel), the sun's glitter, and foam where the water
// meets the ground (from the depth texture). Fog-aware.
Shader "Crab/Water"
{
    Properties
    {
        _Shallow("Shallow", Color) = (0.25, 0.75, 0.75, 1)
        _Deep("Deep", Color) = (0.03, 0.22, 0.35, 1)
        _Sky("Sky", Color) = (0.7, 0.85, 0.95, 1)
        _Foam("Foam", Color) = (0.95, 0.97, 1, 1)
        _WaveHeight("Wave height", Float) = 0.12
        _WaveScale("Wave scale", Float) = 0.25
        _Clarity("Depth to deep colour (m)", Float) = 3
        _Alpha("Alpha", Range(0, 1)) = 0.88
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Shallow, _Deep, _Sky, _Foam;
                half _WaveHeight, _WaveScale, _Clarity, _Alpha;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; };
            struct V { float4 positionCS : SV_POSITION; float3 ws : TEXCOORD0; float3 n : TEXCOORD1; float4 screen : TEXCOORD2; float fog : TEXCOORD3; };

            float3 Waves(float3 p, out float3 n)
            {
                float t = _Time.y;
                float2 d1 = normalize(float2(0.3, 1)), d2 = normalize(float2(-0.7, 0.6)), d3 = normalize(float2(0.9, 0.2));
                float k = _WaveScale;
                float w1 = dot(d1, p.xz) * k + t * 0.9, w2 = dot(d2, p.xz) * k * 1.7 + t * 1.3, w3 = dot(d3, p.xz) * k * 3.1 + t * 2.1;
                float h = sin(w1) * 1.0 + sin(w2) * 0.5 + sin(w3) * 0.22;
                float dx = cos(w1) * d1.x * k + cos(w2) * d2.x * k * 1.7 * 0.5 + cos(w3) * d3.x * k * 3.1 * 0.22;
                float dz = cos(w1) * d1.y * k + cos(w2) * d2.y * k * 1.7 * 0.5 + cos(w3) * d3.y * k * 3.1 * 0.22;
                n = normalize(float3(-dx * _WaveHeight, 1, -dz * _WaveHeight));
                return float3(p.x, p.y + h * _WaveHeight, p.z);
            }

            V vert(A i)
            {
                V o;
                float3 ws = TransformObjectToWorld(i.positionOS.xyz);
                float3 n;
                ws = Waves(ws, n);
                o.ws = ws;
                o.n = n;
                o.positionCS = TransformWorldToHClip(ws);
                o.screen = ComputeScreenPos(o.positionCS);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(V i) : SV_Target
            {
                float3 n = normalize(i.n);
                float3 v = GetWorldSpaceNormalizeViewDir(i.ws);
                float2 suv = i.screen.xy / i.screen.w;
                float sceneZ = LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams);
                float waterZ = LinearEyeDepth(i.positionCS.z, _ZBufferParams);
                float depth = max(0, sceneZ - waterZ);
                float deep = saturate(depth / _Clarity);
                float3 col = lerp(_Shallow.rgb, _Deep.rgb, deep);
                float fres = pow(1 - saturate(dot(n, v)), 4);
                col = lerp(col, _Sky.rgb, fres * 0.6);
                Light L = GetMainLight(TransformWorldToShadowCoord(i.ws));
                float3 h = normalize(L.direction + v);
                float spec = pow(saturate(dot(n, h)), 180) * 3;
                col = col * (0.45 + 0.55 * saturate(dot(n, L.direction)) * L.color) + spec * L.color * L.shadowAttenuation;
                // foam on the shore line, broken up by a moving pattern
                float t = _Time.y;
                float pattern = sin(i.ws.x * 3.1 + t * 1.3) * sin(i.ws.z * 2.7 - t * 0.9) * 0.5 + 0.5;
                float foam = saturate(1 - depth / 0.6) * (0.6 + 0.4 * pattern);
                col = lerp(col, _Foam.rgb, foam * 0.85);
                float alpha = saturate(_Alpha * (0.35 + deep) + foam * 0.5);
                return half4(MixFog(col, i.fog), alpha);
            }
            ENDHLSL
        }
    }
}
