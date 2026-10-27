using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace Reef
{
    /// <summary>
    /// The aiming ring and floating name tag. Both are real 3D objects in the scene (never
    /// screen-space UI), so they sit at the correct stereo depth:
    ///  - with no target, a small faint ring sits at the wall's own depth (zero parallax,
    ///    the most comfortable place for it);
    ///  - with a target, the ring wraps the species at the species' depth and the name tag
    ///    floats just above it, turned to face the viewer.
    /// Sizes are scaled with distance so they read the same near and far.
    /// </summary>
    [DefaultExecutionOrder(20)]
    public class SelectionReticle : MonoBehaviour
    {
        [Header("Ring")]
        [Tooltip("Unlit, transparent material for the ring. Uses Resources/ReefReticle if empty.")]
        public Material lineMaterial;
        public int segments = 72;
        [Tooltip("Line thickness as a fraction of distance (keeps it the same apparent width).")]
        public float lineWidthPerMeter = 0.0035f;
        public bool showIdleRing = true;
        [Tooltip("Idle ring radius as a fraction of the wall distance.")]
        public float idleRadiusPerMeter = 0.012f;
        public Color idleColor = new Color(1f, 1f, 1f, 0.35f);
        [Tooltip("Largest the ring may look, as a fraction of the wall's height, so big animals (the whale) can be selected without backing off into the fog.")]
        [UnityEngine.Range(0.2f, 1f)] public float maxRingFractionOfWall = 0.7f;
        [Tooltip("How quickly the ring follows a moving target.")]
        public float followSharpness = 14f;

        [Header("Name tag")]
        public bool showNameTag = true;
        [Tooltip("Letter height as a fraction of distance.")]
        public float tagHeightPerMeter = 0.028f;
        public string examineHint = "[A] learn more";
        public TMP_FontAsset font;

        ReefCCRig rig;
        LineRenderer ring;
        TextMeshPro nameTag;
        Transform ringT, tagT;
        SelectableSpecies target;
        bool examining;
        Vector3 smoothPos;
        float smoothRadius;
        float appear;
        bool snapNext = true;
        Color currentColor;

        void Start()
        {
            rig = ReefCCRig.Find();
            BuildRing();
            BuildTag();
            currentColor = idleColor;
            SetTarget(target); // in case the selector picked a target before we were built
        }

        void BuildRing()
        {
            var go = new GameObject("Ring");
            go.transform.SetParent(transform, false);
            ringT = go.transform;
            ring = go.AddComponent<LineRenderer>();
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.positionCount = segments;
            ring.alignment = LineAlignment.TransformZ;
            ring.numCapVertices = 0;
            ring.shadowCastingMode = ShadowCastingMode.Off;
            ring.receiveShadows = false;
            ring.lightProbeUsage = LightProbeUsage.Off;
            ring.reflectionProbeUsage = ReflectionProbeUsage.Off;

            if (lineMaterial == null) lineMaterial = Resources.Load<Material>("ReefReticle");
            if (lineMaterial == null)
            {
                var sh = Shader.Find("Reef/UnlitVertexColor");
                if (sh != null) lineMaterial = new Material(sh);
            }
            if (lineMaterial != null) ring.sharedMaterial = lineMaterial;
            SetRingRadius(1f);
        }

        void BuildTag()
        {
            var go = new GameObject("NameTag");
            go.transform.SetParent(transform, false);
            nameTag = go.AddComponent<TextMeshPro>();
            // Adding TextMeshPro swaps the Transform for a RectTransform, so grab it afterwards.
            tagT = nameTag.transform;
            if (font != null) nameTag.font = font;
            nameTag.fontSize = 10f; // 10 = 1 metre em-height for 3D TextMeshPro; the transform scale sets real size
            nameTag.alignment = TextAlignmentOptions.Bottom;
            nameTag.textWrappingMode = TextWrappingModes.NoWrap;
            nameTag.overflowMode = TextOverflowModes.Overflow;
            nameTag.rectTransform.pivot = new Vector2(0.5f, 0f);
            nameTag.rectTransform.sizeDelta = new Vector2(40f, 4f);
            nameTag.outlineWidth = 0.18f;
            nameTag.outlineColor = new Color32(0, 18, 36, 220);
            nameTag.color = Color.white;
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.shadowCastingMode = ShadowCastingMode.Off;
            go.SetActive(false);
        }

        static float s_MaxRingFraction = 0.7f;

        /// <summary>
        /// Ring radius in metres for a target at a given distance: hugs the model, but never looks bigger
        /// than part of the wall's height (the wall is only ~23 degrees tall, so a whale-sized ring would
        /// otherwise only fit on screen from far away).
        /// </summary>
        public static float RingRadius(SelectableSpecies s, float distance, ReefCCRig rig)
        {
            float r = Mathf.Clamp(s.VisualRadius * 1.1f, 0.12f, 4f);
            if (rig != null)
            {
                float halfHeightSlope = rig.ScreenSize.y * 0.5f / Mathf.Max(0.1f, rig.ScreenDistance);
                r = Mathf.Min(r, Mathf.Max(0.05f, distance) * halfHeightSlope * s_MaxRingFraction);
            }
            return Mathf.Max(0.05f, r);
        }

        void SetRingRadius(float r)
        {
            if (ring == null) return;
            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                ring.SetPosition(i, new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f));
            }
        }

        /// <summary>Hide the name tag while the target's info card is open (the card shows the name).</summary>
        public void SetExamining(bool value)
        {
            examining = value;
            SetTarget(target);
        }

        /// <summary>Called by SpeciesSelector when the target changes (null = nothing targeted).</summary>
        public void SetTarget(SelectableSpecies s)
        {
            if (s != target) snapNext = target == null || s == null;
            target = s;
            if (nameTag != null)
            {
                if (s != null && showNameTag && !examining)
                {
                    string title = s.DisplayName;
                    string sub = s.species != null ? s.species.DisplaySubtitle : string.Empty;
                    string text = "<b>" + title + "</b>";
                    if (!string.IsNullOrEmpty(sub)) text += "\n<size=65%>" + sub + "</size>";
                    if (!string.IsNullOrEmpty(examineHint)) text += "\n<size=50%><alpha=#AA>" + examineHint + "</size>";
                    nameTag.text = text;
                    nameTag.gameObject.SetActive(true);
                }
                else nameTag.gameObject.SetActive(false);
            }
        }

        void LateUpdate()
        {
            if (rig == null) { rig = ReefCCRig.Find(); if (rig == null) return; }
            if (ring == null) return;

            Vector3 eye = rig.ViewOrigin;
            float dt = Time.deltaTime;
            float time = Time.time;

            Vector3 wantPos;
            float wantRadius;
            Color wantColor;

            if (target != null && target.isActiveAndEnabled)
            {
                wantPos = target.FocusPoint;
                s_MaxRingFraction = maxRingFractionOfWall;
                wantRadius = RingRadius(target, Vector3.Distance(eye, wantPos), rig);
                wantColor = target.AccentColor;
                wantColor.a = 0.95f;
                appear = Mathf.MoveTowards(appear, 1f, dt * 5f);
            }
            else
            {
                float wallDistance = rig.ScreenDistance;
                wantPos = eye + rig.ViewForward * wallDistance;
                wantRadius = wallDistance * idleRadiusPerMeter;
                wantColor = idleColor;
                appear = Mathf.MoveTowards(appear, showIdleRing ? 1f : 0f, dt * 5f);
            }

            if (snapNext)
            {
                smoothPos = wantPos;
                smoothRadius = wantRadius;
                snapNext = false;
            }
            else
            {
                float k = 1f - Mathf.Exp(-followSharpness * dt);
                smoothPos = Vector3.Lerp(smoothPos, wantPos, k);
                smoothRadius = Mathf.Lerp(smoothRadius, wantRadius, k);
            }
            currentColor = Color.Lerp(currentColor, wantColor, 1f - Mathf.Exp(-10f * dt));

            float pulse = target != null ? 1f + 0.04f * Mathf.Sin(time * 4f) : 1f;
            Vector3 toRing = smoothPos - eye;
            float dist = Mathf.Max(0.3f, toRing.magnitude);
            Quaternion face = Quaternion.LookRotation(toRing.sqrMagnitude > 1e-6f ? toRing : rig.ViewForward, Vector3.up);

            ringT.position = smoothPos;
            ringT.rotation = face;
            SetRingRadius(smoothRadius * pulse);
            ring.widthMultiplier = dist * lineWidthPerMeter;
            Color c = currentColor;
            c.a *= appear;
            ring.startColor = c;
            ring.endColor = c;
            ring.enabled = appear > 0.01f;

            if (nameTag != null && nameTag.gameObject.activeSelf)
            {
                Vector3 tagPos = smoothPos + Vector3.up * (smoothRadius * 1.05f + dist * 0.01f);
                tagT.position = tagPos;
                tagT.rotation = Quaternion.LookRotation(tagPos - eye, Vector3.up);
                float h = dist * tagHeightPerMeter;
                tagT.localScale = new Vector3(h, h, h);
                nameTag.alpha = appear;
            }
        }
    }
}
