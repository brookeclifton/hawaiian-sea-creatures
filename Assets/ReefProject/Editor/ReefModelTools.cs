using UnityEditor;
using UnityEngine;

namespace Reef.EditorTools
{
    /// <summary>
    /// Helpers for bringing Sketchfab models into the reef. Select model(s) in the scene and use
    /// Tools > Reef > ... (or the buttons at the bottom of the Reef Builder window).
    /// </summary>
    public static class ReefModelTools
    {
        [MenuItem("Tools/Reef/Make Selected Fish Swim", priority = 20)]
        public static void MakeSelectedSwim()
        {
            var selection = Selection.gameObjects;
            if (selection.Length == 0) { Debug.LogWarning("[Reef] Select a fish model in the scene first."); return; }
            var shader = Shader.Find("Reef/FishSwim");
            if (shader == null) { Debug.LogError("[Reef] Shader 'Reef/FishSwim' not found. Check the Console for shader errors."); return; }

            string dir = ReefBuilder.GenMats + "/Swim";
            ReefBuilder.EnsureFolder(dir);
            int converted = 0;

            foreach (var go in selection)
            {
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    if (r is SkinnedMeshRenderer)
                    {
                        Debug.Log("[Reef] '" + r.name + "' is rigged. Use its own swim animation (Animator) instead of the FishSwim wiggle.", r);
                        continue;
                    }
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) continue;
                    Bounds b = mf.sharedMesh.bounds;
                    if (b.size.z < b.size.x || b.size.z < b.size.y)
                    {
                        Debug.LogWarning("[Reef] '" + r.name + "': this mesh isn't longest along its own Z axis. FishSwim bends the fish along object-space Z " +
                                         "(nose toward +Z). Re-export it from Maya facing +Z with frozen transforms (see README), or the wiggle will look wrong.", r);
                    }

                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        var src = mats[i];
                        if (src == null) continue;
                        if (src.shader == shader)
                        {
                            Undo.RecordObject(src, "Fit swim");
                            ReefBuilder.ConfigureFishMaterial(src, b, src.GetFloat("_SwimAmplitude"), src.GetFloat("_SwimSpeed"), src.GetFloat("_SwimWaves"), src.GetFloat("_SwimTailBias"));
                            continue;
                        }

                        var m = new Material(shader) { name = src.name + "_Swim" };
                        string texProp = src.HasProperty("_BaseMap") ? "_BaseMap" : (src.HasProperty("_MainTex") ? "_MainTex" : null);
                        if (texProp != null)
                        {
                            m.SetTexture("_BaseMap", src.GetTexture(texProp));
                            m.SetTextureScale("_BaseMap", src.GetTextureScale(texProp));
                            m.SetTextureOffset("_BaseMap", src.GetTextureOffset(texProp));
                        }
                        Color c = src.HasProperty("_BaseColor") ? src.GetColor("_BaseColor") : (src.HasProperty("_Color") ? src.GetColor("_Color") : Color.white);
                        m.SetColor("_BaseColor", c);
                        if (src.HasProperty("_BumpMap") && src.GetTexture("_BumpMap") != null)
                        {
                            m.SetTexture("_BumpMap", src.GetTexture("_BumpMap"));
                            m.SetFloat("_NormalMapOn", 1f);
                            m.EnableKeyword("_NORMALMAP");
                        }
                        if (src.IsKeywordEnabled("_ALPHATEST_ON") || (src.HasProperty("_AlphaClip") && src.GetFloat("_AlphaClip") > 0.5f))
                        {
                            m.SetFloat("_AlphaClip", 1f);
                            m.EnableKeyword("_ALPHATEST_ON");
                            if (src.HasProperty("_Cutoff")) m.SetFloat("_Cutoff", src.GetFloat("_Cutoff"));
                        }
                        ReefBuilder.ConfigureFishMaterial(m, b, 0.08f, 7f, 0.8f, 1.6f);
                        m.enableInstancing = true;
                        string path = AssetDatabase.GenerateUniqueAssetPath(dir + "/" + Sanitize(src.name) + "_Swim.mat");
                        AssetDatabase.CreateAsset(m, path);
                        mats[i] = m;
                        converted++;
                    }
                    Undo.RecordObject(r, "Make fish swim");
                    r.sharedMaterials = mats;
                }

                if (go.GetComponent<SelectableSpecies>() == null) Undo.AddComponent<SelectableSpecies>(go);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Reef] FishSwim applied (" + converted + " new material(s) in " + dir + "). Tune 'Swimming' on the material; " +
                      "assign a SpeciesData on SelectableSpecies. Then drag the object into a prefab and use it in ReefBuildSettings > Schools.");
        }

        [MenuItem("Tools/Reef/Make Selected Selectable", priority = 21)]
        public static void MakeSelectedSelectable()
        {
            int n = 0;
            foreach (var go in Selection.gameObjects)
            {
                if (go.GetComponent<SelectableSpecies>() != null) continue;
                Undo.AddComponent<SelectableSpecies>(go);
                n++;
            }
            Debug.Log("[Reef] Added SelectableSpecies to " + n + " object(s). Now assign each one a SpeciesData asset (Create > Reef > Species Data).");
        }

        [MenuItem("Tools/Reef/Give Selected A Swim Path", priority = 22)]
        public static void AddSwimPathToSelected()
        {
            foreach (var go in Selection.gameObjects)
            {
                var pathGo = new GameObject(go.name + " Path");
                Undo.RegisterCreatedObjectUndo(pathGo, "Create swim path");
                pathGo.transform.position = go.transform.position;
                var path = pathGo.AddComponent<SwimPath>();
                for (int k = 0; k < 6; k++)
                {
                    float a = k / 6f * Mathf.PI * 2f;
                    var wp = new GameObject("Waypoint " + (k + 1)).transform;
                    wp.SetParent(pathGo.transform, false);
                    wp.position = go.transform.position + new Vector3(Mathf.Cos(a) * 5f, 0f, Mathf.Sin(a) * 5f);
                }
                path.Rebuild();
                var ps = go.GetComponent<PathSwimmer>();
                if (ps == null) ps = Undo.AddComponent<PathSwimmer>(go);
                Undo.RecordObject(ps, "Assign path");
                ps.path = path;
                Selection.activeGameObject = pathGo;
            }
            Debug.Log("[Reef] Swim path created. Move its Waypoint children in the Scene view to shape the route.");
        }

        static string Sanitize(string s)
        {
            foreach (char c in System.IO.Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s;
        }
    }
}
