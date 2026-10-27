// Reef/SoftParticle
// Round, soft-edged particle with no texture needed. Used for marine snow (the drifting specks
// that give the water depth and motion in stereo). Fades into the water with distance fog.
Shader "Reef/SoftParticle"
{
    Properties
    {
        _Color("Tint", Color) = (1, 1, 1, 1)
        _Softness("Edge Softness", Range(0.05, 1)) = 0.7
        _FogInfluence("Fog Influence", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Softness;
                half _FogInfluence;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color       : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color       : COLOR;
                float2 uv         : TEXCOORD0;
                half fogFactor    : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.color = v.color * _Color;
                o.uv = v.uv;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 c = i.uv * 2.0 - 1.0;
                half r = length(c);
                half a = saturate((1.0 - r) / max(_Softness, 0.01));
                a *= a;
                half4 col = i.color;
                col.a *= a;
                half3 fogged = MixFog(col.rgb, i.fogFactor);
                col.rgb = lerp(col.rgb, fogged, _FogInfluence);
                return col;
            }
            ENDHLSL
        }
    }
}
