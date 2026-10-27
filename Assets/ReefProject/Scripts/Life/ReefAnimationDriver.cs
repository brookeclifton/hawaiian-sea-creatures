using UnityEngine;

namespace Reef
{
    /// <summary>
    /// Makes an imported swim animation loop and lets PathSwimmer / FishSchool speed it up or slow
    /// it down with the animal's swimming speed. Works with both animation systems:
    ///  - Animator (Mecanim), e.g. FBX models with an Animator Controller
    ///  - legacy Animation, which is what the glTF importer (glTFast) creates by default for GLB files
    /// The Species Setup tool adds this to rigged animals automatically.
    /// </summary>
    public class ReefAnimationDriver : MonoBehaviour
    {
        [Tooltip("Prefer the clip whose name contains this text (e.g. 'swim'). Empty = first clip.")]
        public string preferredClip = "swim";
        [Tooltip("Also play shape-key clips (names starting with 'Key') at the same time.")]
        public bool playShapeKeyClips = true;

        Animator animator;
        Animation legacy;

        void Awake()
        {
            animator = GetComponentInChildren<Animator>();
            legacy = GetComponentInChildren<Animation>();
        }

        void Start()
        {
            if (legacy == null) return;
            legacy.wrapMode = WrapMode.Loop;
            AnimationState pick = null;
            foreach (AnimationState st in legacy)
            {
                st.wrapMode = WrapMode.Loop;
                bool isKey = st.name.StartsWith("Key");
                if (isKey) continue;
                if (pick == null) pick = st;
                if (!string.IsNullOrEmpty(preferredClip) && st.name.ToLowerInvariant().Contains(preferredClip.ToLowerInvariant())) { pick = st; break; }
            }
            if (pick != null) legacy.Play(pick.name);
            if (playShapeKeyClips)
                foreach (AnimationState st in legacy)
                    if (st.name.StartsWith("Key")) legacy.Blend(st.name, 1f, 0f);
        }

        /// <summary>1 = normal speed.</summary>
        public void SetSpeed(float speed)
        {
            if (animator != null) animator.speed = speed;
            if (legacy != null) foreach (AnimationState st in legacy) st.speed = speed;
        }
    }
}
