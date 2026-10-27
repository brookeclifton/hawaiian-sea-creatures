using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Reef.EditorTools
{
    /// <summary>Tools > Reef Builder: one window to build and rebuild the reef.</summary>
    public class ReefBuilderWindow : EditorWindow
    {
        ReefBuildSettings settings;
        Vector2 scroll;
        string lastMessage;

        [MenuItem("Tools/Reef Builder", priority = 0)]
        public static void Open()
        {
            var w = GetWindow<ReefBuilderWindow>("Reef Builder");
            w.minSize = new Vector2(380, 520);
        }

        void OnEnable()
        {
            settings = ReefBuilder.LoadOrCreateSettings();
        }

        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);

            EditorGUILayout.LabelField("Hawaiian Reef Builder", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Builds a medium, bounded spur-and-groove reef with placeholder species into the open scene, " +
                                    "and sets up CC_HUB for swimming and selecting. Everything goes under 'REEF (generated)'; " +
                                    "each step only replaces its own part.", MessageType.None);

            using (new EditorGUILayout.HorizontalScope())
            {
                settings = (ReefBuildSettings)EditorGUILayout.ObjectField("Settings", settings, typeof(ReefBuildSettings), false);
                if (GUILayout.Button("Edit", GUILayout.Width(50)) && settings != null) Selection.activeObject = settings;
            }
            if (settings == null)
            {
                if (GUILayout.Button("Create settings")) settings = ReefBuilder.LoadOrCreateSettings();
                EditorGUILayout.EndScrollView();
                return;
            }

            EditorGUILayout.Space();
            var scene = SceneManager.GetActiveScene();
            var hub = ReefBuilder.FindHub();
            EditorGUILayout.LabelField("Scene", string.IsNullOrEmpty(scene.path) ? "(unsaved)" : scene.path);
            EditorGUILayout.LabelField("CC_HUB", hub != null ? "found" : "NOT FOUND - open a CC_CANOE scene");
            if (scene.path != ReefBuilder.ScenePath)
            {
                EditorGUILayout.HelpBox("Recommended first: save this as your own scene so the CC_CANOE demo scene stays untouched.", MessageType.Info);
                if (GUILayout.Button("Save scene as " + ReefBuilder.ScenePath)) Run(() => { ReefBuilder.SaveSceneAsReefScene(); return "Scene saved as " + ReefBuilder.ScenePath + "."; });
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Build steps", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(hub == null))
            {
                if (Step("1  Set up CC_HUB", "Adds SwimController, SpeciesSelector and friends to CC_HUB, disables DPadFlyer/Waypointer, fixes camera clip planes, creates bounds + spawn point."))
                {
                    Run(() => { ReefBuilder.SetupRig(settings, out string m); return m; });
                }
                if (Step("2  Build seafloor", "Terrain with sand, spur-and-groove reef ridges and patch reefs, painted sand / reef rock / rubble."))
                {
                    Run(() => { ReefBuilder.BuildTerrain(settings); return "Seafloor built."; });
                }
                if (Step("3  Scatter coral, rock and sea-floor items", "Places the layers from the settings (your prefabs, or placeholders)."))
                {
                    Run(() => { ReefBuilder.Scatter(settings, ReefBuilder.FindTerrain()); return "Reef scattered."; });
                }
                if (Step("4  Underwater look", "Sun + animated caustics, fog and water colour, sky, post-processing, light shafts, surface, marine snow."))
                {
                    Run(() => { ReefBuilder.BuildAtmosphere(settings, ReefBuilder.FindTerrain()); return "Underwater look built."; });
                }
                if (Step("5  Sea life", "Tiger shark and turtles on swim paths, fish schools and territorial fish."))
                {
                    Run(() => { ReefBuilder.BuildLife(settings, ReefBuilder.FindTerrain()); return "Sea life added."; });
                }

                EditorGUILayout.Space();
                GUI.backgroundColor = new Color(0.6f, 0.9f, 1f);
                if (GUILayout.Button("Build everything (1-5)", GUILayout.Height(34)))
                {
                    Run(() => { ReefBuilder.BuildAll(settings); return "Reef built. Save the scene (Ctrl/Cmd+S), then press Play."; });
                }
                GUI.backgroundColor = Color.white;
            }

            if (GUILayout.Button("Remove generated reef"))
            {
                Run(() =>
                {
                    if (EditorUtility.DisplayDialog("Remove generated reef", "Delete the 'REEF (generated)' object? (Undo works.)", "Remove", "Cancel"))
                    {
                        ReefBuilder.RemoveGenerated();
                        return "Generated reef removed.";
                    }
                    return lastMessage;
                });
            }

            if (!string.IsNullOrEmpty(lastMessage)) EditorGUILayout.HelpBox(lastMessage, MessageType.Info);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Bringing in your own models", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Select a model in the scene, then:", MessageType.None);
            if (GUILayout.Button("Make selected fish swim (Reef/FishSwim shader)")) Run(() => { ReefModelTools.MakeSelectedSwim(); return "Done - see the Console for details."; });
            if (GUILayout.Button("Make selected selectable")) Run(() => { ReefModelTools.MakeSelectedSelectable(); return "Done - see the Console for details."; });
            if (GUILayout.Button("Give selected a swim path")) Run(() => { ReefModelTools.AddSwimPathToSelected(); return "Done - see the Console for details."; });

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Controls (Play mode)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Left stick: swim    Right stick: look    RT / LT: rise / sink    Click left stick: boost\n" +
                "A: learn more    B: close    D-pad left/right: cycle targets    Hold View: back to start\n" +
                "Keyboard: WASD swim, arrows look, Space/C rise/sink, Shift boost, E examine, Q close, Tab cycle, hold R reset",
                MessageType.None);

            EditorGUILayout.EndScrollView();
        }

        /// <summary>Run heavy work after this GUI pass (progress bars and dialogs break IMGUI layout mid-pass).</summary>
        void Run(System.Func<string> work)
        {
            EditorApplication.delayCall += () =>
            {
                try { lastMessage = work(); }
                catch (System.Exception e) { lastMessage = "Error: " + e.Message; Debug.LogException(e); }
                Repaint();
            };
        }

        static bool Step(string title, string help)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                bool clicked = GUILayout.Button(title, GUILayout.Height(24));
                EditorGUILayout.LabelField(help, EditorStyles.wordWrappedMiniLabel);
                return clicked;
            }
        }
    }
}
