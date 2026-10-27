// Reef/LightShaft
// Cheap "god ray" for a tall, open cone mesh hanging from the surface. Additive, soft at the
// silhouette, fades at the top/bottom, fades out as you swim into it, softens where it meets the
// seafloor (uses the camera depth texture) and fades into the water with fog.
// Because the shafts are real geometry they have correct depth on the stereo wall.
Shader "Reef/LightShaft"
{
    Properties
    {
        [HDR] _Color("Colour", Color) = (0.55, 0.85, 1, 1)
        _Intensity("Intensity", Range(0, 2)) = 0.12
        _EdgePower("Edge Softness", Range(0.5, 8)) = 2.5
        _TopFade("Top Fade", Range(0.001, 1)) = 0.08
        _BottomFade("Bottom Fade", Range(0.001, 1)) = 0.65
        _NearFade("Fade When Closer Than (m)", Float) = 4
        _SoftDepth("Soft Floor Contact (m)", Float) = 1.5
        _StreakCount("Streaks Around", Float) = 5
        _StreakSpeed("Streak Drift Speed", Float) = 0.06
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent-10"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Shaft"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity;
                half _EdgePower;
                half _TopFade;
                half _BottomFade;
                float _NearFade;
                float _SoftDepth;
                float _StreakCount;
                float _StreakSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float2 uv         : TEXCOORD2;
                half fogFactor    : TEXCOORD3;
                float phase       : TEXCOORD4;
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
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv;
                o.fogFactor = ComputeFogFactor(vp.positionCS.z);
                float3 objPos = GetObjectToWorldMatrix()._m03_m13_m23;
                o.phase = frac(dot(objPos, float3(0.137, 0.271, 0.193))) * 6.2831853;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 V = normalize(GetWorldSpaceViewDir(i.positionWS));
                float3 N = normalize(i.normalWS);
                half edge = pow(saturate(abs(dot(N, V))), _EdgePower);

                half along = i.uv.y; // 0 = top (surface), 1 = bottom
                half vfade = smoothstep(0.0, _TopFade, along) * (1.0 - smoothstep(1.0 - _BottomFade, 1.0, along));

                float t = _Time.y;
                half streak = 0.6 + 0.4 * sin(i.uv.x * 6.2831853 * _StreakCount + t * _StreakSpeed * 6.2831853 + i.phase
                                              + 1.5 * sin(i.uv.x * 17.0 + t * 0.3));
                half flicker = 0.85 + 0.15 * sin(t * 0.7 + i.phase);

                float camDist = distance(i.positionWS, GetCameraPositionWS());
                half nearFade = saturate((camDist - _NearFade * 0.35) / max(_NearFade, 0.01));

                float2 suv = GetNormalizedScreenSpaceUV(i.positionCS);
                float sceneEye = LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams);
                float fragEye = -TransformWorldToView(i.positionWS).z;
                half soft = saturate((sceneEye - fragEye) / max(_SoftDepth, 0.01));

                half a = _Intensity * edge * vfade * streak * flicker * nearFade * soft;
                half3 col = _Color.rgb * a;
                col = MixFogColor(col, half3(0, 0, 0), i.fogFactor);
                return half4(col, 0);
            }
            ENDHLSL
        }
    }
}
