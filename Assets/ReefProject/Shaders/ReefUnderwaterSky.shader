// Reef/UnderwaterSky
// Skybox for being underwater: bright toward the surface, water-coloured at the horizon
// (UnderwaterEnvironment keeps this equal to the fog colour so distant things blend in),
// dark below, plus the soft bright "Snell's window" overhead.
Shader "Reef/UnderwaterSky"
{
    Properties
    {
        _TopColor("Toward Surface", Color) = (0.30, 0.70, 0.80, 1)
        _HorizonColor("Horizon (match fog)", Color) = (0.06, 0.34, 0.44, 1)
        _BottomColor("Below", Color) = (0.01, 0.08, 0.14, 1)
        _TopExponent("Top Blend", Range(0.2, 8)) = 1.4
        _BottomExponent("Bottom Blend", Range(0.2, 8)) = 0.9
        [HDR] _WindowColor("Snell's Window Colour", Color) = (0.85, 1, 1, 1)
        _WindowAngle("Snell's Window Half-Angle (deg)", Range(10, 70)) = 48.6
        _WindowSoftness("Window Softness", Range(0.01, 0.5)) = 0.18
        _WindowIntensity("Window Intensity", Range(0, 3)) = 0.7
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Background"
            "PreviewType" = "Skybox"
            "RenderPipeline" = "UniversalPipeline"
        }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor;
                half4 _HorizonColor;
                half4 _BottomColor;
                half _TopExponent;
                half _BottomExponent;
                half4 _WindowColor;
                half _WindowAngle;
                half _WindowSoftness;
                half _WindowIntensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 dir        : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.dir = v.positionOS.xyz;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float y = d.y;
                half3 col = lerp(_HorizonColor.rgb, _TopColor.rgb, pow(saturate(y), _TopExponent));
                col = lerp(col, _BottomColor.rgb, pow(saturate(-y), _BottomExponent));

                float cosWindow = cos(radians(_WindowAngle));
                float window = smoothstep(cosWindow - _WindowSoftness, cosWindow + 0.02, y);
                float ripple = 0.85 + 0.15 * sin(atan2(d.z, d.x) * 14.0 + _Time.y * 0.8);
                col += _WindowColor.rgb * (window * _WindowIntensity * ripple);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
