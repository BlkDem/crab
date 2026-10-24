// A plain sky: a vertical gradient from the horizon to the zenith, a darker ground below, and a
// soft glow around the sun. For skies the procedural one cannot tint (the red sky of Wrath).
Shader "Crab/Sky"
{
    Properties
    {
        _Top("Zenith", Color) = (0.35, 0.12, 0.08, 1)
        _Horizon("Horizon", Color) = (1, 0.55, 0.3, 1)
        _Bottom("Below the horizon", Color) = (0.2, 0.07, 0.04, 1)
        _Sun("Sun colour", Color) = (1, 0.75, 0.45, 1)
        _SunDir("Sun direction (to the sun)", Vector) = (0, 0.3, 1, 0)
        _SunSize("Sun glow size", Float) = 6
        _Exponent("Gradient curve", Float) = 0.6
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Top, _Horizon, _Bottom, _Sun;
                float4 _SunDir;
                half _SunSize, _Exponent;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; };
            struct V { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };
            V vert(A i)
            {
                V o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.dir = i.positionOS.xyz;
                return o;
            }
            half4 frag(V i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float up = d.y;
                float3 col = up > 0 ? lerp(_Horizon.rgb, _Top.rgb, pow(saturate(up), _Exponent)) : lerp(_Horizon.rgb, _Bottom.rgb, saturate(-up * 8));
                float s = saturate(dot(d, normalize(_SunDir.xyz)));
                col += _Sun.rgb * (pow(s, _SunSize * 40) * 6 + pow(s, _SunSize) * 0.35);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
