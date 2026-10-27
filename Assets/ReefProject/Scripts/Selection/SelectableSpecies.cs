using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Reef
{
    /// <summary>
    /// Marks a model in the scene as something the viewer can aim at and examine
    /// (and, later, collect). Add it to the root of each species model / prefab and
    /// point it at a SpeciesData asset.
    ///
    /// Highlighting draws a glowing rim "shell" over the model's own renderers, so the
    /// model's materials are never modified. It works for static meshes, skinned (rigged)
    /// meshes and fish using the Reef/FishSwim shader (the rim wiggles with the fish).
    /// </summary>
    [DisallowMultipleComponent]
    public class SelectableSpecies : MonoBehaviour
    {
        static readonly List<SelectableSpecies> s_All = new List<SelectableSpecies>();

        /// <summary>Every enabled SelectableSpecies in the scene.</summary>
        public static IReadOnlyList<SelectableSpecies> All => s_All;

        [Tooltip("Names and facts shown when this is examined.")]
        public SpeciesData species;

        [Tooltip("Optional point to aim at (e.g. an empty child at the fish's centre). Defaults to the centre of the model's bounds.")]
        public Transform focusPoint;

        [Tooltip("How forgiving aiming is, in metres around the focus point. 0 = based on the model's size.")]
        public float captureRadius = 0f;

        [Tooltip("Smallest capture radius, so tiny things like shark teeth are still easy to select.")]
        public float minimumCaptureRadius = 0.25f;

        public bool selectable = true;

        [Tooltip("When collected, shrink away and hide (good for shells and teeth). Leave off for animals.")]
        public bool hideWhenCollected = false;

        public bool IsCollected { get; private set; }
        public bool IsHighlighted => highlighted;

        /// <summary>
        /// True while the viewer is reading this one's info panel. PathSwimmer and FishSchool
        /// stop moving it (no swimming, no bobbing) until it's released.
        /// </summary>
        public bool IsHeld { get; private set; }

        public void SetHeld(bool held) => IsHeld = held;

        /// <summary>Raised after Collect() is called.</summary>
        public event System.Action<SelectableSpecies> Collected;

        static readonly int RimColorId = Shader.PropertyToID("_RimColor");
        static readonly int RimIntensityId = Shader.PropertyToID("_RimIntensity");
        static readonly string[] SwimProperties =
        {
            "_SwimAmplitude", "_SwimSpeed", "_SwimWaves", "_SwimHeadZ", "_SwimTailZ", "_SwimTailBias", "_SwimSway", "_SwimVertical"
        };

        Renderer[] renderers;
        readonly List<Renderer> shells = new List<Renderer>();
        Material rimMaterial;
        bool highlighted;
        Vector3 localCenter;
        float localRadius = 0.3f;

        public string DisplayName => species != null ? species.DisplayTitle : name;
        public Color AccentColor => species != null ? species.accentColor : new Color(1f, 0.82f, 0.35f, 1f);

        /// <summary>Where the reticle and aiming go.</summary>
        public Vector3 FocusPoint => focusPoint != null ? focusPoint.position : transform.TransformPoint(localCenter);

        /// <summary>Approximate radius of the model in world metres.</summary>
        public float VisualRadius => localRadius * MaxAbs(transform.lossyScale);

        public float CaptureRadius => captureRadius > 0f ? captureRadius : Mathf.Max(minimumCaptureRadius, VisualRadius * 0.9f);

        public bool CanSelect => selectable && !IsCollected && isActiveAndEnabled;

        void Awake()
        {
            CacheRenderers();
            Measure();
        }

        void OnEnable()
        {
            if (!s_All.Contains(this)) s_All.Add(this);
        }

        void OnDisable()
        {
            s_All.Remove(this);
            IsHeld = false;
            if (highlighted) SetHighlighted(false, Color.white);
        }

        void OnDestroy()
        {
            if (rimMaterial != null) Destroy(rimMaterial);
        }

        public void CacheRenderers()
        {
            var list = new List<Renderer>();
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r is MeshRenderer || r is SkinnedMeshRenderer)
                {
                    if (r.gameObject.name == "ReefHighlightShell") continue;
                    list.Add(r);
                }
            }
            renderers = list.ToArray();
        }

        /// <summary>Recompute the aim centre and size from the renderers (call after swapping the model).</summary>
        public void Measure()
        {
            bool has = false;
            Bounds b = new Bounds(transform.position, Vector3.zero);
            if (renderers != null)
            {
                foreach (var r in renderers)
                {
                    if (r == null) continue;
                    if (!has) { b = r.bounds; has = true; }
                    else b.Encapsulate(r.bounds);
                }
            }
            if (!has)
            {
                localCenter = Vector3.zero;
                localRadius = 0.3f;
                return;
            }
            localCenter = transform.InverseTransformPoint(b.center);
            float s = Mathf.Max(MaxAbs(transform.lossyScale), 1e-4f);
            localRadius = b.extents.magnitude / s;
        }

        static float MaxAbs(Vector3 v) => Mathf.Max(Mathf.Abs(v.x), Mathf.Max(Mathf.Abs(v.y), Mathf.Abs(v.z)));

        // ------------------------------------------------------------------ highlight

        /// <summary>Turn the glowing rim on or off.</summary>
        public void SetHighlighted(bool on, Color color, float intensity = 1f)
        {
            if (on && rimMaterial == null)
            {
                rimMaterial = CreateRimMaterial();
                if (rimMaterial == null) return;
            }
            if (rimMaterial != null)
            {
                rimMaterial.SetColor(RimColorId, color);
                rimMaterial.SetFloat(RimIntensityId, intensity);
            }
            if (on == highlighted) return;
            highlighted = on;

            if (on && shells.Count == 0) BuildShells();
            foreach (var s in shells) if (s != null) s.enabled = on;
        }

        Material CreateRimMaterial()
        {
            Material template = Resources.Load<Material>("ReefHighlightRim");
            Material mat = null;
            if (template != null) mat = new Material(template);
            else
            {
                Shader sh = Shader.Find("Reef/HighlightRim");
                if (sh == null)
                {
                    Debug.LogWarning("[Reef] Highlight shader 'Reef/HighlightRim' not found. Run Tools > Reef Builder > Set up CC_HUB so the highlight material is created.", this);
                    return null;
                }
                mat = new Material(sh);
            }
            mat.name = "ReefHighlightRim (" + name + ")";

            // If the model uses the swimming fish shader, copy its wiggle so the rim wiggles too.
            Material source = null;
            if (renderers != null)
                foreach (var r in renderers)
                    if (r != null && r.sharedMaterial != null) { source = r.sharedMaterial; break; }
            if (source != null)
            {
                foreach (var p in SwimProperties)
                    if (source.HasProperty(p) && mat.HasProperty(p)) mat.SetFloat(p, source.GetFloat(p));
            }
            return mat;
        }

        void BuildShells()
        {
            if (renderers == null) CacheRenderers();
            foreach (var r in renderers)
            {
                if (r == null) continue;
                var go = new GameObject("ReefHighlightShell");
                go.layer = r.gameObject.layer;
                go.transform.SetParent(r.transform, false);

                Renderer shell = null;
                int subMeshes = 1;
                if (r is SkinnedMeshRenderer smr)
                {
                    var s = go.AddComponent<SkinnedMeshRenderer>();
                    s.sharedMesh = smr.sharedMesh;
                    s.bones = smr.bones;
                    s.rootBone = smr.rootBone;
                    s.localBounds = smr.localBounds;
                    s.updateWhenOffscreen = smr.updateWhenOffscreen;
                    subMeshes = smr.sharedMesh != null ? smr.sharedMesh.subMeshCount : 1;
                    shell = s;
                }
                else
                {
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) { Destroy(go); continue; }
                    go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                    shell = go.AddComponent<MeshRenderer>();
                    subMeshes = mf.sharedMesh.subMeshCount;
                }

                var mats = new Material[Mathf.Max(1, subMeshes)];
                for (int i = 0; i < mats.Length; i++) mats[i] = rimMaterial;
                shell.sharedMaterials = mats;
                shell.shadowCastingMode = ShadowCastingMode.Off;
                shell.receiveShadows = false;
                shell.lightProbeUsage = LightProbeUsage.Off;
                shell.reflectionProbeUsage = ReflectionProbeUsage.Off;
                shell.enabled = false;
                shells.Add(shell);
            }
        }

        // ------------------------------------------------------------------ collecting

        /// <summary>Mark as collected (used by SpeciesBasket). Hides the object if hideWhenCollected is on.</summary>
        public void Collect()
        {
            if (IsCollected) return;
            IsCollected = true;
            SetHighlighted(false, Color.white);
            Collected?.Invoke(this);
            if (hideWhenCollected && isActiveAndEnabled) StartCoroutine(ShrinkAway());
        }

        /// <summary>Bring a collected object back (e.g. when restarting the game).</summary>
        public void ResetCollected()
        {
            IsCollected = false;
            if (!gameObject.activeSelf) gameObject.SetActive(true);
        }

        IEnumerator ShrinkAway()
        {
            Vector3 start = transform.localScale;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / 0.6f;
                float s = 1f - Mathf.SmoothStep(0f, 1f, t);
                transform.localScale = start * Mathf.Max(0.001f, s);
                yield return null;
            }
            transform.localScale = start;
            gameObject.SetActive(false);
        }

        void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying)
            {
                CacheRenderers();
                Measure();
            }
            Gizmos.color = new Color(1f, 0.8f, 0.3f, 0.8f);
            Gizmos.DrawWireSphere(FocusPoint, CaptureRadius);
        }
    }
}
