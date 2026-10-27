// Reef/WaterSurface
// The underside of the ocean surface, seen from below. Inside Snell's window (roughly 48 degrees
// from straight up) you see the bright sky; outside it the surface acts like a dark mirror.
// Two scrolling layers of the caustic texture make it ripple.
Shader "Reef/WaterSurface"
{
    Properties
    {
        _WindowColor("Sky Through Window", Color) = (0.75, 0.95, 1, 1)
        _MirrorColor("Mirror (outside window)", Color) = (0.04, 0.22, 0.30, 1)
        _WindowAngle("Snell's Window Half-Angle (deg)", Range(20, 70)) = 48.6
        _WindowSoftness("Window Softness", Range(0.01, 0.4)) = 0.08
        _RippleTex("Ripple Texture", 2D) = "white" {}
        _RippleScale("Ripple Scale", Float) = 0.12
        _RippleSpeed("Ripple Speed", Float) = 0.02
        _RippleStrength("Ripple Strength", Range(0, 1)) = 0.45
        _Brightness("Brightness", Range(0, 4)) = 1.3
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry+10"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Surface"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _WindowColor;
                half4 _MirrorColor;
                half _WindowAngle;
                half _WindowSoftness;
                float4 _RippleTex_ST;
                float _RippleScale;
                float _RippleSpeed;
                half _RippleStrength;
                half _Brightness;
            CBUFFER_END

            TEXTURE2D(_RippleTex);
            SAMPLER(sampler_RippleTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half fogFactor    : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs vp = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = vp.positionCS;
                o.positionWS = vp.positionWS;
                o.fogFactor = ComputeFogFactor(vp.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 viewRay = normalize(i.positionWS - GetCameraPositionWS());
                float cosWindow = cos(radians(_WindowAngle));
                half window = smoothstep(cosWindow - _WindowSoftness, cosWindow + _WindowSoftness, viewRay.y);

                float t = _Time.y * _RippleSpeed;
                float2 uv = i.positionWS.xz * _RippleScale;
                half r1 = SAMPLE_TEXTURE2D(_RippleTex, sampler_RippleTex, uv + float2(t, t * 0.6)).r;
                half r2 = SAMPLE_TEXTURE2D(_RippleTex, sampler_RippleTex, uv * 1.7 + float2(-t * 1.3, t * 0.4)).r;
                half ripple = saturate(r1 * r2 * 2.0);

                half3 col = lerp(_MirrorColor.rgb, _WindowColor.rgb * _Brightness, window);
                col *= (1.0 - _RippleStrength) + _RippleStrength * (0.5 + ripple);
                col = MixFog(col, i.fogFactor);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
