// Additive glow for particles, tracers, lightning and the lantern's beam: vertex colour times
// tint; particles (billboards) are soft round motes, lines a soft strip; meshes fade at the rim.
Shader "Crab/Glow"
{
    Properties
    {
        _Tint("Tint", Color) = (1, 1, 1, 1)
        [Toggle] _Round("Round motes (particles)", Float) = 1
        [Toggle] _Rim("Fade at grazing angles (beam)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+20" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _Round, _Rim;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct V { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; float3 n : TEXCOORD1; float3 v : TEXCOORD2; };
            V vert(A i)
            {
                V o;
                float3 ws = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(ws);
                o.color = i.color * _Tint;
                o.uv = i.uv;
                o.n = TransformObjectToWorldNormal(i.normalOS);
                o.v = GetWorldSpaceNormalizeViewDir(ws);
                return o;
            }
            half4 frag(V i) : SV_Target
            {
                half a = i.color.a;
                if (_Round > 0.5) a *= saturate(1 - length(i.uv - 0.5) * 2);
                if (_Rim > 0.5) a *= pow(saturate(abs(dot(normalize(i.n), i.v))), 1.5);
                return half4(i.color.rgb, a);
            }
            ENDHLSL
        }
    }
}
