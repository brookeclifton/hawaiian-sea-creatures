// Reef/HighlightRim
// Additive glowing rim drawn over a selected species (SelectableSpecies adds a "shell" renderer
// with this material). It repeats the FishSwim wiggle so the glow stays glued to swimming fish.
Shader "Reef/HighlightRim"
{
    Properties
    {
        [HDR] _RimColor("Rim Colour", Color) = (1, 0.82, 0.35, 1)
        _RimPower("Rim Tightness", Range(0.5, 8)) = 2.2
        _RimIntensity("Intensity", Range(0, 4)) = 1
        _FillAmount("Inner Glow", Range(0, 1)) = 0.1
        _PulseSpeed("Pulse Speed", Float) = 3

        [Header(Swim wiggle (copied from the fish material))]
        _SwimAmplitude("Tail Swing", Float) = 0
        _SwimSpeed("Tail Beat Speed", Float) = 7
        _SwimWaves("Waves Along Body", Float) = 0.8
        _SwimHeadZ("Nose Z", Float) = 0.5
        _SwimTailZ("Tail Z", Float) = -0.5
        _SwimTailBias("Tail Bias", Float) = 1.6
        _SwimSway("Body Sway", Float) = 0.04
        _SwimVertical("Up-and-Down", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+50"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Rim"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Back
            Offset -1, -1

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _RimColor;
                half _RimPower;
                half _RimIntensity;
                half _FillAmount;
                half _PulseSpeed;
                float _SwimAmplitude;
                float _SwimSpeed;
                float _SwimWaves;
                float _SwimHeadZ;
                float _SwimTailZ;
                float _SwimTailBias;
                float _SwimSway;
                float _SwimVertical;
            CBUFFER_END

            #include "ReefSwim.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 viewDirWS  : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 p = v.positionOS.xyz;
                float3 n = v.normalOS;
                ReefApplySwim(p, n);
                VertexPositionInputs vp = GetVertexPositionInputs(p);
                o.positionCS = vp.positionCS;
                o.normalWS = TransformObjectToWorldNormal(n);
                o.viewDirWS = GetWorldSpaceViewDir(vp.positionWS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float3 v = normalize(i.viewDirWS);
                half ndv = saturate(abs(dot(n, v)));
                half rim = pow(1.0 - ndv, _RimPower);
                half pulse = 0.8 + 0.2 * sin(_Time.y * _PulseSpeed);
                half a = (rim + _FillAmount) * _RimIntensity * pulse;
                return half4(_RimColor.rgb * a, 0.0);
            }
            ENDHLSL
        }
    }
}
