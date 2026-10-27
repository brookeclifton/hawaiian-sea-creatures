using UnityEngine;

namespace Reef
{
    public enum SpeciesCategory
    {
        Fish,
        Shark,
        SeaTurtle,
        Coral,
        Mollusk,
        Echinoderm,
        Crustacean,
        Algae,
        Artifact,
        Other
    }

    public enum NativeStatus
    {
        Unknown,
        Endemic,
        Indigenous,
        Introduced
    }

    /// <summary>
    /// Everything the info panel (and a future "basket" game) needs to know about one species
    /// or collectible. Make one per species: Project window > right-click > Create > Reef > Species Data.
    /// Several objects in the scene can share the same SpeciesData (e.g. every fish in a school).
    /// </summary>
    [CreateAssetMenu(fileName = "NewSpecies", menuName = "Reef/Species Data", order = 0)]
    public class SpeciesData : ScriptableObject
    {
        [Header("Names")]
        [Tooltip("Hawaiian name, with ʻokina and kahakō (e.g. humuhumunukunukuāpuaʻa). Shown as the title when present.")]
        public string hawaiianName;
        [Tooltip("English common name.")]
        public string commonName;
        [Tooltip("Scientific name (shown in italics).")]
        public string scientificName;

        [Header("Classification")]
        public SpeciesCategory category = SpeciesCategory.Fish;
        public NativeStatus nativeStatus = NativeStatus.Unknown;
        [Tooltip("Free text, e.g. 'Threatened (ESA)', 'Least Concern (IUCN)'. Leave blank to hide.")]
        public string conservationStatus;
        [Tooltip("Free text, e.g. 'Up to 30 cm'. Leave blank to hide.")]
        public string sizeText;

        [Header("Info panel text")]
        [TextArea(3, 10)] public string description;
        [TextArea(2, 6)] public string funFact;
        [Tooltip("Optional photo or illustration shown on the info panel.")]
        public Sprite image;
        [Tooltip("Optional recording of the Hawaiian name, played when the panel opens.")]
        public AudioClip namePronunciation;

        [Header("Game (optional)")]
        [Tooltip("Can this be added to the basket if collecting is turned on?")]
        public bool collectible = true;
        public int points = 1;

        [Header("Look")]
        [Tooltip("Accent colour for the highlight, reticle and panel header.")]
        public Color accentColor = new Color(1f, 0.82f, 0.35f, 1f);

        /// <summary>Hawaiian name if present, otherwise the common name.</summary>
        public string DisplayTitle => !string.IsNullOrWhiteSpace(hawaiianName) ? hawaiianName : (!string.IsNullOrWhiteSpace(commonName) ? commonName : name);

        /// <summary>The line under the title: the common name when the title is Hawaiian.</summary>
        public string DisplaySubtitle => !string.IsNullOrWhiteSpace(hawaiianName) ? commonName : string.Empty;

        public static string CategoryLabel(SpeciesCategory c)
        {
            switch (c)
            {
                case SpeciesCategory.SeaTurtle: return "Sea turtle";
                case SpeciesCategory.Mollusk: return "Mollusk / shell";
                case SpeciesCategory.Echinoderm: return "Echinoderm";
                default: return c.ToString();
            }
        }
    }
}
