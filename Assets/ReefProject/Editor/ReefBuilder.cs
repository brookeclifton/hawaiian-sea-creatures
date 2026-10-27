using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Reef.EditorTools
{
    /// <summary>
    /// Builds the reef environment into the open scene. Each step replaces only its own part of
    /// the "REEF (generated)" object, so you can rebuild one step without losing the others.
    /// Anything you place yourself OUTSIDE that object is never touched.
    /// </summary>
    public static class ReefBuilder
    {
        public const string RootFolder = "Assets/ReefProject";
        public const string Gen = RootFolder + "/Generated";
        public const string GenMeshes = Gen + "/Meshes";
        public const string GenMats = Gen + "/Materials";
        public const string GenTex = Gen + "/Textures";
        public const string GenPrefabs = Gen + "/Prefabs";
        public const string ResourcesDir = RootFolder + "/Resources";
        public const string SpeciesDir = RootFolder + "/Species";
        public const string SettingsPath = RootFolder + "/ReefBuildSettings.asset";
        public const string RootName = "REEF (generated)";
        public const string ScenePath = "Assets/Scenes/ReefScene.unity";

        // ================================================================== settings + species

        public static ReefBuildSettings LoadOrCreateSettings()
        {
            var s = AssetDatabase.LoadAssetAtPath<ReefBuildSettings>(SettingsPath);
            if (s != null) return s;
            EnsureFolder(RootFolder);
            s = ScriptableObject.CreateInstance<ReefBuildSettings>();
            PopulateDefaults(s);
            AssetDatabase.CreateAsset(s, SettingsPath);
            AssetDatabase.SaveAssets();
            return s;
        }

        const string PlaceholderText = "PLACEHOLDER - replace with your team's vetted description.";

        static SpeciesData Species(string file, string hawaiian, string common, string scientific,
                                   SpeciesCategory category, Color accent, bool collectible, string description = PlaceholderText)
        {
            EnsureFolder(SpeciesDir);
            string path = SpeciesDir + "/" + file + ".asset";
            var d = AssetDatabase.LoadAssetAtPath<SpeciesData>(path);
            if (d != null) return d;
            d = ScriptableObject.CreateInstance<SpeciesData>();
            d.hawaiianName = hawaiian;
            d.commonName = common;
            d.scientificName = scientific;
            d.category = category;
            d.accentColor = accent;
            d.collectible = collectible;
            d.description = description;
            AssetDatabase.CreateAsset(d, path);
            return d;
        }

        public static void PopulateDefaults(ReefBuildSettings s)
        {
            var shark = Species("TigerShark", "Niuhi", "Tiger shark", "Galeocerdo cuvier", SpeciesCategory.Shark, new Color(1f, 0.55f, 0.35f), false);
            var turtle = Species("GreenSeaTurtle", "Honu", "Green sea turtle", "Chelonia mydas", SpeciesCategory.SeaTurtle, new Color(0.55f, 1f, 0.6f), false);
            var humu = Species("Humuhumunukunukuapuaa", "Humuhumunukunukuāpuaʻa", "Reef triggerfish", "Rhinecanthus rectangulus", SpeciesCategory.Fish, new Color(1f, 0.85f, 0.3f), true);
            var tooth = Species("TigerSharkTooth", "", "Tiger shark tooth", "Galeocerdo cuvier", SpeciesCategory.Artifact, new Color(0.95f, 0.95f, 0.85f), true);
            var fish = Species("Placeholder_ReefFish", "", "Reef fish (placeholder)", "", SpeciesCategory.Fish, new Color(1f, 0.9f, 0.2f), true,
                               "Placeholder school. Assign your own fish prefab and SpeciesData in ReefBuildSettings > Schools.");
            var coral = Species("Placeholder_Coral", "", "Coral (placeholder)", "", SpeciesCategory.Coral, new Color(1f, 0.7f, 0.6f), false,
                                "Placeholder coral. Assign your Sketchfab coral prefabs and their SpeciesData in ReefBuildSettings > Layers.");
            var shell = Species("Placeholder_Shell", "", "Shell (placeholder)", "", SpeciesCategory.Mollusk, new Color(0.9f, 0.75f, 1f), true,
                                "Placeholder shell. Assign your shell prefabs and SpeciesData in ReefBuildSettings > Layers.");

            s.layers = new List<ScatterLayer>
            {
                new ScatterLayer { name = "Basalt boulders", placeholder = PlaceholderKind.Boulder, zone = ReefZone.ReefAnywhere, count = 150,
                                   scale = new Vector2(0.6f, 2.6f), minSpacing = 1.4f, clustering = 0.4f, alignToSlope = 0.7f, sink = 0.15f, addCollider = true },
                new ScatterLayer { name = "Mound corals", placeholder = PlaceholderKind.MoundCoral, zone = ReefZone.SpurTops, count = 240,
                                   scale = new Vector2(0.5f, 2.0f), minSpacing = 1.2f, clustering = 0.5f, alignToSlope = 0.4f, sink = 0.08f, addCollider = true,
                                   makeSelectable = true, species = coral, selectableFraction = 0.25f },
                new ScatterLayer { name = "Branching corals", placeholder = PlaceholderKind.BranchingCoral, zone = ReefZone.SpurSides, count = 220,
                                   scale = new Vector2(0.5f, 1.3f), minSpacing = 0.9f, clustering = 0.7f, alignToSlope = 0.6f, sink = 0.03f, addCollider = true,
                                   makeSelectable = true, species = coral, selectableFraction = 0.2f },
                new ScatterLayer { name = "Plate and encrusting corals", placeholder = PlaceholderKind.PlateCoral, zone = ReefZone.SpurSides, count = 110,
                                   scale = new Vector2(0.4f, 1.2f), minSpacing = 1.0f, clustering = 0.5f, alignToSlope = 0.9f, sink = 0.02f, addCollider = false,
                                   makeSelectable = true, species = coral, selectableFraction = 0.2f },
                new ScatterLayer { name = "Patch reef coral heads", placeholder = PlaceholderKind.MoundCoral, zone = ReefZone.PatchReefs, count = 40,
                                   scale = new Vector2(0.9f, 2.4f), minSpacing = 0.8f, clustering = 0.2f, alignToSlope = 0.4f, sink = 0.1f, addCollider = true,
                                   makeSelectable = true, species = coral, selectableFraction = 0.3f },
                new ScatterLayer { name = "Shells on the sand", placeholder = PlaceholderKind.Shell, zone = ReefZone.SandChannels, count = 45,
                                   scale = new Vector2(0.05f, 0.09f), minSpacing = 30f, clustering = 0.3f, alignToSlope = 1f, sink = 0.05f, addCollider = false, castShadows = false,
                                   makeSelectable = true, species = shell, selectableFraction = 1f, hideWhenCollected = true },
                new ScatterLayer { name = "Tiger shark teeth", placeholder = PlaceholderKind.SharkTooth, zone = ReefZone.SandChannels, count = 12,
                                   scale = new Vector2(0.025f, 0.035f), minSpacing = 120f, clustering = 0f, alignToSlope = 1f, sink = 0.02f, addCollider = false, castShadows = false,
                                   makeSelectable = true, species = tooth, selectableFraction = 1f, hideWhenCollected = true },
                new ScatterLayer { name = "Urchins and rock invertebrates (placeholder)", placeholder = PlaceholderKind.Urchin, zone = ReefZone.SpurSides, count = 70,
                                   scale = new Vector2(0.07f, 0.14f), minSpacing = 8f, clustering = 0.6f, alignToSlope = 0.8f, sink = 0.1f, addCollider = false, castShadows = false }
            };

            s.tigerShark.species = shark;
            s.swimmingTurtles.species = turtle;
            s.restingTurtles.species = turtle;

            s.schools = new List<SchoolSpec>
            {
                new SchoolSpec { name = "Humuhumunukunukuāpuaʻa", species = humu, placeholderLength = 0.25f, placeholderColor = new Color(0.86f, 0.8f, 0.62f),
                                 behaviour = SchoolBehaviour.Territorial, zone = ReefZone.SpurTops, groups = 4, fishPerGroup = 2, homeRadius = 2.5f,
                                 cruiseSpeed = 0.4f, heightAboveFloor = 0.6f, nearSpawn = 32f },
                new SchoolSpec { name = "Reef fish school (placeholder)", species = fish, placeholderLength = 0.2f, placeholderColor = new Color(1f, 0.85f, 0.1f),
                                 behaviour = SchoolBehaviour.Schooling, zone = ReefZone.PatchReefs, groups = 3, fishPerGroup = 22, homeRadius = 5f,
                                 cruiseSpeed = 0.6f, heightAboveFloor = 2.2f, nearSpawn = 45f },
                new SchoolSpec { name = "Reef fish over spurs (placeholder)", species = fish, placeholderLength = 0.18f, placeholderColor = new Color(0.35f, 0.5f, 0.9f),
                                 behaviour = SchoolBehaviour.LooseAggregation, zone = ReefZone.SpurTops, groups = 3, fishPerGroup = 16, homeRadius = 6f,
                                 cruiseSpeed = 0.5f, heightAboveFloor = 2.5f, nearSpawn = 0f }
            };
        }

        // ================================================================== 1. CC_HUB

        public static GameObject FindHub()
        {
            var scene = SceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
            {
                var t = ReefCCRig.FindDeep(root.transform, "CC_HUB");
                if (t != null) return t.gameObject;
            }
            return null;
        }

        public static bool SetupRig(ReefBuildSettings s, out string message)
        {
            var hub = FindHub();
            if (hub == null)
            {
                message = "No CC_HUB found in the open scene. Open (or add) one of the CC_CANOE scenes first, e.g. CC_ALL/CC_LAVA_LED_WALL_SCENE.";
                return false;
            }

            Undo.RegisterFullObjectHierarchyUndo(hub, "Set up CC_HUB for the reef");
            var log = new List<string>();

            if (Quaternion.Angle(hub.transform.rotation, Quaternion.identity) > 0.01f)
            {
                hub.transform.rotation = Quaternion.identity;
                log.Add("reset CC_HUB rotation to 0,0,0 (needed for correct stereo)");
            }

            var rig = GetOrAdd<ReefCCRig>(hub);
            rig.head = null; rig.frontScreen = null; rig.cameras = null;
            rig.AutoFind();
            var swim = GetOrAdd<SwimController>(hub);
            GetOrAdd<SpeciesSelector>(hub);
            GetOrAdd<SpeciesBasket>(hub);

            foreach (var typeName in new[] { "DPadFlyer", "CCaux_Waypointer" })
            {
                if (hub.GetComponent(typeName) is Behaviour b && b.enabled)
                {
                    Undo.RecordObject(b, "Disable " + typeName);
                    b.enabled = false;
                    log.Add("disabled " + typeName);
                }
            }

            foreach (var cam in hub.GetComponentsInChildren<Camera>(true))
            {
                Undo.RecordObject(cam, "Camera clip planes");
                cam.nearClipPlane = 0.05f;
                cam.farClipPlane = 120f;
                var data = cam.GetUniversalAdditionalCameraData();
                if (data != null)
                {
                    Undo.RecordObject(data, "Camera post-processing");
                    data.renderPostProcessing = true;
                    EditorUtility.SetDirty(data);
                }
                EditorUtility.SetDirty(cam);
            }
            log.Add("set near/far clip to 0.05/120 m and enabled post-processing on " + hub.GetComponentsInChildren<Camera>(true).Length + " cameras");

            EnsureResourceMaterials();
            var bounds = BuildBounds(s);
            swim.bounds = bounds;

            // Put the head at the spawn point so the Scene view shows what the audience will see.
            if (rig.head != null)
            {
                hub.transform.position += s.spawnHeadPosition - rig.head.position;
            }

            if (s.disableCCDemoObjects)
            {
                foreach (var root in hub.scene.GetRootGameObjects())
                {
                    if (root == hub || !root.activeSelf) continue;
                    foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                    {
                        if (mb != null && mb.GetType().Name == "Rotater")
                        {
                            Undo.RecordObject(root, "Disable demo object");
                            root.SetActive(false);
                            log.Add("disabled CC demo object '" + root.name + "'");
                            break;
                        }
                    }
                }
            }

            if (EnsureTMPEssentials()) log.Add("importing TextMesh Pro Essential Resources (needed for the info panel)");

            EditorSceneManager.MarkSceneDirty(hub.scene);
            message = "CC_HUB is ready: " + string.Join("; ", log) + ".";
            return true;
        }

        static bool EnsureTMPEssentials()
        {
            if (Resources.Load<TMPro.TMP_Settings>("TMP Settings") != null) return false;
            TMPro.TMP_PackageResourceImporter.ImportResources(true, false, false);
            return true;
        }

        public static void EnsureResourceMaterials()
        {
            EnsureFolder(ResourcesDir);
            EnsureMaterial(ResourcesDir + "/ReefHighlightRim.mat", "Reef/HighlightRim", null);
            EnsureMaterial(ResourcesDir + "/ReefReticle.mat", "Reef/UnlitVertexColor", null);
            EnsureMaterial(ResourcesDir + "/ReefMarineSnow.mat", "Reef/SoftParticle", null);
        }

        static ReefBounds BuildBounds(ReefBuildSettings s)
        {
            var t = ReplaceChild("Bounds and Spawn");
            var b = t.gameObject.AddComponent<ReefBounds>();
            t.position = new Vector3(s.boundsCenter.x, s.surfaceHeight - s.boundsSize.y * 0.5f, s.boundsCenter.z);
            b.size = s.boundsSize;
            b.surfaceHeight = s.surfaceHeight;
            var sp = new GameObject("Spawn Point (viewer's head)").transform;
            sp.SetParent(t, false);
            sp.position = s.spawnHeadPosition;
            sp.rotation = Quaternion.Euler(0f, s.spawnYaw, 0f);
            b.spawnPoint = sp;
            return b;
        }

        // ================================================================== 2. terrain

        public static Terrain FindTerrain()
        {
            var root = GameObject.Find(RootName);
            if (root != null)
            {
                var t = root.GetComponentInChildren<Terrain>();
                if (t != null) return t;
            }
            return Terrain.activeTerrain;
        }

        public static Terrain BuildTerrain(ReefBuildSettings s)
        {
            try
            {
                EditorUtility.DisplayProgressBar("Reef Builder", "Shaping the seafloor", 0.1f);
                var field = new ReefField(s);
                var layers = EnsureTerrainLayers(s.seed);

                int res = s.heightmapResolution <= 257 ? 257 : s.heightmapResolution <= 513 ? 513 : 1025;
                float size = s.terrainSize;
                float baseY = Mathf.Min(s.sandHeightNorth, s.sandHeightSouth) - 6f;
                float heightRange = s.surfaceHeight - baseY + 2f;
                Vector3 origin = new Vector3(s.boundsCenter.x - size * 0.5f, baseY, s.boundsCenter.z - size * 0.5f);

                var parent = ReplaceChild("Seafloor");

                var td = new TerrainData();
                td.heightmapResolution = res;
                td.size = new Vector3(size, heightRange, size);
                var h = new float[res, res];
                for (int zi = 0; zi < res; zi++)
                {
                    float wz = origin.z + zi / (float)(res - 1) * size;
                    for (int xi = 0; xi < res; xi++)
                    {
                        float wx = origin.x + xi / (float)(res - 1) * size;
                        h[zi, xi] = Mathf.Clamp01((field.Height(wx, wz) - baseY) / heightRange);
                    }
                }
                td.SetHeights(0, 0, h);

                EditorUtility.DisplayProgressBar("Reef Builder", "Painting sand, rock and rubble", 0.6f);
                td.terrainLayers = layers;
                const int ar = 256;
                td.alphamapResolution = ar;
                var a = new float[ar, ar, 3];
                for (int zi = 0; zi < ar; zi++)
                {
                    float v = zi / (float)(ar - 1);
                    float wz = origin.z + v * size;
                    for (int xi = 0; xi < ar; xi++)
                    {
                        float u = xi / (float)(ar - 1);
                        float wx = origin.x + u * size;
                        Vector3 w = field.Splat(wx, wz, td.GetSteepness(u, v));
                        a[zi, xi, 0] = w.x;
                        a[zi, xi, 1] = w.y;
                        a[zi, xi, 2] = w.z;
                    }
                }
                td.SetAlphamaps(0, 0, a);

                EnsureFolder(Gen);
                string path = Gen + "/ReefTerrainData.asset";
                if (AssetDatabase.LoadAssetAtPath<TerrainData>(path) != null) AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(td, path);

                var go = Terrain.CreateTerrainGameObject(td);
                go.name = "Reef Terrain";
                Undo.RegisterCreatedObjectUndo(go, "Build seafloor");
                go.transform.SetParent(parent, false);
                go.transform.position = origin;
                var terrain = go.GetComponent<Terrain>();
                var mat = EnsureTerrainMaterial();
                if (mat != null) terrain.materialTemplate = mat;
                terrain.heightmapPixelError = 3f;
                terrain.basemapDistance = 90f;
                terrain.drawInstanced = true;
                terrain.detailObjectDistance = 0f;

                AssetDatabase.SaveAssets();
                return terrain;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        static Material EnsureTerrainMaterial()
        {
            EnsureFolder(GenMats);
            return EnsureMaterial(GenMats + "/ReefTerrain.mat", "Universal Render Pipeline/Terrain/Lit", null);
        }

        static TerrainLayer[] EnsureTerrainLayers(int seed)
        {
            EnsureFolder(GenTex);
            return new[]
            {
                EnsureTerrainLayer("Sand", ReefProcedural.GroundKind.Sand, 4f, seed),
                EnsureTerrainLayer("ReefRock", ReefProcedural.GroundKind.ReefRock, 3f, seed + 1),
                EnsureTerrainLayer("Rubble", ReefProcedural.GroundKind.Rubble, 2.5f, seed + 2)
            };
        }

        /// <summary>Creates the ground texture + TerrainLayer once. If you swap in your own texture, rebuilding keeps it.</summary>
        static TerrainLayer EnsureTerrainLayer(string name, ReefProcedural.GroundKind kind, float tile, int seed)
        {
            string layerPath = GenTex + "/Terrain_" + name + ".terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
            if (layer != null && layer.diffuseTexture != null) return layer;

            string texPath = GenTex + "/Ground_" + name + ".png";
            if (!File.Exists(texPath))
            {
                var tex = ReefProcedural.GroundTexture(kind, 512, seed);
                File.WriteAllBytes(texPath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(texPath);
                if (AssetImporter.GetAtPath(texPath) is TextureImporter ti)
                {
                    ti.wrapMode = TextureWrapMode.Repeat;
                    ti.mipmapEnabled = true;
                    ti.SaveAndReimport();
                }
            }
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            bool isNew = layer == null;
            if (isNew) layer = new TerrainLayer();
            layer.diffuseTexture = texture;
            layer.tileSize = new Vector2(tile, tile);
            layer.smoothness = 0.1f;
            if (isNew) AssetDatabase.CreateAsset(layer, layerPath);
            else EditorUtility.SetDirty(layer);
            return layer;
        }

        // ================================================================== 3. scatter

        public static void Scatter(ReefBuildSettings s, Terrain terrain)
        {
            if (terrain == null) { Debug.LogError("[Reef] Build the seafloor first."); return; }
            var field = new ReefField(s);
            var parent = ReplaceChild("Reef Scatter");
            var td = terrain.terrainData;
            Vector3 tpos = terrain.transform.position;
            var bigObjects = new List<Vector3>(); // x, z, radius
            float halfX = s.boundsSize.x * 0.5f + s.scatterMargin;
            float halfZ = s.boundsSize.z * 0.5f + s.scatterMargin;
            Vector2 spawnXZ = new Vector2(s.spawnHeadPosition.x, s.spawnHeadPosition.z);

            try
            {
                for (int li = 0; li < s.layers.Count; li++)
                {
                    var L = s.layers[li];
                    if (L == null || !L.enabled || L.count <= 0) continue;
                    EditorUtility.DisplayProgressBar("Reef Builder", "Placing " + L.name, li / (float)s.layers.Count);

                    var prefabs = new List<GameObject>();
                    if (L.prefabs != null) foreach (var p in L.prefabs) if (p != null) prefabs.Add(p);
                    if (prefabs.Count == 0) prefabs = Placeholders(L.placeholder);
                    if (prefabs.Count == 0) continue;

                    var container = new GameObject(L.name).transform;
                    container.SetParent(parent, false);
                    var rng = new System.Random(s.seed * 31 + li * 7919);
                    var mine = new List<Vector3>();
                    int placed = 0, attempts = 0, maxAttempts = L.count * 60;

                    while (placed < L.count && attempts++ < maxAttempts)
                    {
                        float x = s.boundsCenter.x + ((float)rng.NextDouble() * 2f - 1f) * halfX;
                        float z = s.boundsCenter.z + ((float)rng.NextDouble() * 2f - 1f) * halfZ;
                        float u = (x - tpos.x) / td.size.x, v = (z - tpos.z) / td.size.z;
                        if (u < 0.01f || u > 0.99f || v < 0.01f || v > 0.99f) continue;

                        // Keep a clear area right where the viewer starts.
                        if (L.addCollider && Vector2.Distance(new Vector2(x, z), spawnXZ) < 4f) continue;

                        float slope = td.GetSteepness(u, v);
                        if (slope > L.maxSlope) continue;
                        if (!field.InZone(L.zone, x, z, slope)) continue;

                        if (L.clustering > 0f)
                        {
                            float c = Mathf.PerlinNoise(x * 0.09f + li * 10.3f, z * 0.09f + s.seed * 0.01f);
                            float keep = Mathf.Lerp(1f, ReefProcedural.SStep(0.3f, 0.75f, c), L.clustering);
                            if (rng.NextDouble() > keep) continue;
                        }

                        float t01 = (float)rng.NextDouble();
                        float scale = Mathf.Lerp(L.scale.x, L.scale.y, t01 * t01); // more small than large
                        float spacing = Mathf.Max(0.05f, L.minSpacing * scale);
                        if (TooClose(mine, x, z, spacing)) continue;
                        if (L.addCollider && TooClose(bigObjects, x, z, spacing * 0.6f)) continue;

                        float y = terrain.SampleHeight(new Vector3(x, 0f, z)) + tpos.y;
                        Vector3 normal = td.GetInterpolatedNormal(u, v);
                        Quaternion rot = Quaternion.FromToRotation(Vector3.up, Vector3.Slerp(Vector3.up, normal, L.alignToSlope))
                                       * Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                        Vector3 pos = new Vector3(x, y, z) - (rot * Vector3.up) * (L.sink * scale);

                        var prefab = prefabs[rng.Next(prefabs.Count)];
                        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, container);
                        go.transform.SetPositionAndRotation(pos, rot);
                        go.transform.localScale = prefab.transform.localScale * scale;
                        ConfigureScatterInstance(go, L, rng);

                        mine.Add(new Vector3(x, z, spacing));
                        if (L.addCollider) bigObjects.Add(new Vector3(x, z, spacing));
                        placed++;
                    }

                    if (placed < L.count)
                        Debug.Log("[Reef] '" + L.name + "': placed " + placed + " of " + L.count + " (ran out of room in zone " + L.zone + "; lower Min Spacing or change the zone).");
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            EditorSceneManager.MarkSceneDirty(parent.gameObject.scene);
        }

        static bool TooClose(List<Vector3> list, float x, float z, float spacing)
        {
            for (int i = 0; i < list.Count; i++)
            {
                float dx = list[i].x - x, dz = list[i].y - z;
                float r = Mathf.Max(spacing, list[i].z);
                if (dx * dx + dz * dz < r * r) return true;
            }
            return false;
        }

        static void ConfigureScatterInstance(GameObject go, ScatterLayer L, System.Random rng)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                r.shadowCastingMode = L.castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;

            if (L.addCollider && go.GetComponentInChildren<Collider>(true) == null)
            {
                foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
                    if (mf.sharedMesh != null) mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
            }

            bool selectable = L.makeSelectable && rng.NextDouble() < L.selectableFraction;
            var existing = go.GetComponent<SelectableSpecies>();
            if (existing != null && L.makeSelectable && !selectable)
            {
                // The prefab carries its own species info, but only a fraction of this layer should be selectable.
                UnityEngine.Object.DestroyImmediate(existing);
                existing = null;
            }
            if (selectable || existing != null)
            {
                var sel = existing != null ? existing : go.AddComponent<SelectableSpecies>();
                if (sel.species == null) sel.species = L.species;
                if (L.hideWhenCollected) sel.hideWhenCollected = true;
            }
            else
            {
                // Non-interactive scenery can be static-batched for speed.
                foreach (var t in go.GetComponentsInChildren<Transform>(true))
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
            }
        }

        // ------------------------------------------------------------------ placeholder prefabs

        static List<GameObject> Placeholders(PlaceholderKind kind)
        {
            var list = new List<GameObject>();
            switch (kind)
            {
                case PlaceholderKind.MoundCoral:
                    list.Add(PlaceholderPrefab("PH_MoundCoral_A", () => ReefProcedural.MoundCoral(11), new Color(0.62f, 0.52f, 0.30f), 0.25f, true));
                    list.Add(PlaceholderPrefab("PH_MoundCoral_B", () => ReefProcedural.MoundCoral(23), new Color(0.55f, 0.55f, 0.28f), 0.25f, true));
                    list.Add(PlaceholderPrefab("PH_MoundCoral_C", () => ReefProcedural.MoundCoral(37), new Color(0.47f, 0.35f, 0.24f), 0.25f, true));
                    break;
                case PlaceholderKind.BranchingCoral:
                    list.Add(PlaceholderPrefab("PH_FingerCoral", () => ReefProcedural.BranchingCoral(5, false), new Color(0.72f, 0.64f, 0.55f), 0.2f, true));
                    list.Add(PlaceholderPrefab("PH_CauliflowerCoral", () => ReefProcedural.BranchingCoral(9, true), new Color(0.68f, 0.46f, 0.44f), 0.2f, true));
                    break;
                case PlaceholderKind.PlateCoral:
                    list.Add(PlaceholderPrefab("PH_PlateCoral_A", () => ReefProcedural.PlateCoral(3), new Color(0.46f, 0.36f, 0.30f), 0.2f, false));
                    list.Add(PlaceholderPrefab("PH_PlateCoral_B", () => ReefProcedural.PlateCoral(8), new Color(0.42f, 0.32f, 0.38f), 0.2f, false));
                    break;
                case PlaceholderKind.Boulder:
                    list.Add(PlaceholderPrefab("PH_Boulder_A", () => ReefProcedural.Boulder(2), new Color(0.23f, 0.22f, 0.21f), 0.15f, true));
                    list.Add(PlaceholderPrefab("PH_Boulder_B", () => ReefProcedural.Boulder(6), new Color(0.29f, 0.28f, 0.26f), 0.15f, true));
                    list.Add(PlaceholderPrefab("PH_Boulder_C", () => ReefProcedural.Boulder(13), new Color(0.21f, 0.23f, 0.19f), 0.15f, true));
                    break;
                case PlaceholderKind.Shell:
                    list.Add(PlaceholderPrefab("PH_Cowrie", () => ReefProcedural.Shell(1, false), new Color(0.45f, 0.30f, 0.18f), 0.85f, false));
                    list.Add(PlaceholderPrefab("PH_ConeShell", () => ReefProcedural.Shell(2, true), new Color(0.85f, 0.78f, 0.60f), 0.7f, false));
                    break;
                case PlaceholderKind.SharkTooth:
                    list.Add(PlaceholderPrefab("PH_SharkTooth", ReefProcedural.SharkTooth, new Color(0.84f, 0.82f, 0.76f), 0.6f, false));
                    break;
                case PlaceholderKind.Urchin:
                    list.Add(PlaceholderPrefab("PH_Urchin", () => ReefProcedural.Urchin(4), new Color(0.12f, 0.06f, 0.12f), 0.3f, false));
                    break;
            }
            return list;
        }

        static GameObject PlaceholderPrefab(string name, Func<Mesh> meshFactory, Color color, float smoothness, bool collider)
        {
            string path = GenPrefabs + "/" + name + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            EnsureFolder(GenPrefabs);
            var mesh = SaveMesh(meshFactory(), GenMeshes + "/" + name + ".asset");
            var mat = LitMaterial(GenMats + "/" + name + ".mat", color, smoothness);
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            if (collider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        /// <summary>Swimming-animal placeholder: mesh on the root, scaled to real length, with the FishSwim shader.</summary>
        static GameObject AnimalPlaceholder(string name, Func<Mesh> meshFactory, Color color, float length, bool swim,
                                           float amplitude, float beatSpeed, float waves, float tailBias, SpeciesData species)
        {
            string path = GenPrefabs + "/" + name + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                // Reuse it unless the requested length changed.
                if (Mathf.Abs(existing.transform.localScale.x - length) < 0.001f) return existing;
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.DeleteAsset(GenMats + "/" + name + ".mat");
            }
            EnsureFolder(GenPrefabs);
            var mesh = SaveMesh(meshFactory(), GenMeshes + "/" + name + ".asset");
            Material mat;
            if (swim)
            {
                mat = FishMaterial(GenMats + "/" + name + ".mat", color, mesh.bounds, amplitude, beatSpeed, waves, tailBias);
            }
            else
            {
                mat = LitMaterial(GenMats + "/" + name + ".mat", color, 0.35f);
            }
            var go = new GameObject(name);
            go.transform.localScale = Vector3.one * length;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            var sel = go.AddComponent<SelectableSpecies>();
            sel.species = species;
            sel.minimumCaptureRadius = 0.3f;
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        static GameObject FishPlaceholder(SchoolSpec spec)
        {
            string safe = MakeSafeName(spec.name);
            bool territorial = spec.behaviour == SchoolBehaviour.Territorial;
            return AnimalPlaceholder("PH_Fish_" + safe,
                () => territorial ? ReefProcedural.Fish(0.42f, 0.2f) : ReefProcedural.Fish(0.5f, 0.16f),
                spec.placeholderColor, spec.placeholderLength, true,
                territorial ? 0.07f : 0.09f, territorial ? 6f : 8f, 0.8f, 1.6f, spec.species);
        }

        static GameObject SharkPlaceholder(CharacterSpec spec) =>
            AnimalPlaceholder("PH_TigerShark", ReefProcedural.Shark, new Color(0.46f, 0.47f, 0.47f), spec.placeholderLength, true,
                              0.06f, 2.2f, 0.6f, 1.3f, spec.species);

        static GameObject TurtlePlaceholder(CharacterSpec spec) =>
            AnimalPlaceholder("PH_GreenSeaTurtle", ReefProcedural.Turtle, new Color(0.36f, 0.32f, 0.2f), spec.placeholderLength, false,
                              0f, 0f, 0f, 1f, spec.species);

        static string MakeSafeName(string n)
        {
            var chars = new List<char>();
            foreach (char c in n) if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')) chars.Add(c);
            return chars.Count > 0 ? new string(chars.ToArray()) : "Fish";
        }

        // ================================================================== 4. atmosphere

        public static void BuildAtmosphere(ReefBuildSettings s, Terrain terrain)
        {
            try
            {
                var parent = ReplaceChild("Atmosphere");

                // --- Sun + caustics
                Light sun = FindSun();
                if (sun == null)
                {
                    var sg = new GameObject("Sun (Directional Light)");
                    Undo.RegisterCreatedObjectUndo(sg, "Create sun");
                    sun = sg.AddComponent<Light>();
                    sun.type = LightType.Directional;
                }
                Undo.RecordObject(sun, "Sun settings");
                Undo.RecordObject(sun.transform, "Sun direction");
                sun.transform.rotation = Quaternion.Euler(65f, 35f, 0f);
                sun.color = new Color(0.8f, 0.94f, 1f);
                sun.intensity = 1.8f;
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = 0.75f;
                RenderSettings.sun = sun;

                EditorUtility.DisplayProgressBar("Reef Builder", "Generating caustics", 0.1f);
                var frames = EnsureCausticFrames(s);
                var caustics = GetOrAdd<CausticsAnimator>(sun.gameObject);
                caustics.frames = frames;
                caustics.tileSize = s.causticTileSize;
                if (frames != null && frames.frames != null && frames.frames.Length > 0)
                {
                    sun.cookie = frames.frames[0];
                    var ld = sun.GetUniversalAdditionalLightData();
                    if (ld != null) { ld.lightCookieSize = new Vector2(s.causticTileSize, s.causticTileSize); EditorUtility.SetDirty(ld); }
                }

                // --- Water colour, fog, ambient, sky
                EditorUtility.DisplayProgressBar("Reef Builder", "Water, sky and fog", 0.6f);
                var skyMat = EnsureMaterial(GenMats + "/ReefSky.mat", "Reef/UnderwaterSky", null);
                var envGo = new GameObject("Underwater Environment");
                envGo.transform.SetParent(parent, false);
                var env = envGo.AddComponent<UnderwaterEnvironment>();
                env.surfaceHeight = s.surfaceHeight;
                env.sun = sun;
                env.skyboxMaterial = skyMat;
                env.fogDensity = s.fogDensity;
                if (skyMat != null) RenderSettings.skybox = skyMat;
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Exponential;
                RenderSettings.fogDensity = s.fogDensity;
                RenderSettings.fogColor = env.shallowWater;

                // --- Post-processing volume
                var volGo = new GameObject("Reef Post-Processing");
                volGo.transform.SetParent(parent, false);
                var vol = volGo.AddComponent<Volume>();
                vol.isGlobal = true;
                vol.priority = 10f;
                vol.sharedProfile = EnsureVolumeProfile();

                // --- Underside of the water surface
                var surfMat = EnsureMaterial(GenMats + "/ReefWaterSurface.mat", "Reef/WaterSurface", null);
                if (surfMat != null && frames != null && frames.frames != null && frames.frames.Length > 0)
                    surfMat.SetTexture("_RippleTex", frames.frames[0]);
                var surf = GameObject.CreatePrimitive(PrimitiveType.Plane);
                surf.name = "Water Surface (underside)";
                Object.DestroyImmediate(surf.GetComponent<Collider>());
                surf.transform.SetParent(parent, false);
                surf.transform.position = new Vector3(s.boundsCenter.x, s.surfaceHeight, s.boundsCenter.z);
                float sScale = s.terrainSize * 1.6f / 10f;
                surf.transform.localScale = new Vector3(sScale, 1f, sScale);
                var smr = surf.GetComponent<MeshRenderer>();
                smr.sharedMaterial = surfMat;
                smr.shadowCastingMode = ShadowCastingMode.Off;
                smr.receiveShadows = false;

                // --- Light shafts
                var coneMesh = AssetDatabase.LoadAssetAtPath<Mesh>(GenMeshes + "/LightShaftCone.asset");
                if (coneMesh == null) coneMesh = SaveMesh(ReefProcedural.LightShaftCone(0.7f, 2.4f, 16f, 20), GenMeshes + "/LightShaftCone.asset");
                var shaftMat = EnsureMaterial(GenMats + "/ReefLightShaft.mat", "Reef/LightShaft", null);
                var shafts = new GameObject("Light Shafts").transform;
                shafts.SetParent(parent, false);
                var rng = new System.Random(s.seed + 99);
                Quaternion tilt = Quaternion.FromToRotation(Vector3.down, sun.transform.forward);
                float hx = s.boundsSize.x * 0.5f + 8f, hz = s.boundsSize.z * 0.5f + 8f;
                for (int i = 0; i < s.lightShaftCount; i++)
                {
                    var g = new GameObject("Shaft " + i);
                    g.transform.SetParent(shafts, false);
                    g.transform.position = new Vector3(s.boundsCenter.x + ((float)rng.NextDouble() * 2f - 1f) * hx, s.surfaceHeight,
                                                       s.boundsCenter.z + ((float)rng.NextDouble() * 2f - 1f) * hz);
                    g.transform.rotation = tilt * Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                    float w = 0.7f + (float)rng.NextDouble() * 0.6f;
                    g.transform.localScale = new Vector3(w, 0.8f + (float)rng.NextDouble() * 0.4f, w);
                    g.AddComponent<MeshFilter>().sharedMesh = coneMesh;
                    var mr = g.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = shaftMat;
                    mr.shadowCastingMode = ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                    g.AddComponent<LightShaftSway>();
                }

                // --- Marine snow
                var snowGo = new GameObject("Marine Snow");
                snowGo.transform.SetParent(parent, false);
                var ps = snowGo.AddComponent<ParticleSystem>();
                var main = ps.main;
                main.playOnAwake = false;
                main.loop = false;
                var em = ps.emission; em.enabled = false;
                var sh = ps.shape; sh.enabled = false;
                var snowMat = AssetDatabase.LoadAssetAtPath<Material>(ResourcesDir + "/ReefMarineSnow.mat");
                snowGo.GetComponent<ParticleSystemRenderer>().sharedMaterial = snowMat;
                var snow = snowGo.AddComponent<MarineSnow>();
                snow.count = s.marineSnowCount;
                snow.material = snowMat;

                AssetDatabase.SaveAssets();
                EditorSceneManager.MarkSceneDirty(parent.gameObject.scene);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        static Light FindSun()
        {
            if (RenderSettings.sun != null) return RenderSettings.sun;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude))
                if (l.type == LightType.Directional) return l;
            return null;
        }

        static CausticFrameSet EnsureCausticFrames(ReefBuildSettings s)
        {
            EnsureFolder(Gen);
            string path = Gen + "/CausticFrames.asset";
            var set = AssetDatabase.LoadAssetAtPath<CausticFrameSet>(path);
            if (set != null && set.frames != null && set.frames.Length == s.causticFrames && set.frames.Length > 0 &&
                set.frames[0] != null && set.frames[0].width == s.causticResolution)
                return set;
            if (set != null) AssetDatabase.DeleteAsset(path);

            var frames = ReefProcedural.CausticFrames(Mathf.Max(4, s.causticFrames), Mathf.Clamp(s.causticResolution, 64, 1024), s.seed,
                p => EditorUtility.DisplayProgressBar("Reef Builder", "Generating caustic frames", p));
            set = ScriptableObject.CreateInstance<CausticFrameSet>();
            set.frames = frames;
            AssetDatabase.CreateAsset(set, path);
            foreach (var f in frames) AssetDatabase.AddObjectToAsset(f, set);
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            return set;
        }

        static VolumeProfile EnsureVolumeProfile()
        {
            EnsureFolder(Gen);
            string path = Gen + "/ReefVolumeProfile.asset";
            var p = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (p != null) return p;
            p = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(p, path);

            var tone = p.Add<Tonemapping>(true);
            tone.mode.value = TonemappingMode.Neutral;

            var bloom = p.Add<Bloom>(true);
            bloom.threshold.value = 1.0f;
            bloom.intensity.value = 0.35f;
            bloom.scatter.value = 0.65f;
            bloom.tint.value = new Color(0.8f, 0.95f, 1f);

            var ca = p.Add<ColorAdjustments>(true);
            ca.postExposure.value = 0.15f;
            ca.contrast.value = 8f;
            ca.colorFilter.value = new Color(0.9f, 1f, 1f);
            ca.hueShift.value = 0f;
            ca.saturation.value = 6f;

            var wb = p.Add<WhiteBalance>(true);
            wb.temperature.value = -15f;
            wb.tint.value = -4f;

            // Water absorbs red first: pull some red out of the image.
            var mix = p.Add<ChannelMixer>(true);
            mix.redOutRedIn.value = 82f;
            mix.redOutGreenIn.value = 8f;
            mix.redOutBlueIn.value = 4f;

            foreach (var c in p.components)
            {
                c.name = c.GetType().Name;
                c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(c, p);
            }
            EditorUtility.SetDirty(p);
            AssetDatabase.SaveAssets();
            return p;
        }

        // ================================================================== 5. life

        public static void BuildLife(ReefBuildSettings s, Terrain terrain)
        {
            if (terrain == null) { Debug.LogError("[Reef] Build the seafloor first."); return; }
            var field = new ReefField(s);
            var parent = ReplaceChild("Sea Life");
            var rng = new System.Random(s.seed + 555);
            Vector3 spawn = s.spawnHeadPosition;
            float Floor(float x, float z) => terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y;
            float Rand(float a, float b) => a + (b - a) * (float)rng.NextDouble();

            try
            {
                EditorUtility.DisplayProgressBar("Reef Builder", "Adding sea life", 0.2f);

                // --- Tiger shark(s): a wide, slow loop around the reef, appearing out of the fog.
                var sharkSpec = s.tigerShark;
                if (sharkSpec.enabled && sharkSpec.count > 0)
                {
                    var prefab = sharkSpec.prefab != null ? sharkSpec.prefab : SharkPlaceholder(sharkSpec);
                    for (int i = 0; i < sharkSpec.count; i++)
                    {
                        Vector3 c = new Vector3(s.boundsCenter.x, 0f, s.boundsCenter.z + 6f);
                        var path = MakeLoopPath(parent, "Tiger Shark Path " + (i + 1), c, sharkSpec.pathRadius, 9, i * 1.3f, sharkSpec.heightAboveFloor, rng, Floor);
                        var go = Spawn(prefab, parent, path.transform.GetChild(0).position, "Tiger Shark " + (i + 1), sharkSpec.species);
                        var ps = GetOrAdd<PathSwimmer>(go);
                        ps.path = path;
                        ps.speed = sharkSpec.speed;
                        ps.startAt = (0.15f + i * 0.5f) % 1f;
                        ps.viewerClearance = 3.5f;
                        ps.minHeightAboveFloor = 1f;
                        ps.bobAmplitude = 0.05f;
                        ps.maxBank = 10f;
                    }
                }

                // --- Swimming green sea turtles: slow loops around patch reefs (the first is the "hero" reef near the start).
                var turtleSpec = s.swimmingTurtles;
                GameObject turtlePrefab = null;
                if ((turtleSpec.enabled && turtleSpec.count > 0) || (s.restingTurtles.enabled && s.restingTurtles.count > 0))
                    turtlePrefab = turtleSpec.prefab != null ? turtleSpec.prefab : (s.restingTurtles.prefab != null ? s.restingTurtles.prefab : TurtlePlaceholder(turtleSpec));

                if (turtleSpec.enabled && turtleSpec.count > 0 && field.patches.Count > 0)
                {
                    for (int i = 0; i < turtleSpec.count; i++)
                    {
                        var pr = field.patches[i % field.patches.Count];
                        Vector3 c = new Vector3(pr.centre.x, 0f, pr.centre.y);
                        Vector2 radius = turtleSpec.pathRadius + Vector2.one * pr.radius * 0.5f;
                        var path = MakeLoopPath(parent, "Turtle Path " + (i + 1), c, radius, 6, i * 2.1f, turtleSpec.heightAboveFloor + Vector2.one * pr.height, rng, Floor);
                        var go = Spawn(turtlePrefab, parent, path.transform.GetChild(0).position, "Green Sea Turtle " + (i + 1), turtleSpec.species);
                        var ps = GetOrAdd<PathSwimmer>(go);
                        ps.path = path;
                        ps.speed = turtleSpec.speed;
                        ps.startAt = Rand(0f, 1f);
                        ps.bobAmplitude = 0.12f;
                        ps.maxBank = 8f;
                        ps.viewerClearance = 2.2f;
                        ps.minHeightAboveFloor = 0.6f;
                    }
                }

                // --- Resting turtles on the sand beside the hero patch reef.
                var restSpec = s.restingTurtles;
                if (restSpec.enabled && restSpec.count > 0 && field.patches.Count > 0 && turtlePrefab != null)
                {
                    var pr = field.patches[0];
                    for (int i = 0; i < restSpec.count; i++)
                    {
                        float ang = (40f + i * 70f) * Mathf.Deg2Rad;
                        Vector2 p2 = pr.centre + new Vector2(Mathf.Cos(ang), -Mathf.Sin(ang)) * (pr.radius + 1.2f);
                        Vector3 pos = new Vector3(p2.x, Floor(p2.x, p2.y) + 0.08f, p2.y);
                        var go = Spawn(turtlePrefab, parent, pos, "Resting Green Sea Turtle " + (i + 1), restSpec.species != null ? restSpec.species : turtleSpec.species);
                        Vector3 look = new Vector3(pr.centre.x, pos.y, pr.centre.y) - pos;
                        go.transform.rotation = Quaternion.LookRotation(Quaternion.Euler(0f, 60f, 0f) * look.normalized, Vector3.up);
                        var ps = GetOrAdd<PathSwimmer>(go);
                        ps.path = null;
                    }
                }

                // --- Other characters (any species, any of the four routes)
                if (s.otherCharacters != null)
                {
                    int ci = 0;
                    foreach (var spec in s.otherCharacters)
                    {
                        ci++;
                        if (spec == null || !spec.enabled || spec.count <= 0) continue;
                        if (spec.prefab == null)
                        {
                            Debug.LogWarning("[Reef] Other character '" + spec.name + "' has no prefab assigned, skipped.");
                            continue;
                        }
                        for (int i = 0; i < spec.count; i++)
                        {
                            string label = spec.name + " " + (i + 1);
                            GameObject go;
                            PathSwimmer ps;
                            switch (spec.route)
                            {
                                case CharacterRoute.WideReefLoop:
                                {
                                    Vector3 c = new Vector3(s.boundsCenter.x, 0f, s.boundsCenter.z + 6f);
                                    var path = MakeLoopPath(parent, label + " Path", c, spec.pathRadius, 8, ci * 0.9f + i * 1.7f, spec.heightAboveFloor, rng, Floor);
                                    go = Spawn(spec.prefab, parent, path.transform.GetChild(0).position, label, spec.species);
                                    ps = GetOrAdd<PathSwimmer>(go);
                                    ps.path = path;
                                    ps.viewerClearance = 3f;
                                    break;
                                }
                                case CharacterRoute.RestOnSand:
                                {
                                    var pr = field.patches[(ci + i) % field.patches.Count];
                                    float ang = Rand(0f, Mathf.PI * 2f);
                                    Vector2 p2 = pr.centre + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (pr.radius + 1.2f);
                                    Vector3 pos = new Vector3(p2.x, Floor(p2.x, p2.y) + 0.05f, p2.y);
                                    go = Spawn(spec.prefab, parent, pos, label, spec.species);
                                    go.transform.rotation = Quaternion.Euler(0f, Rand(0f, 360f), 0f);
                                    ps = GetOrAdd<PathSwimmer>(go);
                                    ps.path = null;
                                    break;
                                }
                                case CharacterRoute.HoverNearReef:
                                {
                                    if (!FindZonePoint(s, field, terrain, ReefZone.ReefAnywhere, 0f, new List<Vector3>(), rng, out Vector3 at)) continue;
                                    var path = MakeLoopPath(parent, label + " Path", at, new Vector2(1.2f, 1.2f), 5, i, spec.heightAboveFloor, rng, Floor);
                                    go = Spawn(spec.prefab, parent, path.transform.GetChild(0).position, label, spec.species);
                                    ps = GetOrAdd<PathSwimmer>(go);
                                    ps.path = path;
                                    ps.viewerClearance = 1.5f;
                                    break;
                                }
                                default: // PatchReefLoop
                                {
                                    var pr = field.patches[(ci + i) % field.patches.Count];
                                    Vector3 c = new Vector3(pr.centre.x, 0f, pr.centre.y);
                                    var path = MakeLoopPath(parent, label + " Path", c, spec.pathRadius + Vector2.one * pr.radius * 0.5f, 6, i * 2.1f,
                                                            spec.heightAboveFloor + Vector2.one * pr.height, rng, Floor);
                                    go = Spawn(spec.prefab, parent, path.transform.GetChild(0).position, label, spec.species);
                                    ps = GetOrAdd<PathSwimmer>(go);
                                    ps.path = path;
                                    ps.viewerClearance = 2.2f;
                                    break;
                                }
                            }
                            ps.speed = spec.speed;
                            ps.startAt = Rand(0f, 1f);
                        }
                    }
                }

                // --- Fish schools and territorial fish
                EditorUtility.DisplayProgressBar("Reef Builder", "Adding fish", 0.6f);
                if (s.schools != null)
                {
                    var used = new List<Vector3>();
                    foreach (var spec in s.schools)
                    {
                        if (spec == null || !spec.enabled || spec.groups <= 0) continue;
                        var prefab = spec.prefab != null ? spec.prefab : FishPlaceholder(spec);
                        for (int gi = 0; gi < spec.groups; gi++)
                        {
                            if (!FindZonePoint(s, field, terrain, spec.zone, spec.nearSpawn, used, rng, out Vector3 at)) continue;
                            used.Add(at);
                            at.y = Floor(at.x, at.z) + spec.heightAboveFloor;
                            var go = new GameObject(spec.name + " " + (gi + 1));
                            go.transform.SetParent(parent, false);
                            go.transform.position = at;
                            var school = go.AddComponent<FishSchool>();
                            school.fishPrefab = prefab;
                            school.species = spec.species;
                            school.count = spec.fishPerGroup;
                            school.behaviour = spec.behaviour;
                            school.homeRadius = spec.homeRadius;
                            school.homeHalfHeight = spec.behaviour == SchoolBehaviour.Territorial ? 1f : Mathf.Max(1f, spec.homeRadius * 0.35f);
                            school.cruiseSpeed = spec.cruiseSpeed;
                            school.surfaceHeight = s.surfaceHeight;
                            school.seed = s.seed * 7 + gi * 131 + spec.name.Length;
                            if (spec.behaviour == SchoolBehaviour.Territorial)
                            {
                                school.minSpeed = 0.05f;
                                school.maxSpeed = 1.4f;
                                school.viewerAvoidRadius = 1.6f;
                            }
                        }
                    }
                }

                EditorSceneManager.MarkSceneDirty(parent.gameObject.scene);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        static SwimPath MakeLoopPath(Transform parent, string name, Vector3 centre, Vector2 radius, int points, float angleOffset,
                                     Vector2 heightAboveFloor, System.Random rng, Func<float, float, float> floor)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = centre;
            var path = go.AddComponent<SwimPath>();
            path.closedLoop = true;
            for (int k = 0; k < points; k++)
            {
                float a = k / (float)points * Mathf.PI * 2f + angleOffset;
                float rx = radius.x * (0.85f + 0.3f * (float)rng.NextDouble());
                float rz = radius.y * (0.85f + 0.3f * (float)rng.NextDouble());
                float x = centre.x + Mathf.Cos(a) * rx;
                float z = centre.z + Mathf.Sin(a) * rz;
                float y = floor(x, z) + Mathf.Lerp(heightAboveFloor.x, heightAboveFloor.y, (float)rng.NextDouble());
                var wp = new GameObject("Waypoint " + (k + 1)).transform;
                wp.SetParent(go.transform, false);
                wp.position = new Vector3(x, y, z);
            }
            path.Rebuild();
            return path;
        }

        static GameObject Spawn(GameObject prefab, Transform parent, Vector3 pos, string name, SpeciesData species)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            go.transform.position = pos;
            var sel = go.GetComponent<SelectableSpecies>();
            if (sel == null) sel = go.AddComponent<SelectableSpecies>();
            if (species != null) sel.species = species;
            return go;
        }

        static bool FindZonePoint(ReefBuildSettings s, ReefField field, Terrain terrain, ReefZone zone, float nearSpawn,
                                  List<Vector3> avoid, System.Random rng, out Vector3 result)
        {
            var td = terrain.terrainData;
            Vector3 tpos = terrain.transform.position;
            Vector3 spawn = s.spawnHeadPosition;
            float hx = s.boundsSize.x * 0.5f - 4f, hz = s.boundsSize.z * 0.5f - 4f;
            for (int attempt = 0; attempt < 800; attempt++)
            {
                float x, z;
                if (nearSpawn > 0f)
                {
                    float a = (float)rng.NextDouble() * Mathf.PI; // in front of the viewer (+Z half-disc)
                    float r = Mathf.Lerp(7f, nearSpawn, (float)rng.NextDouble());
                    x = spawn.x + Mathf.Cos(a) * r;
                    z = spawn.z + Mathf.Sin(a) * r;
                }
                else
                {
                    x = s.boundsCenter.x + ((float)rng.NextDouble() * 2f - 1f) * hx;
                    z = s.boundsCenter.z + ((float)rng.NextDouble() * 2f - 1f) * hz;
                }
                float u = (x - tpos.x) / td.size.x, v = (z - tpos.z) / td.size.z;
                if (u < 0.02f || u > 0.98f || v < 0.02f || v > 0.98f) continue;
                if (!field.InZone(zone, x, z, td.GetSteepness(u, v))) continue;
                bool clash = false;
                foreach (var p in avoid) if ((new Vector2(p.x - x, p.z - z)).sqrMagnitude < 36f) { clash = true; break; }
                if (clash && attempt < 600) continue;
                result = new Vector3(x, 0f, z);
                return true;
            }
            result = Vector3.zero;
            return false;
        }

        // ================================================================== all + clear

        public static void BuildAll(ReefBuildSettings s)
        {
            if (!SetupRig(s, out string msg)) { EditorUtility.DisplayDialog("Reef Builder", msg, "OK"); return; }
            Debug.Log("[Reef] " + msg);
            var terrain = BuildTerrain(s);
            Scatter(s, terrain);
            BuildAtmosphere(s, terrain);
            BuildLife(s, terrain);
            AssetDatabase.SaveAssets();
            var root = GameObject.Find(RootName);
            if (root != null) Selection.activeGameObject = root;
            Debug.Log("[Reef] Reef built. Save the scene, then press Play.");
        }

        public static void RemoveGenerated()
        {
            var root = GameObject.Find(RootName);
            if (root != null) Undo.DestroyObjectImmediate(root);
        }

        public static void SaveSceneAsReefScene()
        {
            var scene = SceneManager.GetActiveScene();
            EnsureFolder(Path.GetDirectoryName(ScenePath).Replace('\\', '/'));
            if (EditorSceneManager.SaveScene(scene, ScenePath))
            {
                var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
                list.RemoveAll(e => e.path == ScenePath);
                list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = list.ToArray();
                Debug.Log("[Reef] Scene saved as " + ScenePath + " and added to the top of Build Settings.");
            }
        }

        // ================================================================== helpers

        static Transform GetRoot()
        {
            var go = GameObject.Find(RootName);
            if (go == null)
            {
                go = new GameObject(RootName);
                Undo.RegisterCreatedObjectUndo(go, "Create reef root");
            }
            return go.transform;
        }

        static Transform ReplaceChild(string name)
        {
            var root = GetRoot();
            var old = root.Find(name);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Build " + name);
            go.transform.SetParent(root, false);
            return go.transform;
        }

        static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            if (c == null) c = Undo.AddComponent<T>(go);
            return c;
        }

        public static void EnsureFolder(string path)
        {
            path = path.TrimEnd('/');
            if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        static Material EnsureMaterial(string path, string shaderName, Action<Material> init)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            var sh = Shader.Find(shaderName);
            if (sh == null)
            {
                Debug.LogError("[Reef] Shader '" + shaderName + "' not found. Check the Console for shader compile errors.");
                return null;
            }
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            m = new Material(sh);
            init?.Invoke(m);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static Material LitMaterial(string path, Color color, float smoothness)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            m = new Material(sh);
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            m.enableInstancing = true;
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        public static Material FishMaterial(string path, Color color, Bounds meshBounds, float amplitude, float beatSpeed, float waves, float tailBias)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            var sh = Shader.Find("Reef/FishSwim");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Simple Lit");
            m = new Material(sh);
            m.SetColor("_BaseColor", color);
            ConfigureFishMaterial(m, meshBounds, amplitude, beatSpeed, waves, tailBias);
            m.enableInstancing = true;
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        public static void ConfigureFishMaterial(Material m, Bounds meshBounds, float amplitude, float beatSpeed, float waves, float tailBias)
        {
            if (!m.HasProperty("_SwimAmplitude")) return;
            m.SetFloat("_SpecularOn", 1f);
            m.EnableKeyword("_SPECULAR_COLOR");
            m.SetColor("_SpecColor", new Color(0.3f, 0.3f, 0.3f, 0.6f));
            m.SetFloat("_SwimAmplitude", amplitude);
            m.SetFloat("_SwimSpeed", beatSpeed);
            m.SetFloat("_SwimWaves", waves);
            m.SetFloat("_SwimTailBias", tailBias);
            m.SetFloat("_SwimHeadZ", meshBounds.max.z);
            m.SetFloat("_SwimTailZ", meshBounds.min.z);
        }

        static Mesh SaveMesh(Mesh mesh, string path)
        {
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }
    }
}
