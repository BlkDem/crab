// Alpha-blended soft motes (dust, mist, splashes): vertex colour, round falloff.
Shader "Crab/Soft"
{
    Properties { _Tint("Tint", Color) = (1, 1, 1, 1) }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+10" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct V { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; float fog : TEXCOORD1; };
            V vert(A i)
            {
                V o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.color = i.color * _Tint;
                o.uv = i.uv;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }
            half4 frag(V i) : SV_Target
            {
                half d = length(i.uv - 0.5) * 2;
                half a = i.color.a * saturate(1 - d * d);
                return half4(MixFog(i.color.rgb, i.fog), a);
            }
            ENDHLSL
        }
    }
}
