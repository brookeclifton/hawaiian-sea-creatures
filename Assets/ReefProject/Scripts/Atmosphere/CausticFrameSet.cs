using UnityEngine;

namespace Reef
{
    /// <summary>A looping sequence of tileable caustic textures for CausticsAnimator.</summary>
    [CreateAssetMenu(fileName = "CausticFrames", menuName = "Reef/Caustic Frame Set", order = 20)]
    public class CausticFrameSet : ScriptableObject
    {
        public Texture2D[] frames;
    }
}
