using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Reef
{
    /// <summary>
    /// Animated caustics (the dancing light network on the seafloor) done the cheap way:
    /// a looping sequence of tileable caustic textures is played on the sun's LIGHT COOKIE.
    /// Every URP-lit surface (terrain, coral, rocks, fish, sharks) then shows moving caustics
    /// with correct stereo depth, at practically no cost.
    ///
    /// Put this on the Directional Light. The Reef Builder generates the frames for you
    /// (Assets/ReefProject/Generated/CausticFrames.asset); you can also drop in your own.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class CausticsAnimator : MonoBehaviour
    {
        public CausticFrameSet frames;
        [Tooltip("Playback speed in frames per second.")]
        public float framesPerSecond = 16f;
        [Tooltip("Size in metres that one caustic tile covers on the floor.")]
        public float tileSize = 5f;
        [Tooltip("Slow drift of the whole pattern, metres per second.")]
        public Vector2 drift = new Vector2(0.05f, 0.02f);

        Light sun;
        UniversalAdditionalLightData lightData;
        float time;
        int lastFrame = -1;

        void OnEnable()
        {
            sun = GetComponent<Light>();
            lightData = sun.GetUniversalAdditionalLightData();
            time = 0f;
            lastFrame = -1;
        }

        void OnDisable()
        {
            if (sun != null) sun.cookie = null;
        }

        void Update()
        {
            if (frames == null || frames.frames == null || frames.frames.Length == 0 || sun == null) return;

            time += Time.deltaTime;
            int n = frames.frames.Length;
            int idx = Mathf.FloorToInt(time * framesPerSecond) % n;
            if (idx != lastFrame)
            {
                sun.cookie = frames.frames[idx];
                lastFrame = idx;
            }

            if (lightData != null)
            {
                lightData.lightCookieSize = new Vector2(tileSize, tileSize);
                lightData.lightCookieOffset = drift * time;
            }
        }
    }
}
