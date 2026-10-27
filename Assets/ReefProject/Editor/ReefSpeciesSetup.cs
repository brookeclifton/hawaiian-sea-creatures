using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Reef.EditorTools
{
    /// <summary>
    /// Tools > Reef > Set Up My Species
    ///
    /// Turns the optimized models in Assets/ReefSpecies/&lt;name&gt;/&lt;name&gt;.glb into reef-ready
    /// prefabs and puts them into the Reef Builder, then rebuilds the reef scatter and sea life:
    ///  - wraps each model in a prefab (real-world size, facing +Z)
    ///  - fish without a rig get the Reef/FishSwim wiggle (the whale swims up-and-down)
    ///  - rigged animals get a looping swim animation (ReefAnimationDriver)
    ///  - each gets SelectableSpecies + a SpeciesData asset (names filled in where known;
    ///    descriptions left for your team)
    ///  - registers them in ReefBuildSettings (shark/turtle slots, Other characters, Schools, Layers)
    /// Safe to run again: it updates what's there and never overwrites SpeciesData you've edited.
    /// </summary>
    public static class ReefSpeciesSetup
    {
        public const string Folder = "Assets/ReefSpecies";

        enum Role { Shark, Turtle, Character, School, Layer }

        class Entry
        {
            public string file, title, common, hawaiian, scientific, note;
            public SpeciesCategory category;
            public Role role;
            public bool rigged, swim, vertical, alphaClip, selectable = true, collectible = true;
            public float targetLength;          // rigged only (static models are already real size)
            public bool measureWidth;           // manta: size by wingspan
            public float swimAmp = 0.08f, swimSpeed = 7f, swimWaves = 0.8f, tailBias = 1.6f;
            // character
            public float speed = 0.8f; public Vector2 radius = new Vector2(30, 25), height = new Vector2(2, 4);
            // school
            public SchoolBehaviour behaviour; public ReefZone zone; public int groups = 3, perGroup = 16;
            public float homeRadius = 5f, cruise = 0.6f, schoolHeight = 2f, nearSpawn;
            // layer
            public string layer; public int count; public Vector2 scale = new Vector2(0.8f, 1.2f);
            public float spacing = 2f, clustering = 0.3f, align = 1f, sink = 0.03f, selectFraction = 1f;
            public bool collider, shadows = true, hide;
            public string reuseSpecies;         // existing SpeciesData asset path
        }

        static List<Entry> Entries() => new List<Entry>
        {
            new Entry { file = "tigerShark", title = "Tiger Shark", role = Role.Shark, rigged = true, targetLength = 3.6f,
                        reuseSpecies = ReefBuilder.SpeciesDir + "/TigerShark.asset" },
            new Entry { file = "seaTurtle", title = "Green Sea Turtle", role = Role.Turtle, rigged = true, targetLength = 0.9f,
                        reuseSpecies = ReefBuilder.SpeciesDir + "/GreenSeaTurtle.asset" },
            new Entry { file = "mantaRay", title = "Manta Ray", common = "Manta ray", hawaiian = "Hāhālua", category = SpeciesCategory.Fish,
                        note = "Hawaiʻi has reef mantas (Mobula alfredi) and oceanic mantas (Mobula birostris); confirm which.",
                        role = Role.Character, rigged = true, targetLength = 3.5f, measureWidth = true, collectible = false,
                        speed = 0.6f, radius = new Vector2(24, 20), height = new Vector2(2.5f, 4.5f) },
            new Entry { file = "humpbackWhale", title = "Humpback Whale", common = "Humpback whale", hawaiian = "Koholā",
                        scientific = "Megaptera novaeangliae", category = SpeciesCategory.Other, collectible = false,
                        role = Role.Character, swim = true, vertical = true, swimAmp = 0.05f, swimSpeed = 1.4f, swimWaves = 0.5f, tailBias = 1.4f,
                        speed = 0.9f, radius = new Vector2(20, 16), height = new Vector2(4f, 6f) },

            new Entry { file = "coralFish", title = "Coral Reef Fish", common = "Coral reef fish",
                        note = "Looks like it may be a Moorish idol (kihikihi); please confirm the species.",
                        category = SpeciesCategory.Fish, role = Role.School, swim = true, swimAmp = 0.08f, swimSpeed = 7f,
                        behaviour = SchoolBehaviour.Schooling, zone = ReefZone.PatchReefs, groups = 3, perGroup = 18,
                        homeRadius = 4.5f, cruise = 0.6f, schoolHeight = 1.8f, nearSpawn = 40f },
            new Entry { file = "parrotfish", title = "Parrotfish", common = "Parrotfish", hawaiian = "Uhu", category = SpeciesCategory.Fish,
                        role = Role.School, swim = true, swimAmp = 0.07f, swimSpeed = 5f,
                        behaviour = SchoolBehaviour.LooseAggregation, zone = ReefZone.SpurTops, groups = 4, perGroup = 5,
                        homeRadius = 5f, cruise = 0.45f, schoolHeight = 1.2f, nearSpawn = 0f },
            new Entry { file = "tuna", title = "Tuna", common = "Tuna", hawaiian = "ʻAhi", category = SpeciesCategory.Fish,
                        note = "ʻAhi covers yellowfin and bigeye tuna; confirm the species.",
                        role = Role.School, swim = true, swimAmp = 0.05f, swimSpeed = 10f, swimWaves = 0.6f, tailBias = 2.2f, collectible = false,
                        behaviour = SchoolBehaviour.Schooling, zone = ReefZone.SandChannels, groups = 1, perGroup = 9,
                        homeRadius = 12f, cruise = 1.3f, schoolHeight = 5f, nearSpawn = 0f },
            // Built from your humuhumu.png: a rounded, two-sided body textured with the photo.
            new Entry { file = "humuhumu", title = "Humuhumunukunukuāpuaʻa", role = Role.School, swim = true, alphaClip = true,
                        swimAmp = 0.035f, swimSpeed = 4f, swimWaves = 0.5f, tailBias = 2.5f,
                        reuseSpecies = ReefBuilder.SpeciesDir + "/Humuhumunukunukuapuaa.asset",
                        behaviour = SchoolBehaviour.Territorial, zone = ReefZone.SpurTops, groups = 6, perGroup = 2,
                        homeRadius = 2.5f, cruise = 0.4f, schoolHeight = 0.6f, nearSpawn = 32f },

            new Entry { file = "lavaRock", title = "Lava Rock", common = "Lava rock (basalt)", hawaiian = "Pōhaku", category = SpeciesCategory.Other,
                        role = Role.Layer, layer = "Basalt boulders", collectible = false,
                        scale = new Vector2(0.5f, 2.2f), spacing = 1.4f, clustering = 0.4f, align = 0.7f, sink = 0.12f, collider = true, count = 150 },
            new Entry { file = "seaweed", title = "Seaweed", common = "Seaweed", hawaiian = "Limu", category = SpeciesCategory.Algae,
                        role = Role.Layer, layer = "Seaweed (limu)", zone = ReefZone.SpurSides, count = 160, collectible = false,
                        scale = new Vector2(1.2f, 2.8f), spacing = 0.4f, clustering = 0.7f, align = 0.5f, sink = 0.02f, shadows = false, hide = true },
            new Entry { file = "cowrieShell", title = "Cowrie Shell", common = "Cowrie", hawaiian = "Leho", category = SpeciesCategory.Mollusk,
                        role = Role.Layer, layer = "Shells on the sand", scale = new Vector2(1.6f, 2.4f), spacing = 1f, shadows = false, hide = true },
            new Entry { file = "coneShell", title = "Cone Shell", common = "Cone shell", category = SpeciesCategory.Mollusk,
                        note = "Could also be an olive shell; please confirm.", role = Role.Layer, layer = "Shells on the sand", scale = new Vector2(1.6f, 2.4f), spacing = 1f, shadows = false, hide = true },
            new Entry { file = "snailShell", title = "Marbled Cone Shell", common = "Marbled cone shell", scientific = "Conus marmoreus",
                        category = SpeciesCategory.Mollusk, role = Role.Layer, layer = "Shells on the sand", scale = new Vector2(1.6f, 2.4f), spacing = 1f, shadows = false, hide = true },
            new Entry { file = "sharkTooth", title = "Tiger Shark Tooth", role = Role.Layer, layer = "Tiger shark teeth", scale = new Vector2(1.6f, 2.4f), spacing = 1f, shadows = false, hide = true,
                        reuseSpecies = ReefBuilder.SpeciesDir + "/TigerSharkTooth.asset" },
            new Entry { file = "coconut", title = "Coconut", common = "Coconut", hawaiian = "Niu", scientific = "Cocos nucifera",
                        category = SpeciesCategory.Other, role = Role.Layer, layer = "Coconuts and driftwood", zone = ReefZone.SandChannels,
                        count = 10, scale = new Vector2(1.6f, 2.4f), spacing = 3f, clustering = 0.2f, sink = 0.04f, hide = true },
            new Entry { file = "driftwood", title = "Driftwood", common = "Driftwood", category = SpeciesCategory.Other, collectible = false,
                        role = Role.Layer, layer = "Coconuts and driftwood", zone = ReefZone.SandChannels, count = 10, scale = new Vector2(1.6f, 2.4f), spacing = 3f, clustering = 0.2f, sink = 0.04f, hide = true },
        };

        [MenuItem("Tools/Reef/Set Up My Species (and rebuild reef)", priority = 10)]
        public static void Run()
        {
            var settings = ReefBuilder.LoadOrCreateSettings();
            var log = new List<string>();
            var missing = new List<string>();
            var prefabs = new Dictionary<string, (GameObject prefab, SpeciesData data)>();

            try
            {
                var list = Entries();
                for (int i = 0; i < list.Count; i++)
                {
                    var e = list[i];
                    EditorUtility.DisplayProgressBar("Reef species setup", e.title, i / (float)list.Count);
                    string glbPath = Folder + "/" + e.file + "/" + e.file + ".glb";
                    var model = AssetDatabase.LoadAssetAtPath<GameObject>(glbPath);
                    if (model == null) { missing.Add(glbPath); continue; }
                    var data = EnsureSpecies(e);
                    var prefab = BuildPrefab(e, model, data);
                    prefabs[e.file] = (prefab, data);
                    log.Add(e.title);
                }

                Register(settings, prefabs);
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();

                var terrain = ReefBuilder.FindTerrain();
                if (terrain != null)
                {
                    EditorUtility.DisplayProgressBar("Reef species setup", "Rebuilding reef scatter", 0.9f);
                    ReefBuilder.Scatter(settings, terrain);
                    EditorUtility.DisplayProgressBar("Reef species setup", "Rebuilding sea life", 0.95f);
                    ReefBuilder.BuildLife(settings, terrain);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            string recipeReport = ReefRecipeSetup.Run();
            string msg = "Set up " + log.Count + " species: " + string.Join(", ", log) + ".\n\n" + recipeReport;
            if (missing.Count > 0) msg += "\n\nNot found (still importing?): " + string.Join(", ", missing);
            msg += "\n\nThe reef scatter and sea life were rebuilt. Save the scene (Cmd+S) and press Play.";
            Debug.Log("[Reef] " + msg.Replace("\n", " "));
            EditorUtility.DisplayDialog("Reef species setup", msg, "OK");
        }

        // ------------------------------------------------------------------ species data

        static SpeciesData EnsureSpecies(Entry e)
        {
            if (!string.IsNullOrEmpty(e.reuseSpecies))
            {
                var existing = AssetDatabase.LoadAssetAtPath<SpeciesData>(e.reuseSpecies);
                if (existing != null) return existing;
            }
            string path = Folder + "/" + e.file + "/" + e.file + "_Info.asset";
            var d = AssetDatabase.LoadAssetAtPath<SpeciesData>(path);
            if (d != null) return d; // never overwrite what the team has written
            d = ScriptableObject.CreateInstance<SpeciesData>();
            d.commonName = string.IsNullOrEmpty(e.common) ? e.title : e.common;
            d.hawaiianName = e.hawaiian;
            d.scientificName = e.scientific;
            d.category = e.category;
            d.collectible = e.collectible;
            d.description = "PLACEHOLDER - replace with your team's vetted description." + (string.IsNullOrEmpty(e.note) ? "" : "\n(" + e.note + ")");
            AssetDatabase.CreateAsset(d, path);
            return d;
        }

        // ------------------------------------------------------------------ prefab

        static GameObject BuildPrefab(Entry e, GameObject model, SpeciesData data)
        {
            string dir = Folder + "/" + e.file;
            var root = new GameObject(e.title.Replace(" ", ""));
            try
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
                inst.transform.SetParent(root.transform, false);

                if (e.rigged)
                {
                    // Size and centre the rig (its swim animation keys the root, so we scale the wrapper's child).
                    Bounds b = RendererBounds(inst);
                    float size = e.measureWidth ? b.size.x : Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                    float s = size > 1e-4f ? e.targetLength / size : 1f;
                    inst.transform.localScale = Vector3.one * s;
                    b = RendererBounds(inst);
                    inst.transform.localPosition = -b.center;
                    var drv = root.AddComponent<ReefAnimationDriver>();
                    drv.preferredClip = "swim";
                }

                if (e.swim) ApplySwimMaterials(e, inst, dir);

                if (e.selectable)
                {
                    var sel = root.AddComponent<SelectableSpecies>();
                    sel.species = data;
                    sel.hideWhenCollected = e.hide;
                    sel.minimumCaptureRadius = 0.25f;
                }

                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                    r.shadowCastingMode = e.shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;

                if (e.collider)
                    foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                        if (mf.sharedMesh != null && mf.GetComponent<Collider>() == null) mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;

                string path = dir + "/" + e.file + ".prefab";
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static Bounds RendererBounds(GameObject go)
        {
            bool has = false; Bounds b = new Bounds(go.transform.position, Vector3.zero);
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
            }
            return b;
        }

        static readonly string[] TexProps = { "_BaseMap", "baseColorTexture", "_MainTex" };
        static readonly string[] ColorProps = { "_BaseColor", "baseColorFactor", "_Color" };
        static readonly string[] NormalProps = { "_BumpMap", "normalTexture" };

        static void ApplySwimMaterials(Entry e, GameObject inst, string dir)
        {
            var shader = Shader.Find("Reef/FishSwim");
            if (shader == null) { Debug.LogError("[Reef] Reef/FishSwim shader missing."); return; }
            int idx = 0;
            foreach (var mf in inst.GetComponentsInChildren<MeshFilter>(true))
            {
                var r = mf.GetComponent<MeshRenderer>();
                if (r == null || mf.sharedMesh == null) continue;
                Bounds mb = mf.sharedMesh.bounds;
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var src = mats[i];
                    string path = dir + "/" + e.file + "_Swim" + (idx++ == 0 ? "" : "_" + idx) + ".mat";
                    var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                    bool isNew = m == null;
                    if (isNew) m = new Material(shader);
                    if (src != null)
                    {
                        foreach (var p in TexProps) if (src.HasProperty(p) && src.GetTexture(p) != null) { m.SetTexture("_BaseMap", src.GetTexture(p)); break; }
                        foreach (var p in ColorProps) if (src.HasProperty(p)) { m.SetColor("_BaseColor", src.GetColor(p)); break; }
                        foreach (var p in NormalProps)
                            if (src.HasProperty(p) && src.GetTexture(p) != null)
                            {
                                m.SetTexture("_BumpMap", src.GetTexture(p)); m.SetFloat("_NormalMapOn", 1f); m.EnableKeyword("_NORMALMAP"); break;
                            }
                    }
                    ReefBuilder.ConfigureFishMaterial(m, mb, e.swimAmp, e.swimSpeed, e.swimWaves, e.tailBias);
                    m.SetFloat("_SwimVertical", e.vertical ? 1f : 0f);
                    m.SetFloat("_Cull", 0f); // scanned fins are often single-sided
                    if (e.alphaClip)
                    {
                        m.SetFloat("_AlphaClip", 1f);
                        m.SetFloat("_Cutoff", 0.5f);
                        m.EnableKeyword("_ALPHATEST_ON");
                        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                    }
                    m.enableInstancing = true;
                    if (isNew) AssetDatabase.CreateAsset(m, path); else EditorUtility.SetDirty(m);
                    mats[i] = m;
                }
                r.sharedMaterials = mats;
            }
        }

        // ------------------------------------------------------------------ builder settings

        static void Register(ReefBuildSettings s, Dictionary<string, (GameObject prefab, SpeciesData data)> p)
        {
            var entries = Entries();
            foreach (var e in entries)
            {
                if (!p.TryGetValue(e.file, out var pd)) continue;
                switch (e.role)
                {
                    case Role.Shark:
                        s.tigerShark.prefab = pd.prefab; s.tigerShark.species = pd.data; break;
                    case Role.Turtle:
                        s.swimmingTurtles.prefab = pd.prefab; s.swimmingTurtles.species = pd.data;
                        s.restingTurtles.prefab = pd.prefab; s.restingTurtles.species = pd.data; break;
                    case Role.Character:
                    {
                        if (s.otherCharacters == null) s.otherCharacters = new List<CharacterSpec>();
                        var c = s.otherCharacters.Find(x => x != null && x.name == e.title);
                        if (c == null) { c = new CharacterSpec { name = e.title }; s.otherCharacters.Add(c); }
                        c.enabled = true; c.prefab = pd.prefab; c.species = pd.data; c.route = CharacterRoute.WideReefLoop;
                        c.count = 1; c.speed = e.speed; c.pathRadius = e.radius; c.heightAboveFloor = e.height;
                        break;
                    }
                    case Role.School:
                    {
                        var sc = s.schools.Find(x => x != null && x.name == e.title);
                        if (sc == null) { sc = new SchoolSpec { name = e.title }; s.schools.Add(sc); }
                        sc.enabled = true; sc.prefab = pd.prefab; sc.species = pd.data; sc.behaviour = e.behaviour; sc.zone = e.zone;
                        sc.groups = e.groups; sc.fishPerGroup = e.perGroup; sc.homeRadius = e.homeRadius; sc.cruiseSpeed = e.cruise;
                        sc.heightAboveFloor = e.schoolHeight; sc.nearSpawn = e.nearSpawn;
                        break;
                    }
                    case Role.Layer:
                    {
                        var L = s.layers.Find(x => x != null && x.name == e.layer);
                        bool fresh = false;
                        if (L == null) { L = new ScatterLayer { name = e.layer, prefabs = new List<GameObject>() }; s.layers.Add(L); fresh = true; }
                        if (!L.prefabs.Contains(pd.prefab))
                        {
                            // First real model replaces the placeholders in this layer.
                            L.prefabs.RemoveAll(x => x == null || AssetDatabase.GetAssetPath(x).StartsWith(ReefBuilder.GenPrefabs));
                            L.prefabs.Add(pd.prefab);
                        }
                        L.enabled = true;
                        L.scale = e.scale; L.minSpacing = e.spacing; L.alignToSlope = e.align; L.sink = e.sink;
                        L.clustering = e.clustering; L.addCollider = e.collider; L.castShadows = e.shadows;
                        L.makeSelectable = e.selectable; L.selectableFraction = e.selectFraction; L.hideWhenCollected = e.hide;
                        L.species = null; // each prefab carries its own SpeciesData
                        if (fresh || e.count > 0) { if (e.count > 0) L.count = e.count; if (fresh) L.zone = e.zone; }
                        break;
                    }
                }
            }
            // The two generic placeholder schools are replaced by the real fish.
            foreach (var sc in s.schools)
                if (sc != null && sc.name.Contains("(placeholder)")) sc.enabled = false;
        }
    }
}
