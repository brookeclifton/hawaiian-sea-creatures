using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Reef
{
    /// <summary>
    /// The underwater "look" in one place: water colour + fog, ambient light from above,
    /// the skybox gradient, sun strength, and the settings every CC_CANOE camera needs.
    ///
    /// It also darkens and deepens the water colour as the viewer swims deeper, which is a
    /// big part of feeling "in the ocean".
    ///
    /// Camera settings are applied to ALL cameras under CC_HUB (CenterCamera, left and right eye)
    /// because CC_CANOE renders each eye with its own camera:
    ///  - sensible near/far clip planes (the CC defaults of 0.01 / 100000 cause z-fighting on coral)
    ///  - post-processing switched on (bloom and colour grading from the Reef volume)
    /// </summary>
    [ExecuteAlways]
    [DefaultExecutionOrder(-40)]
    public class UnderwaterEnvironment : MonoBehaviour
    {
        [Header("Water")]
        [Tooltip("World height of the ocean surface.")]
        public float surfaceHeight = 12f;
        [Tooltip("Fog colour near the surface.")]
        public Color shallowWater = new Color(0.07f, 0.36f, 0.45f, 1f);
        [Tooltip("Fog colour at 'deep' depth.")]
        public Color deepWater = new Color(0.02f, 0.15f, 0.25f, 1f);
        [Tooltip("Depth (m below the surface) at which the water reaches the deep colour.")]
        public float deepAt = 25f;
        [Tooltip("Exponential fog density. 0.05 = you can see ~30-40 m, typical of clear Hawaiian water.")]
        public float fogDensity = 0.05f;
        [Tooltip("Fog density multiplier at 'deep' depth.")]
        public float deepFogMultiplier = 1.25f;

        [Header("Ambient light (from above)")]
        public Color ambientSky = new Color(0.30f, 0.62f, 0.72f, 1f);
        public Color ambientEquator = new Color(0.10f, 0.33f, 0.40f, 1f);
        public Color ambientGround = new Color(0.03f, 0.10f, 0.13f, 1f);
        [Tooltip("Ambient brightness at 'deep' depth, relative to the surface.")]
        [Range(0f, 1f)] public float deepAmbient = 0.6f;

        [Header("Sun")]
        public Light sun;
        public float sunIntensity = 1.8f;
        [Range(0f, 1f)] public float deepSun = 0.55f;

        [Header("Sky")]
        [Tooltip("Material using Reef/UnderwaterSky. Its horizon colour is kept equal to the fog colour.")]
        public Material skyboxMaterial;
        public Color skyTop = new Color(0.30f, 0.70f, 0.80f, 1f);
        public Color skyBottom = new Color(0.01f, 0.07f, 0.12f, 1f);

        [Header("Cameras (all cameras under CC_HUB)")]
        public bool applyCameraSettings = true;
        public float nearClip = 0.05f;
        [Tooltip("Keep this just past where the fog becomes fully opaque.")]
        public float farClip = 120f;
        public bool enablePostProcessing = true;

        ReefCCRig rig;
        Material skyInstance;
        bool camerasApplied;

        static readonly int TopId = Shader.PropertyToID("_TopColor");
        static readonly int HorizonId = Shader.PropertyToID("_HorizonColor");
        static readonly int BottomId = Shader.PropertyToID("_BottomColor");

        void OnEnable()
        {
            camerasApplied = false;
            Apply(true);
        }

        void OnDisable()
        {
            if (skyInstance != null)
            {
                if (Application.isPlaying) Destroy(skyInstance); else DestroyImmediate(skyInstance);
                skyInstance = null;
            }
        }

        void LateUpdate()
        {
            Apply(false);
        }

        void OnValidate()
        {
            camerasApplied = false;
        }

        void Apply(bool force)
        {
            if (rig == null) rig = ReefCCRig.Find();

            float headY = rig != null ? rig.ViewOrigin.y : surfaceHeight - 5f;
            float depth = Mathf.Max(0f, surfaceHeight - headY);
            float t = deepAt > 0.01f ? Mathf.Clamp01(depth / deepAt) : 0f;

            Color water = Color.Lerp(shallowWater, deepWater, t);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = water;
            RenderSettings.fogDensity = fogDensity * Mathf.Lerp(1f, deepFogMultiplier, t);

            float amb = Mathf.Lerp(1f, deepAmbient, t);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = ambientSky * amb;
            RenderSettings.ambientEquatorColor = ambientEquator * amb;
            RenderSettings.ambientGroundColor = ambientGround * amb;

            if (sun != null) sun.intensity = sunIntensity * Mathf.Lerp(1f, deepSun, t);

            if (skyboxMaterial != null)
            {
                // Use an instance at runtime so the asset on disk isn't modified every frame.
                Material sky = skyboxMaterial;
                if (Application.isPlaying)
                {
                    if (skyInstance == null) skyInstance = new Material(skyboxMaterial) { name = skyboxMaterial.name + " (runtime)" };
                    sky = skyInstance;
                }
                sky.SetColor(TopId, Color.Lerp(skyTop, skyTop * 0.5f, t));
                sky.SetColor(HorizonId, water);
                sky.SetColor(BottomId, skyBottom);
                if (RenderSettings.skybox != sky) RenderSettings.skybox = sky;
            }

            if (applyCameraSettings && (!camerasApplied || force)) ApplyCameras(water);
        }

        void ApplyCameras(Color water)
        {
            if (rig == null || rig.cameras == null) return;
            foreach (var cam in rig.cameras)
            {
                if (cam == null) continue;
                cam.nearClipPlane = nearClip;
                cam.farClipPlane = farClip;
                cam.backgroundColor = water;
                if (skyboxMaterial == null) cam.clearFlags = CameraClearFlags.SolidColor;
                var data = cam.GetUniversalAdditionalCameraData();
                if (data != null) data.renderPostProcessing = enablePostProcessing;
            }
            camerasApplied = true;
        }
    }
}
