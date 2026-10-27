using TMPro;
using UnityEngine;

namespace Reef
{
    /// <summary>
    /// The game's UI typeface: Prompt (Google Fonts, SIL Open Font License, see
    /// Resources/Fonts/Prompt-OFL.txt). It covers the ʻokina and kahakō.
    ///
    /// The TextMesh Pro font asset is made at runtime from Resources/Fonts/Prompt-Regular.ttf
    /// (dynamic atlas, glyphs are added as they're needed), so there is no font asset to keep
    /// up to date. Bold and italic are drawn by TextMesh Pro from the regular face, which keeps
    /// the draw-on-top materials in ReefUI working on every word.
    /// </summary>
    public static class ReefFonts
    {
        const string PromptPath = "Fonts/Prompt-Regular";
        static TMP_FontAsset s_Prompt;
        static bool s_Tried;

        /// <summary>Prompt as a TMP font asset, or null if the font file is missing.</summary>
        public static TMP_FontAsset Prompt
        {
            get
            {
                if (s_Prompt != null || s_Tried) return s_Prompt;
                s_Tried = true;
                var ttf = Resources.Load<Font>(PromptPath);
                if (ttf == null)
                {
                    Debug.LogWarning("[Reef] Resources/" + PromptPath + ".ttf not found, using the default TextMesh Pro font.");
                    return null;
                }
                s_Prompt = TMP_FontAsset.CreateFontAsset(ttf, 90, 9, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024);
                if (s_Prompt != null)
                {
                    s_Prompt.name = "Prompt (runtime)";
                    // Anything Prompt lacks falls back to the project's default font.
                    var fallback = TMP_Settings.defaultFontAsset;
                    if (fallback != null && fallback != s_Prompt)
                    {
                        if (s_Prompt.fallbackFontAssetTable == null) s_Prompt.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset>();
                        s_Prompt.fallbackFontAssetTable.Add(fallback);
                    }
                }
                return s_Prompt;
            }
        }

        /// <summary>The font to use: Prompt when asked for and available, otherwise the one given.</summary>
        public static TMP_FontAsset Pick(bool usePrompt, TMP_FontAsset assigned)
        {
            if (usePrompt && Prompt != null) return Prompt;
            return assigned;
        }
    }
}
