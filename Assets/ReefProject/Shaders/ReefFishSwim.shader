// Reef/FishSwim
// URP Simple Lit + a procedural swimming wiggle, for fish models that have no animation rig.
// Swap a fish's material to this shader (Tools > Reef Builder > "Make selected fish swim" does it
// for you and fits Head Z / Tail Z to the model), then tune the Swimming section.
// Receives shadows, fog and the main-light cookie (the animated caustics) like any URP material.
Shader "Reef/FishSwim"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)

        [Toggle(_ALPHATEST_ON)] _AlphaClip("Alpha Clipping (fins with cut-outs)", Float) = 0
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5

        [Toggle(_SPECULAR_COLOR)] _SpecularOn("Specular Highlights", Float) = 1
        _SpecColor("Specular Colour (alpha = smoothness)", Color) = (0.25, 0.25, 0.25, 0.55)

        [Toggle(_NORMALMAP)] _NormalMapOn("Use Normal Map", Float) = 0
        [NoScaleOffset] _BumpMap("Normal Map", 2D) = "bump" {}

        [Toggle(_EMISSION)] _EmissionOn("Emission", Float) = 0
        [HDR] _EmissionColor("Emission Colour", Color) = (0, 0, 0, 1)
        [NoScaleOffset] _EmissionMap("Emission Map", 2D) = "white" {}

        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull (Off for single-sided fins)", Float) = 2

        [Header(Swimming)]
        _SwimAmplitude("Tail Swing (fraction of body length)", Range(0, 0.3)) = 0.08
        _SwimSpeed("Tail Beat Speed", Range(0, 25)) = 7
        _SwimWaves("Waves Along Body", Range(0, 3)) = 0.8
        _SwimHeadZ("Nose Z (object space)", Float) = 0.5
        _SwimTailZ("Tail Z (object space)", Float) = -0.5
        _SwimTailBias("Tail Bias (higher = only the tail bends)", Range(0.1, 4)) = 1.6
        _SwimSway("Whole-Body Sway", Range(0, 0.3)) = 0.04
        [ToggleUI] _SwimVertical("Up-and-Down (whales)", Float) = 0

        [HideInInspector] _SpecGlossMap("Specular Map", 2D) = "white" {}
        [HideInInspector] _BumpScale("Scale", Float) = 1.0
        [HideInInspector] _QueueOffset("Queue offset", Float) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "SimpleLit"
            "IgnoreProjector" = "True"
            "Queue" = "Geometry"
        }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend One Zero
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex ReefFishForwardVertex
            #pragma fragment LitPassFragmentSimple

            // Material keywords
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _ _SPECGLOSSMAP _SPECULAR_COLOR

            // URP keywords (same set as URP's Simple Lit)
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            // Unity keywords
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fragment _ DEBUG_DISPLAY

            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer

            #define BUMP_SCALE_NOT_SUPPORTED 1

            #include "ReefFishInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/SimpleLitForwardPass.hlsl"

            Varyings ReefFishForwardVertex(Attributes input)
            {
                ReefApplySwim(input.positionOS.xyz, input.normalOS);
                return LitPassVertexSimple(input);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex ReefFishShadowVertex
            #pragma fragment ShadowPassFragment

            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "ReefFishInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"

            Varyings ReefFishShadowVertex(Attributes input)
            {
                ReefApplySwim(input.positionOS.xyz, input.normalOS);
                return ShadowPassVertex(input);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex ReefFishDepthVertex
            #pragma fragment DepthOnlyFragment

            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing

            #include "ReefFishInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"

            Varyings ReefFishDepthVertex(Attributes input)
            {
                ReefApplySwimPosition(input.position.xyz);
                return DepthOnlyVertex(input);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex ReefFishDepthNormalsVertex
            #pragma fragment DepthNormalsFragment

            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #pragma multi_compile_instancing

            #include "ReefFishInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/SimpleLitDepthNormalsPass.hlsl"

            Varyings ReefFishDepthNormalsVertex(Attributes input)
            {
                ReefApplySwim(input.positionOS.xyz, input.normal);
                return DepthNormalsVertex(input);
            }
            ENDHLSL
        }
    }

    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
