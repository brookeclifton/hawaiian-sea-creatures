using System;
using System.Collections.Generic;
using UnityEngine;

namespace Reef
{
    [Serializable]
    public class RecipeIngredient
    {
        [Tooltip("What the tracker shows, e.g. \"Limu (seaweed)\".")]
        public string label = "Ingredient";
        [Tooltip("How many must be found. Once this many are found, no more of these can be selected.")]
        [Min(1)] public int count = 1;
        [Tooltip("Species that count for this ingredient (e.g. every fish for \"Any fish\").")]
        public List<SpeciesData> accepts = new List<SpeciesData>();

        public bool Accepts(SpeciesData d) => d != null && accepts != null && accepts.Contains(d);
    }

    /// <summary>
    /// One recipe in the collecting game: a picture, a name, and the ingredients to find on the reef.
    /// Make one with Project window > right-click > Create > Reef > Recipe, then add it to the
    /// Recipes list on ReefRecipeGame (on CC_HUB).
    /// </summary>
    [CreateAssetMenu(fileName = "NewRecipe", menuName = "Reef/Recipe", order = 1)]
    public class ReefRecipe : ScriptableObject
    {
        [Tooltip("Hawaiian name, with ʻokina and kahakō. Shown as the title when present.")]
        public string hawaiianName;
        [Tooltip("English name (shown under the Hawaiian name).")]
        public string englishName;
        [Tooltip("Picture for the start screen, the tracker and the Maikaʻi! screen.")]
        public Texture2D image;
        public List<RecipeIngredient> ingredients = new List<RecipeIngredient>();

        public string DisplayTitle => !string.IsNullOrWhiteSpace(hawaiianName) ? hawaiianName : (!string.IsNullOrWhiteSpace(englishName) ? englishName : name);
        public string DisplaySubtitle => !string.IsNullOrWhiteSpace(hawaiianName) ? englishName : string.Empty;

        /// <summary>"Tiger shark tooth · Driftwood", with ×3 for multiples.</summary>
        public string IngredientSummary
        {
            get
            {
                var parts = new List<string>();
                if (ingredients != null)
                    foreach (var i in ingredients)
                        if (i != null) parts.Add(i.count > 1 ? i.label + " ×" + i.count : i.label);
                return string.Join("  ·  ", parts);
            }
        }
    }
}
