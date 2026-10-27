using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Reef.EditorTools
{
    /// <summary>
    /// Tools > Reef > Set Up Recipe Game
    ///
    /// Creates the six recipe assets in Assets/ReefRecipes (pictures from Assets/ReefRecipes/Images),
    /// links their ingredients to your species, and adds ReefRecipeGame to CC_HUB.
    /// Safe to run again: existing recipe assets are kept (only empty picture / ingredient slots are filled),
    /// so edits your team makes to them are never overwritten.
    /// Also run automatically at the end of "Set Up My Species".
    /// </summary>
    public static class ReefRecipeSetup
    {
        public const string Folder = "Assets/ReefRecipes";
        const string Images = Folder + "/Images";
        const string Sp = ReefSpeciesSetup.Folder;

        struct Ing
        {
            public string label; public int count; public string[] species;
            public Ing(string label, int count, params string[] species) { this.label = label; this.count = count; this.species = species; }
        }

        struct Def
        {
            public string file, hawaiian, english, image; public Ing[] ingredients;
        }

        static string Info(string file) => Sp + "/" + file + "/" + file + "_Info.asset";

        static List<Def> Recipes()
        {
            string tooth = ReefBuilder.SpeciesDir + "/TigerSharkTooth.asset";
            string humu = ReefBuilder.SpeciesDir + "/Humuhumunukunukuapuaa.asset";
            return new List<Def>
            {
                new Def { file = "Leiomano", hawaiian = "Leiomano", english = "Shark tooth weapon", image = "leiomano",
                          ingredients = new[] { new Ing("Tiger shark tooth", 1, tooth), new Ing("Driftwood", 1, Info("driftwood")) } },
                new Def { file = "Poke", hawaiian = "", english = "Hawaiian-style poke", image = "poke",
                          ingredients = new[] { new Ing("ʻAhi (tuna)", 1, Info("tuna")), new Ing("Limu (seaweed)", 1, Info("seaweed")) } },
                new Def { file = "LeiPupu", hawaiian = "Lei pūpū", english = "Shell necklace", image = "leiPupu",
                          ingredients = new[] { new Ing("Leho (cowrie shell)", 1, Info("cowrieShell")),
                                                new Ing("Cone shell", 1, Info("coneShell"), Info("snailShell")) } },
                new Def { file = "Luhee", hawaiian = "Luheʻe", english = "Octopus lure", image = "luhee",
                          ingredients = new[] { new Ing("Leho (cowrie shell)", 1, Info("cowrieShell")), new Ing("Pōhaku (lava rock)", 1, Info("lavaRock")),
                                                new Ing("Driftwood", 1, Info("driftwood")), new Ing("Niu (coconut)", 1, Info("coconut")) } },
                new Def { file = "Ia", hawaiian = "Iʻa", english = "Sashimi", image = "ia",
                          ingredients = new[] { new Ing("Any fish", 1, Info("tuna"), Info("parrotfish"), Info("coralFish"), humu, Info("humuhumu")) } },
                new Def { file = "LauAiLimu", hawaiian = "Lau ʻai limu", english = "Seaweed salad", image = "lauAiLimu",
                          ingredients = new[] { new Ing("Limu (seaweed)", 3, Info("seaweed")) } },
            };
        }

        [MenuItem("Tools/Reef/Set Up Recipe Game", priority = 11)]
        public static void RunFromMenu()
        {
            string msg = Run();
            EditorUtility.DisplayDialog("Recipe game", msg, "OK");
        }

        /// <summary>Returns a short report.</summary>
        public static string Run()
        {
            ReefBuilder.EnsureFolder(Folder);
            var recipes = new List<ReefRecipe>();
            var missing = new List<string>();

            foreach (var def in Recipes())
            {
                string path = Folder + "/" + def.file + ".asset";
                var r = AssetDatabase.LoadAssetAtPath<ReefRecipe>(path);
                bool isNew = r == null;
                if (isNew)
                {
                    r = ScriptableObject.CreateInstance<ReefRecipe>();
                    r.hawaiianName = def.hawaiian;
                    r.englishName = def.english;
                    r.ingredients = new List<RecipeIngredient>();
                    foreach (var i in def.ingredients) r.ingredients.Add(new RecipeIngredient { label = i.label, count = i.count });
                    AssetDatabase.CreateAsset(r, path);
                }

                if (r.image == null) r.image = LoadImage(def.image);
                if (r.image == null) missing.Add("picture " + def.image + ".jpg");

                // Fill in species for any ingredient that has none yet (e.g. models added later).
                for (int k = 0; k < def.ingredients.Length && k < r.ingredients.Count; k++)
                {
                    var ing = r.ingredients[k];
                    if (ing.accepts == null) ing.accepts = new List<SpeciesData>();
                    ing.accepts.RemoveAll(x => x == null);
                    foreach (var sp in def.ingredients[k].species)
                    {
                        var d = AssetDatabase.LoadAssetAtPath<SpeciesData>(sp);
                        if (d != null && !ing.accepts.Contains(d)) ing.accepts.Add(d);
                    }
                    if (ing.accepts.Count == 0) missing.Add(r.DisplayTitle + ": " + ing.label);
                }
                EditorUtility.SetDirty(r);
                recipes.Add(r);
            }
            AssetDatabase.SaveAssets();

            string report = "Recipes: " + recipes.Count + " in " + Folder + ".";
            var hub = ReefBuilder.FindHub();
            if (hub == null) report += "\nCC_HUB not found in the open scene, so ReefRecipeGame was not added. Open the reef scene and run Tools > Reef > Set Up Recipe Game.";
            else
            {
                var game = hub.GetComponent<ReefRecipeGame>();
                if (game == null) game = Undo.AddComponent<ReefRecipeGame>(hub);
                game.recipes = recipes;
                EditorUtility.SetDirty(game);
                EditorSceneManager.MarkSceneDirty(hub.scene);
                report += "\nReefRecipeGame is on CC_HUB. Save the scene and press Play.";
            }
            if (missing.Count > 0) report += "\n\nStill missing: " + string.Join(", ", missing);
            Debug.Log("[Reef] " + report.Replace("\n", " "));
            return report;
        }

        static Texture2D LoadImage(string name)
        {
            foreach (var ext in new[] { ".jpg", ".jpeg", ".png" })
            {
                string p = Images + "/" + name + ext;
                var imp = AssetImporter.GetAtPath(p) as TextureImporter;
                if (imp == null) continue;
                if (imp.textureType != TextureImporterType.Default || imp.maxTextureSize != 1024 || imp.wrapMode != TextureWrapMode.Clamp)
                {
                    imp.textureType = TextureImporterType.Default;
                    imp.maxTextureSize = 1024;
                    imp.wrapMode = TextureWrapMode.Clamp;
                    imp.mipmapEnabled = true;
                    imp.SaveAndReimport();
                }
                return AssetDatabase.LoadAssetAtPath<Texture2D>(p);
            }
            return null;
        }
    }
}
