using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Reef
{
    public enum PanelPlacement
    {
        [Tooltip("Float beside the selection ring, at the species' depth, and follow it.")]
        NextToTarget,
        [Tooltip("Always in the same spot on the wall (right-hand side).")]
        FixedOnWall
    }

    /// <summary>
    /// The floating info card that opens when a species is examined.
    ///
    /// It is a WORLD-SPACE canvas (never Screen Space: CC_CANOE composites the two eye images on
    /// a Screen Space canvas, so an overlay would be drawn flat across both halves).
    ///
    /// Default placement: NEXT TO THE TARGET. The card appears beside the selection ring, at
    /// about the species' depth, on whichever side has room, and follows the animal as it swims
    /// and as the viewer moves. Its size is scaled with distance so the text always looks the
    /// same size, it is kept inside the edges of the wall, it's pulled in front of any rock that
    /// would hide it, and a thin line connects it to the ring.
    ///
    /// The card's height fits its text, so a short description gives a short card.
    /// While it's open (lockWhileReading) the card stays put on the wall instead of following
    /// the animal, so it can be read; SpeciesSelector also holds the animal still.
    ///
    /// The whole card is built in code, so there is no prefab to maintain.
    /// </summary>
    [DefaultExecutionOrder(30)]
    public class SpeciesInfoPanel : MonoBehaviour
    {
        [Header("Placement")]
        public PanelPlacement placement = PanelPlacement.NextToTarget;
        [Tooltip("Card width, and the tallest it gets, in metres at the wall's distance (it scales with distance so text reads the same). The height shrinks to fit the text.")]
        public Vector2 sizeMeters = new Vector2(1.7f, 1.3f);
        [Tooltip("Keep the card in one spot on the wall while it's open, instead of following the animal, so it can be read.")]
        public bool lockWhileReading = true;

        [Header("Next to target")]
        [Tooltip("How tall the card looks, in degrees of the viewer's field of view.")]
        public float angularHeight = 15f;
        [Tooltip("Space between the ring and the card, in degrees.")]
        public float gapDegrees = 1.5f;
        [Tooltip("Keep this far inside the edges of the wall, in degrees.")]
        public float edgeMarginDegrees = 1f;
        [Tooltip("Nearest the card may come to the viewer (closer is uncomfortable in stereo), metres.")]
        public float minDistance = 2.5f;
        public float maxDistance = 14f;
        [Tooltip("How quickly the card follows a moving target.")]
        public float followSharpness = 6f;
        [Tooltip("Rock/coral layers the card is pulled in front of.")]
        public LayerMask occlusionMask = 1;
        public bool showConnector = true;
        [Tooltip("Unlit transparent material for the connector line. Uses Resources/ReefReticle if empty.")]
        public Material connectorMaterial;

        [Header("Fixed on wall")]
        [Tooltip("Where the card sits on the wall: x -1 = left edge, 0 = centre, +1 = right edge; y -1 = bottom, +1 = top.")]
        public Vector2 wallAnchor = new Vector2(0.6f, 0.02f);
        [Tooltip("How far in front of the wall (toward the viewer) the card floats, in metres.")]
        public float popOut = 0.2f;
        [Tooltip("Canvas resolution. Font sizes below are in these units.")]
        public float unitsPerMeter = 600f;

        [Header("Look")]
        [Tooltip("Use the Prompt typeface (Resources/Fonts). Off = use the font below, or TextMesh Pro's default.")]
        public bool usePromptFont = true;
        public TMP_FontAsset font;
        public Color background = new Color(0.02f, 0.09f, 0.15f, 0.88f);
        public Color titleColor = Color.white;
        public Color subtitleColor = new Color(0.85f, 0.93f, 1f, 1f);
        public Color scientificColor = new Color(0.70f, 0.80f, 0.88f, 1f);
        public Color bodyColor = new Color(0.92f, 0.96f, 1f, 1f);
        public float titleSize = 92f;
        public float subtitleSize = 52f;
        public float scientificSize = 42f;
        public float bodySize = 44f;
        public float smallSize = 32f;

        [Header("Text")]
        public string closeHint = "[B] close";
        public string collectHint = "[X] add to basket";
        public string factPrefix = "Did you know? ";

        [Header("Animation")]
        public float fadeSeconds = 0.25f;

        public bool IsOpen => open;

        /// <summary>Card height as a fraction of sizeMeters.y (the card shrinks to fit its text).</summary>
        float CardHeightRatio => rootRect != null && rootRect.rect.height > 1f
            ? Mathf.Min(1f, rootRect.rect.height / Mathf.Max(1f, sizeMeters.y * unitsPerMeter))
            : 1f;

        ReefCCRig rig;
        Canvas canvas;
        CanvasGroup group;
        RectTransform rootRect;
        Image accentBar, photo;
        TextMeshProUGUI category, title, subtitle, scientific, body, fact, footer;
        RectTransform chipsRow;
        LayoutElement photoLayout;
        bool built, open;
        float alpha;
        static Sprite s_Rounded;

        SelectableSpecies current;
        bool snapPlacement;
        Vector3 smoothPos;
        float smoothScale = 1f;
        Quaternion smoothRot = Quaternion.identity;
        LineRenderer connector;
        Vector3 connectorFrom, connectorTo;
        bool placedRight = true;
        Color accent = Color.white;

        void Start()
        {
            rig = ReefCCRig.Find();
            if (!built) Build();
        }

        void Build()
        {
            built = true;
            if (rig == null) rig = ReefCCRig.Find();
            if (rig != null && transform.parent != rig.Hub) transform.SetParent(rig.Hub, false);
            gameObject.layer = 0;
            font = ReefFonts.Pick(usePromptFont, font);

            canvas = gameObject.GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 10;
            group = gameObject.GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            rootRect = (RectTransform)transform;
            rootRect.sizeDelta = sizeMeters * unitsPerMeter;
            rootRect.localScale = Vector3.one / unitsPerMeter;
            rootRect.pivot = new Vector2(0.5f, 0.5f);

            // The card's height follows its contents (content column + footer).
            var cardLayout = gameObject.AddComponent<VerticalLayoutGroup>();
            cardLayout.padding = new RectOffset(48, 48, 40, 30);
            cardLayout.spacing = 22f;
            cardLayout.childAlignment = TextAnchor.UpperLeft;
            cardLayout.childControlWidth = true;
            cardLayout.childControlHeight = true;
            cardLayout.childForceExpandWidth = true;
            cardLayout.childForceExpandHeight = false;
            var fitter = gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Background card
            var bg = MakeImage("Background", rootRect, background);
            Stretch(bg.rectTransform, 0, 0, 0, 0);
            bg.sprite = RoundedSprite();
            bg.type = Image.Type.Sliced;
            bg.pixelsPerUnitMultiplier = 0.6f;
            bg.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            // Accent strip along the top
            accentBar = MakeImage("Accent", rootRect, Color.white);
            var ar = accentBar.rectTransform;
            ar.anchorMin = new Vector2(0f, 1f);
            ar.anchorMax = new Vector2(1f, 1f);
            ar.pivot = new Vector2(0.5f, 1f);
            ar.offsetMin = new Vector2(40f, -14f);
            ar.offsetMax = new Vector2(-40f, -6f);
            accentBar.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            // Content column
            var content = NewRect("Content", rootRect);
            var v = content.gameObject.AddComponent<VerticalLayoutGroup>();
            v.childAlignment = TextAnchor.UpperLeft;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            v.spacing = 10f;

            category = MakeText("Category", content, smallSize, FontStyles.Bold | FontStyles.UpperCase, Color.white);
            category.characterSpacing = 6f;

            title = MakeText("Title", content, titleSize, FontStyles.Bold, titleColor);
            title.enableAutoSizing = true;
            title.fontSizeMin = titleSize * 0.55f;
            title.fontSizeMax = titleSize;
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.gameObject.AddComponent<LayoutElement>().preferredHeight = titleSize * 1.2f;

            subtitle = MakeText("Subtitle", content, subtitleSize, FontStyles.Normal, subtitleColor);
            scientific = MakeText("Scientific", content, scientificSize, FontStyles.Italic, scientificColor);

            chipsRow = NewRect("Chips", content);
            var h = chipsRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            h.spacing = 12f;
            chipsRow.gameObject.AddComponent<LayoutElement>().preferredHeight = smallSize * 1.9f;

            photo = MakeImage("Photo", content, Color.white);
            photo.preserveAspect = true;
            photoLayout = photo.gameObject.AddComponent<LayoutElement>();
            photoLayout.preferredHeight = 260f;

            body = MakeText("Body", content, bodySize, FontStyles.Normal, bodyColor);
            body.lineSpacing = 8f;
            fact = MakeText("Fact", content, bodySize * 0.94f, FontStyles.Italic, bodyColor);
            fact.lineSpacing = 6f;

            footer = MakeText("Footer", rootRect, smallSize, FontStyles.Normal, new Color(1f, 1f, 1f, 0.75f));

            var cgo = new GameObject("Connector");
            cgo.transform.SetParent(transform.parent, false);
            connector = cgo.AddComponent<LineRenderer>();
            connector.useWorldSpace = true;
            connector.positionCount = 2;
            connector.numCapVertices = 2;
            connector.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            connector.receiveShadows = false;
            if (connectorMaterial == null) connectorMaterial = Resources.Load<Material>("ReefReticle");
            if (connectorMaterial != null) connector.sharedMaterial = connectorMaterial;
            connector.enabled = false;

            gameObject.SetActive(true);
        }

        // ------------------------------------------------------------------ public API

        public void Show(SelectableSpecies target, bool collectingEnabled, SpeciesBasket basket)
        {
            if (!built) Build();
            current = target;
            snapPlacement = lockWhileReading || !open || alpha < 0.05f;

            SpeciesData d = target != null ? target.species : null;
            accent = target != null ? target.AccentColor : Color.white;
            accentBar.color = accent;
            category.color = accent;

            if (d == null)
            {
                category.text = "Unidentified";
                title.text = target != null ? target.name : "?";
                SetText(subtitle, "No SpeciesData assigned yet");
                SetText(scientific, "");
                SetText(body, "Assign a SpeciesData asset to this object's SelectableSpecies component to show its names and facts here.");
                SetText(fact, "");
                ClearChips();
                chipsRow.gameObject.SetActive(false);
                photo.gameObject.SetActive(false);
            }
            else
            {
                string cat = SpeciesData.CategoryLabel(d.category);
                if (d.nativeStatus != NativeStatus.Unknown) cat += "  ·  " + d.nativeStatus;
                category.text = cat;
                title.text = d.DisplayTitle;
                SetText(subtitle, d.DisplaySubtitle);
                SetText(scientific, d.scientificName);
                SetText(body, d.description);
                SetText(fact, string.IsNullOrWhiteSpace(d.funFact) ? "" : "<b>" + factPrefix + "</b>" + d.funFact);
                fact.color = Color.Lerp(bodyColor, accent, 0.35f);

                ClearChips();
                if (!string.IsNullOrWhiteSpace(d.conservationStatus)) AddChip(d.conservationStatus, accent);
                if (!string.IsNullOrWhiteSpace(d.sizeText)) AddChip(d.sizeText, new Color(0.6f, 0.8f, 0.95f, 1f));
                if (basket != null && basket.Has(d)) AddChip("In your basket", new Color(0.55f, 0.95f, 0.6f, 1f));
                chipsRow.gameObject.SetActive(chipsRow.childCount > 0);

                photo.sprite = d.image;
                photo.gameObject.SetActive(d.image != null);
            }

            bool canCollect = collectingEnabled && target != null && !target.IsCollected && (d == null || d.collectible);
            footer.text = canCollect ? closeHint + "      " + collectHint : closeHint;

            open = true;
            gameObject.SetActive(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);
            if (placement == PanelPlacement.FixedOnWall || current == null) PlaceOnWall();
            else PlaceNextToTarget(true);
        }

        public void ShowCollectedMessage(SpeciesBasket basket)
        {
            if (!built) return;
            string msg = "<b>Added to your basket!</b>";
            if (basket != null) msg += "   " + basket.ItemCount + " collected, " + basket.UniqueSpeciesCount + " species";
            footer.text = msg;
        }

        /// <summary>Replace the hint line at the bottom of the card (rich text allowed).</summary>
        public void ShowFooterMessage(string message)
        {
            if (!built) return;
            footer.text = message;
        }

        public void Hide()
        {
            open = false;
        }

        // ------------------------------------------------------------------ placement + fade

        void PlaceOnWall()
        {
            if (rig == null) rig = ReefCCRig.Find();
            if (rig == null) return;
            Transform hub = rig.Hub;
            if (transform.parent != hub) transform.SetParent(hub, false);

            Vector3 headL = rig.HeadLocal;
            Vector3 screenL = hub.InverseTransformPoint(rig.ScreenCenter);
            Vector2 wall = rig.ScreenSize;
            Vector3 towardViewer = (headL - screenL).normalized;

            Vector3 pos = screenL
                        + Vector3.right * (wallAnchor.x * wall.x * 0.5f)
                        + Vector3.up * (wallAnchor.y * wall.y * 0.5f)
                        + towardViewer * popOut;
            transform.localPosition = pos;
            transform.localRotation = Quaternion.LookRotation(pos - headL, Vector3.up);

            // Make sure the card is never taller than the wall.
            float maxH = wall.y * 0.92f;
            float cardH = sizeMeters.y * CardHeightRatio;
            float scale = cardH > maxH ? maxH / cardH : 1f;
            rootRect.localScale = Vector3.one * (scale / unitsPerMeter);
        }

        void LateUpdate()
        {
            if (!built) return;
            bool visible = open || alpha > 0.001f;
            if (visible && placement == PanelPlacement.NextToTarget && current != null && current.isActiveAndEnabled)
            {
                if (snapPlacement || !lockWhileReading) PlaceNextToTarget(snapPlacement);
                else UpdateLockedConnector();
            }
            snapPlacement = false;

            if (connector != null)
            {
                bool show = showConnector && visible && placement == PanelPlacement.NextToTarget && current != null;
                connector.enabled = show;
                if (show)
                {
                    connector.SetPosition(0, connectorFrom);
                    connector.SetPosition(1, connectorTo);
                    float d = rig != null ? Vector3.Distance(rig.ViewOrigin, connectorFrom) : 4f;
                    connector.widthMultiplier = Mathf.Max(0.002f, d * 0.0025f);
                    Color c = accent;
                    c.a = 0.8f * (group != null ? group.alpha : 1f);
                    connector.startColor = c;
                    connector.endColor = c;
                }
            }
        }

        /// <summary>
        /// Puts the card beside the target's ring, in the viewer's frame. Works in the "wall plane"
        /// (x/z, y/z in CC_HUB space), where the wall is a simple rectangle, so the card can be
        /// kept inside the wall's edges exactly.
        /// </summary>
        void PlaceNextToTarget(bool snap)
        {
            if (rig == null) rig = ReefCCRig.Find();
            if (rig == null || current == null) return;
            Transform hub = rig.Hub;
            if (transform.parent != hub) transform.SetParent(hub, true);

            Vector3 headL = rig.HeadLocal;
            Vector3 screenL = hub.InverseTransformPoint(rig.ScreenCenter);
            Vector2 wall = rig.ScreenSize;
            float wallZ = Mathf.Max(0.1f, screenL.z - headL.z);
            float uL = (screenL.x - wall.x * 0.5f - headL.x) / wallZ;
            float uR = (screenL.x + wall.x * 0.5f - headL.x) / wallZ;
            float vB = (screenL.y - wall.y * 0.5f - headL.y) / wallZ;
            float vT = (screenL.y + wall.y * 0.5f - headL.y) / wallZ;

            Vector3 focus = current.FocusPoint;
            Vector3 t = hub.InverseTransformPoint(focus) - headL;   // target relative to the head, hub axes
            if (t.z < 0.3f) return;                                  // behind or beside the viewer: keep last pose
            float ut = t.x / t.z, vt = t.y / t.z;
            float ringRadius = SelectionReticle.RingRadius(current, t.z, rig);
            float rr = ringRadius / t.z;

            float margin = Mathf.Tan(edgeMarginDegrees * Mathf.Deg2Rad);
            float gap = Mathf.Tan(gapDegrees * Mathf.Deg2Rad);
            float aspect = sizeMeters.x / Mathf.Max(0.01f, sizeMeters.y);
            float ph = Mathf.Min(2f * Mathf.Tan(angularHeight * 0.5f * Mathf.Deg2Rad), (vT - vB) - 2f * margin);
            float pw = ph * aspect;
            float maxW = (uR - uL) * 0.45f;
            if (pw > maxW) { pw = maxW; ph = pw / aspect; }
            // ph is the full-size card; the real card is shorter when its text is short.
            float ratio = CardHeightRatio;
            float avail = (vT - vB) - 2f * margin;
            if (ph * ratio > avail) { float shrink = avail / (ph * ratio); ph *= shrink; pw *= shrink; }
            float phCard = ph * ratio;

            // Side with room: prefer the side toward the middle of the wall.
            float mid = (uL + uR) * 0.5f;
            float right = ut + rr + gap + pw * 0.5f;
            float left = ut - rr - gap - pw * 0.5f;
            bool fitsRight = right + pw * 0.5f <= uR - margin;
            bool fitsLeft = left - pw * 0.5f >= uL + margin;
            bool useRight = ut <= mid ? (fitsRight || !fitsLeft) : !(fitsLeft || !fitsRight);
            float uc = useRight ? right : left;
            uc = Mathf.Clamp(uc, uL + margin + pw * 0.5f, uR - margin - pw * 0.5f);
            float vc = Mathf.Clamp(vt, vB + margin + phCard * 0.5f, vT - margin - phCard * 0.5f);

            // Depth: a little in front of the target, within comfortable limits.
            float depth = Mathf.Clamp(t.z - ringRadius * 0.5f, minDistance, maxDistance);
            Vector3 dirL = new Vector3(uc, vc, 1f);
            Vector3 eye = rig.ViewOrigin;
            Vector3 dirW = hub.TransformDirection(dirL).normalized;
            float along = depth * dirL.magnitude;
            if (Physics.Raycast(eye, dirW, out RaycastHit hit, along, occlusionMask, QueryTriggerInteraction.Ignore))
            {
                if (!hit.collider.transform.IsChildOf(current.transform))
                    along = Mathf.Max(minDistance, hit.distance - 0.3f);
            }
            depth = along / dirL.magnitude;

            Vector3 posL = headL + dirL * depth;
            Vector3 pos = hub.TransformPoint(posL);
            Quaternion rot = Quaternion.LookRotation(pos - eye, hub.up);
            float scale = (ph * depth) / Mathf.Max(0.01f, sizeMeters.y);

            if (snap)
            {
                smoothPos = pos; smoothRot = rot; smoothScale = scale;
            }
            else
            {
                float k = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
                smoothPos = Vector3.Lerp(smoothPos, pos, k);
                smoothRot = Quaternion.Slerp(smoothRot, rot, k);
                smoothScale = Mathf.Lerp(smoothScale, scale, k);
            }
            transform.SetPositionAndRotation(smoothPos, smoothRot);
            rootRect.localScale = Vector3.one * (smoothScale / unitsPerMeter);

            placedRight = useRight;
            UpdateConnector(focus, eye, ringRadius);
        }

        /// <summary>While the card is locked in place, only the line to the ring needs updating.</summary>
        void UpdateLockedConnector()
        {
            if (rig == null || current == null) return;
            Vector3 focus = current.FocusPoint;
            float z = (rig.Hub.InverseTransformPoint(focus) - rig.HeadLocal).z;
            UpdateConnector(focus, rig.ViewOrigin, SelectionReticle.RingRadius(current, Mathf.Max(0.3f, z), rig));
        }

        /// <summary>Connector: from the edge of the ring to the near edge of the card.</summary>
        void UpdateConnector(Vector3 focus, Vector3 eye, float ringRadius)
        {
            Vector3 cardEdge = transform.position + transform.rotation * Vector3.right * (placedRight ? -1f : 1f) * (sizeMeters.x * 0.5f * smoothScale);
            Vector3 towardCard = Vector3.ProjectOnPlane(cardEdge - focus, focus - eye);
            if (towardCard.sqrMagnitude < 1e-6f) towardCard = rig.Hub.right * (placedRight ? 1f : -1f);
            connectorFrom = focus + towardCard.normalized * ringRadius;
            connectorTo = cardEdge;
        }

        void Update()
        {
            if (!built || group == null) return;
            float target = open ? 1f : 0f;
            alpha = Mathf.MoveTowards(alpha, target, Time.deltaTime / Mathf.Max(0.01f, fadeSeconds));
            group.alpha = Mathf.SmoothStep(0f, 1f, alpha);
            canvas.enabled = alpha > 0.001f;
        }

        // ------------------------------------------------------------------ helpers

        void SetText(TextMeshProUGUI t, string s)
        {
            bool has = !string.IsNullOrWhiteSpace(s);
            t.text = has ? s : string.Empty;
            t.gameObject.SetActive(has);
        }

        void ClearChips()
        {
            for (int i = chipsRow.childCount - 1; i >= 0; i--) Destroy(chipsRow.GetChild(i).gameObject);
            chipsRow.DetachChildren();
        }

        void AddChip(string text, Color color)
        {
            var chip = MakeImage("Chip", chipsRow, new Color(color.r, color.g, color.b, 0.22f));
            chip.sprite = RoundedSprite();
            chip.type = Image.Type.Sliced;
            chip.pixelsPerUnitMultiplier = 1.2f;
            var hl = chip.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.padding = new RectOffset(18, 18, 6, 6);
            hl.childControlWidth = true;
            hl.childControlHeight = true;
            hl.childForceExpandWidth = false;
            hl.childForceExpandHeight = false;
            var t = MakeText("Label", chip.rectTransform, smallSize, FontStyles.Bold, Color.Lerp(color, Color.white, 0.5f));
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.text = text;
        }

        static RectTransform NewRect(string n, Transform parent)
        {
            var go = new GameObject(n, typeof(RectTransform));
            go.layer = 0;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        static void Stretch(RectTransform rt, float left, float right, float top, float bottom)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        Image MakeImage(string n, Transform parent, Color c)
        {
            var rt = NewRect(n, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        TextMeshProUGUI MakeText(string n, Transform parent, float size, FontStyles style, Color c)
        {
            var rt = NewRect(n, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = c;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            t.alignment = TextAlignmentOptions.TopLeft;
            return t;
        }

        /// <summary>A small rounded-rectangle sprite generated once, used 9-sliced for the card and chips.</summary>
        static Sprite RoundedSprite()
        {
            if (s_Rounded != null) return s_Rounded;
            const int size = 64;
            const float radius = 24f;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                    float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    float a = Mathf.Clamp01(radius - d + 0.5f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            s_Rounded = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                                      SpriteMeshType.FullRect, new Vector4(radius + 2, radius + 2, radius + 2, radius + 2));
            s_Rounded.name = "ReefRounded";
            return s_Rounded;
        }
    }
}
