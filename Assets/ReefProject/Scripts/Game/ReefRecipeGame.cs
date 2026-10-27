using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Reef
{
    /// <summary>
    /// The recipe collecting game. Put it on CC_HUB (Tools > Reef > Set Up Recipe Game does it).
    ///
    ///  1. START SCREEN: the recipes in a row with their pictures. D-pad / left stick to choose, A to start.
    ///  2. SWIM AND COLLECT: a small recipe card sits in the top-right corner of the wall. Aim at an
    ///     ingredient and press A: it is checked off. Once an ingredient has enough (e.g. 3 limu), no more
    ///     of it can be selected. Things that aren't in the recipe can still be examined for their info.
    ///  3. MAIKAʻI!: when everything is found, a card with the recipe's picture pops out with a
    ///     "Hana hou?" button. A takes you back to the start screen (and the reef is reset).
    ///  Menu (Start) button / M: give up and go back to the start screen.
    ///
    /// Everything is world-space UI parented to CC_HUB at a fixed depth just in front of the wall, so
    /// it stays put on the wall in stereo while you swim, and is drawn on top of rocks and fish.
    /// </summary>
    [DefaultExecutionOrder(5)]
    public class ReefRecipeGame : MonoBehaviour
    {
        public enum State { StartScreen, Playing, Complete }

        [Header("Recipes")]
        public List<ReefRecipe> recipes = new List<ReefRecipe>();

        [Header("Rules")]
        [Tooltip("Off: things that aren't in the recipe can still be examined for info. On: only the recipe's ingredients can be selected.")]
        public bool onlyIngredientsSelectable = false;
        [Tooltip("Glide back to the spawn point when returning to the start screen.")]
        public bool returnToSpawnOnNewGame = true;
        [Tooltip("Seconds between finding the last ingredient and the Maikaʻi! card.")]
        public float completeDelay = 1.4f;

        [Header("Placement (fraction of the way from the viewer to the wall; 1 = on the wall)")]
        [UnityEngine.Range(0.6f, 1f)] public float startScreenDepth = 0.93f;
        [UnityEngine.Range(0.6f, 1f)] public float trackerDepth = 0.86f;
        [UnityEngine.Range(0.6f, 1f)] public float completeDepth = 0.88f;
        [Tooltip("Gap between the tracker and the wall's top-right corner, metres.")]
        public float trackerMargin = 0.06f;
        [Tooltip("Tracker width in metres (as seen on the wall).")]
        public float trackerWidth = 0.95f;

        [Header("Text")]
        public string startTitle = "Choose a recipe";
        public string startHint = "D-pad or left stick to choose     [A] start";
        public string trackerHint = "Aim and press [A] to collect     [Menu] recipes";
        public string addedMessage = "<b>Added to your recipe!</b>";
        public string notInRecipeMessage = "Not in this recipe";
        public string completeTitle = "Maikaʻi!";
        public string completeLine = "You gathered everything for";
        public string againButton = "Hana hou?";

        [Header("Sound")]
        [Tooltip("Played when an ingredient is collected. Empty = a built-in two-note chime.")]
        public AudioClip collectSound;
        [UnityEngine.Range(0f, 1f)] public float collectVolume = 0.7f;

        [Header("Look")]
        [Tooltip("Use the Prompt typeface (Resources/Fonts). Off = use the font below, or TextMesh Pro's default.")]
        public bool usePromptFont = true;
        public TMP_FontAsset font;
        public Color panelColor = new Color(0.02f, 0.09f, 0.15f, 0.92f);
        public Color cardColor = new Color(0.06f, 0.18f, 0.27f, 0.96f);
        public Color accent = new Color(1f, 0.82f, 0.35f, 1f);
        public Color doneColor = new Color(0.55f, 0.95f, 0.6f, 1f);
        public Color textColor = new Color(0.94f, 0.97f, 1f, 1f);

        public State CurrentState { get; private set; } = State.StartScreen;
        public ReefRecipe CurrentRecipe { get; private set; }
        public bool IsComplete => CurrentRecipe != null && AllFound();

        const float UnitsPerMeter = 600f;

        ReefCCRig rig;
        SpeciesSelector selector;
        SwimController swim;
        int[] found = new int[0];
        readonly List<SelectableSpecies> collected = new List<SelectableSpecies>();
        int menuIndex;
        int stateFrame;
        float stateTime;
        bool stickArmed = true;
        float stickRepeatAt;
        bool firstMenu = true;
        Coroutine pending;
        AudioSource sfx;
        static AudioClip s_Chime;

        // start screen
        Canvas startCanvas;
        CanvasGroup startGroup;
        readonly List<RectTransform> cards = new List<RectTransform>();
        readonly List<Image> cardFrames = new List<Image>();

        // tracker
        Canvas trackerCanvas;
        CanvasGroup trackerGroup;
        RawImage trackerImage;
        TextMeshProUGUI trackerTitle, trackerSubtitle;
        RectTransform rowsRoot;
        readonly List<Row> rows = new List<Row>();

        // complete
        Canvas completeCanvas;
        CanvasGroup completeGroup;
        RawImage completeImage;
        TextMeshProUGUI completeName, completeEnglish;
        RectTransform completeButton;

        class Row
        {
            public RectTransform rt;
            public Image box, check;
            public TextMeshProUGUI label, count;
            public float pulse;
        }

        // ------------------------------------------------------------------ setup

        void Start()
        {
            rig = GetComponent<ReefCCRig>();
            if (rig == null) rig = ReefCCRig.Find();
            selector = GetComponent<SpeciesSelector>();
            if (selector == null) selector = FindAnyObjectByType<SpeciesSelector>();
            swim = GetComponent<SwimController>();
            if (swim == null) swim = FindAnyObjectByType<SwimController>();

            recipes.RemoveAll(r => r == null);
            if (recipes.Count == 0)
            {
                Debug.LogWarning("[Reef] ReefRecipeGame has no recipes. Run Tools > Reef > Set Up Recipe Game.", this);
                enabled = false;
                return;
            }
            if (rig == null)
            {
                Debug.LogWarning("[Reef] ReefRecipeGame could not find the CC_HUB rig (ReefCCRig).", this);
                enabled = false;
                return;
            }

            font = ReefFonts.Pick(usePromptFont, font);
            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            sfx.spatialBlend = 0f;
            if (collectSound == null) collectSound = Chime();

            BuildStartScreen();
            BuildTracker();
            BuildComplete();

            SpeciesSelector.Examined += OnExamined;
            SpeciesSelector.SelectionFilter = CanSelect;
            EnterStartScreen();
        }

        void OnDestroy()
        {
            SpeciesSelector.Examined -= OnExamined;
            if (SpeciesSelector.SelectionFilter == (System.Func<SelectableSpecies, bool>)CanSelect) SpeciesSelector.SelectionFilter = null;
        }

        // ------------------------------------------------------------------ states

        void SetState(State s)
        {
            CurrentState = s;
            stateFrame = Time.frameCount;
            stateTime = Time.time;
        }

        /// <summary>Show the recipe picker and reset the reef (collected things come back).</summary>
        public void EnterStartScreen()
        {
            if (pending != null) { StopCoroutine(pending); pending = null; }
            SetState(State.StartScreen);
            SetSelector(false);
            LockSwim(true);

            foreach (var s in collected) if (s != null) s.ResetCollected();
            collected.Clear();
            CurrentRecipe = null;

            if (!firstMenu && returnToSpawnOnNewGame && swim != null) swim.BeginReturnToStart();
            firstMenu = false;

            menuIndex = Mathf.Clamp(menuIndex, 0, recipes.Count - 1);
            HighlightCard(true);
        }

        /// <summary>Start a recipe (index into Recipes).</summary>
        public void StartRecipe(int index)
        {
            if (index < 0 || index >= recipes.Count) return;
            CurrentRecipe = recipes[index];
            found = new int[CurrentRecipe.ingredients != null ? CurrentRecipe.ingredients.Count : 0];
            collected.Clear();
            FillTracker();
            SetState(State.Playing);
            LockSwim(false);
            // Turn selection on next frame so this A press doesn't also select something.
            if (pending != null) StopCoroutine(pending);
            pending = StartCoroutine(EnableSelectorNextFrame());
        }

        IEnumerator EnableSelectorNextFrame()
        {
            yield return null;
            pending = null;
            if (CurrentState == State.Playing) SetSelector(true);
        }

        IEnumerator CompleteAfterDelay()
        {
            yield return new WaitForSeconds(completeDelay);
            pending = null;
            if (CurrentState != State.Playing) yield break;
            SetState(State.Complete);
            SetSelector(false);
            LockSwim(true);
            FillComplete();
        }

        void SetSelector(bool on)
        {
            if (selector != null && selector.enabled != on) selector.enabled = on;
        }

        void LockSwim(bool locked)
        {
            if (swim == null) return;
            swim.movementLocked = locked;
            swim.lookLocked = locked;
        }

        // ------------------------------------------------------------------ rules

        /// <summary>Used by SpeciesSelector: may this be targeted right now?</summary>
        bool CanSelect(SelectableSpecies s)
        {
            if (CurrentState != State.Playing || CurrentRecipe == null) return true;
            var d = s.species;
            bool isIngredient = false;
            var list = CurrentRecipe.ingredients;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == null || !list[i].Accepts(d)) continue;
                isIngredient = true;
                if (found[i] < list[i].count) return true;
            }
            return isIngredient ? false : !onlyIngredientsSelectable;
        }

        int OpenSlotFor(SpeciesData d)
        {
            if (CurrentRecipe == null || d == null) return -1;
            var list = CurrentRecipe.ingredients;
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null && list[i].Accepts(d) && found[i] < list[i].count) return i;
            return -1;
        }

        bool AllFound()
        {
            var list = CurrentRecipe.ingredients;
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null && found[i] < list[i].count) return false;
            return true;
        }

        void OnExamined(SelectableSpecies s)
        {
            if (CurrentState != State.Playing || s == null || selector == null) return;
            int slot = OpenSlotFor(s.species);
            if (slot < 0 || s.IsCollected)
            {
                selector.ShowCardMessage(s, "<alpha=#AA>" + notInRecipeMessage + "</alpha>      [B] close");
                return;
            }

            found[slot]++;
            collected.Add(s);
            var ing = CurrentRecipe.ingredients[slot];
            selector.CollectWithMessage(s, addedMessage + "   " + ing.label + " " + found[slot] + "/" + ing.count);
            RefreshRows();
            rows[slot].pulse = 1f;
            if (sfx != null && collectSound != null) sfx.PlayOneShot(collectSound, collectVolume);

            if (AllFound())
            {
                if (pending != null) StopCoroutine(pending);
                pending = StartCoroutine(CompleteAfterDelay());
            }
        }

        /// <summary>A short, bright two-note chime (C6 then G6) made in code, so no audio file is needed.</summary>
        static AudioClip Chime()
        {
            if (s_Chime != null) return s_Chime;
            const int rate = 44100;
            const float length = 0.75f;
            int n = (int)(rate * length);
            var data = new float[n];
            float[] freqs = { 1046.5f, 1568f };
            float[] starts = { 0f, 0.09f };
            float peak = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float v = 0f;
                for (int k = 0; k < freqs.Length; k++)
                {
                    float lt = t - starts[k];
                    if (lt < 0f) continue;
                    float env = Mathf.Min(1f, lt / 0.004f) * Mathf.Exp(-lt / 0.22f);
                    float w = 2f * Mathf.PI * freqs[k] * lt;
                    v += env * (Mathf.Sin(w) + 0.25f * Mathf.Sin(2f * w) + 0.08f * Mathf.Sin(3f * w));
                }
                data[i] = v;
                peak = Mathf.Max(peak, Mathf.Abs(v));
            }
            float norm = peak > 0f ? 0.9f / peak : 1f;
            for (int i = 0; i < n; i++) data[i] *= norm;
            s_Chime = AudioClip.Create("ReefCollectChime", n, 1, rate, false);
            s_Chime.SetData(data, 0);
            return s_Chime;
        }

        // ------------------------------------------------------------------ per frame

        void Update()
        {
            bool fresh = Time.frameCount == stateFrame; // ignore the button press that changed the state
            switch (CurrentState)
            {
                case State.StartScreen:
                    if (!fresh) UpdateStartInput();
                    break;
                case State.Playing:
                    if (!fresh && ReefInput.BackToMenuPressed) EnterStartScreen();
                    break;
                case State.Complete:
                    if (!fresh && Time.time - stateTime > 0.6f && (ReefInput.ConfirmPressed || ReefInput.BackToMenuPressed))
                        EnterStartScreen();
                    break;
            }
        }

        void UpdateStartInput()
        {
            int dir = 0;
            if (ReefInput.MenuLeftPressed) dir = -1;
            if (ReefInput.MenuRightPressed) dir = +1;

            float x = ReefInput.MenuStickX;
            if (Mathf.Abs(x) < 0.3f) stickArmed = true;
            else if (Mathf.Abs(x) > 0.6f && (stickArmed || Time.time >= stickRepeatAt))
            {
                dir = x > 0f ? 1 : -1;
                stickRepeatAt = Time.time + (stickArmed ? 0.45f : 0.25f);
                stickArmed = false;
            }

            if (dir != 0)
            {
                menuIndex = (menuIndex + dir + recipes.Count) % recipes.Count;
                HighlightCard(false);
            }
            if (ReefInput.ConfirmPressed) StartRecipe(menuIndex);
        }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            Fade(startGroup, startCanvas, CurrentState == State.StartScreen, dt);
            Fade(trackerGroup, trackerCanvas, CurrentState == State.Playing, dt);
            Fade(completeGroup, completeCanvas, CurrentState == State.Complete, dt);

            if (startCanvas != null && startCanvas.enabled)
            {
                PlaceCentered((RectTransform)startCanvas.transform, startScreenDepth, 0.97f, 0.94f);
                for (int i = 0; i < cards.Count; i++)
                {
                    float want = i == menuIndex ? 1.06f : 1f;
                    float s = Mathf.Lerp(cards[i].localScale.x, want, 1f - Mathf.Exp(-14f * dt));
                    cards[i].localScale = new Vector3(s, s, 1f);
                }
                if (menuIndex < cardFrames.Count)
                {
                    var c = accent;
                    c.a = 0.75f + 0.25f * Mathf.Sin(Time.time * 4f);
                    cardFrames[menuIndex].color = c;
                }
            }

            if (trackerCanvas != null && trackerCanvas.enabled)
            {
                PlaceTopRight((RectTransform)trackerCanvas.transform);
                foreach (var r in rows)
                {
                    if (r.pulse <= 0f) { r.rt.localScale = Vector3.one; continue; }
                    r.pulse = Mathf.Max(0f, r.pulse - dt * 1.6f);
                    float s = 1f + 0.12f * Mathf.Sin(r.pulse * Mathf.PI);
                    r.rt.localScale = new Vector3(s, s, 1f);
                }
            }

            if (completeCanvas != null && completeCanvas.enabled)
            {
                var rt = (RectTransform)completeCanvas.transform;
                PlaceCentered(rt, completeDepth, 0.8f, 0.92f);
                float t = Mathf.Clamp01((Time.time - stateTime) / 0.45f);
                float pop = CurrentState == State.Complete ? EaseOutBack(t) : 1f;
                rt.localScale *= Mathf.Lerp(0.6f, 1f, pop);
                float b = 1f + 0.05f * Mathf.Sin(Time.time * 5f);
                completeButton.localScale = new Vector3(b, b, 1f);
            }
        }

        static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        static void Fade(CanvasGroup g, Canvas c, bool show, float dt)
        {
            if (g == null) return;
            g.alpha = Mathf.MoveTowards(g.alpha, show ? 1f : 0f, dt / 0.25f);
            c.enabled = g.alpha > 0.001f;
        }

        // ------------------------------------------------------------------ placement on the wall

        /// <summary>
        /// Positions a canvas in CC_HUB space on the line from the head to a point on the wall, at a fraction
        /// of the way there. Scaling by that fraction keeps its apparent size the same as if it were on the wall.
        /// </summary>
        void PlaceOnWall(RectTransform rt, Vector2 wallPointFromCentre, float depth, float scaleOnWall)
        {
            Transform hub = rig.Hub;
            if (rt.parent != hub) rt.SetParent(hub, false);
            Vector3 headL = rig.HeadLocal;
            Vector3 screenL = hub.InverseTransformPoint(rig.ScreenCenter);
            Vector3 p = screenL + new Vector3(wallPointFromCentre.x, wallPointFromCentre.y, 0f);
            rt.localPosition = headL + (p - headL) * depth;
            rt.localRotation = Quaternion.identity;
            float s = scaleOnWall * depth / UnitsPerMeter;
            rt.localScale = new Vector3(s, s, s);
        }

        void PlaceCentered(RectTransform rt, float depth, float maxWidthFraction, float maxHeightFraction)
        {
            Vector2 wall = rig.ScreenSize;
            Vector2 size = rt.sizeDelta / UnitsPerMeter;
            float fit = Mathf.Min(wall.x * maxWidthFraction / size.x, wall.y * maxHeightFraction / size.y);
            rt.pivot = new Vector2(0.5f, 0.5f);
            PlaceOnWall(rt, Vector2.zero, depth, fit);
        }

        void PlaceTopRight(RectTransform rt)
        {
            Vector2 wall = rig.ScreenSize;
            Vector2 size = rt.rect.size / UnitsPerMeter;
            float fit = Mathf.Min(trackerWidth / Mathf.Max(0.01f, size.x), (wall.y - 2f * trackerMargin) / Mathf.Max(0.01f, size.y));
            rt.pivot = new Vector2(1f, 1f);
            PlaceOnWall(rt, new Vector2(wall.x * 0.5f - trackerMargin, wall.y * 0.5f - trackerMargin), trackerDepth, fit);
        }

        // ------------------------------------------------------------------ start screen

        void BuildStartScreen()
        {
            const float W = 3540f, H = 980f, pad = 60f, top = 150f, bottom = 90f, gap = 36f;
            startCanvas = ReefUI.MakeCanvas("Reef Start Screen", rig.Hub, new Vector2(W, H), 40);
            startGroup = startCanvas.GetComponent<CanvasGroup>();
            startGroup.alpha = 0f;
            var root = (RectTransform)startCanvas.transform;

            var bg = ReefUI.MakePanel("Background", root, panelColor);
            ReefUI.Stretch(bg.rectTransform, 0, 0, 0, 0);

            var title = ReefUI.MakeText("Title", root, font, 78f, FontStyles.Bold, textColor, TextAlignmentOptions.Center);
            ReefUI.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(W - 2 * pad, 100f));
            title.text = startTitle;

            var hint = ReefUI.MakeText("Hint", root, font, 34f, FontStyles.Normal, new Color(1f, 1f, 1f, 0.7f), TextAlignmentOptions.Center);
            ReefUI.Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(W - 2 * pad, 50f));
            hint.text = startHint;

            int n = recipes.Count;
            float cardW = (W - 2 * pad - (n - 1) * gap) / n;
            float cardH = H - top - bottom;
            float imgSize = Mathf.Min(cardW - 48f, cardH - 230f);
            for (int i = 0; i < n; i++)
            {
                var r = recipes[i];
                var card = ReefUI.NewRect("Card " + (i + 1), root);
                ReefUI.Place(card, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f),
                             new Vector2(pad + cardW * 0.5f + i * (cardW + gap), -top - cardH * 0.5f), new Vector2(cardW, cardH));

                var frame = ReefUI.MakePanel("Frame", card, accent, 0.5f);
                ReefUI.Stretch(frame.rectTransform, -12, -12, -12, -12);
                frame.enabled = false;
                cardFrames.Add(frame);

                var cbg = ReefUI.MakePanel("Card", card, cardColor, 0.5f);
                ReefUI.Stretch(cbg.rectTransform, 0, 0, 0, 0);

                var img = ReefUI.MakeRawImage("Picture", card);
                ReefUI.Place(img.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(imgSize, imgSize));
                ReefUI.SetCover(img, r.image);

                float y = -24f - imgSize - 14f;
                var name = ReefUI.MakeText("Name", card, font, 50f, FontStyles.Bold, textColor, TextAlignmentOptions.Top);
                ReefUI.Place(name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(cardW - 32f, 62f));
                name.textWrappingMode = TextWrappingModes.NoWrap;
                name.enableAutoSizing = true;
                name.fontSizeMin = 30f;
                name.fontSizeMax = 50f;
                name.text = r.DisplayTitle;

                y -= 64f;
                var sub = ReefUI.MakeText("English", card, font, 34f, FontStyles.Italic, new Color(0.85f, 0.93f, 1f, 1f), TextAlignmentOptions.Top);
                ReefUI.Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(cardW - 32f, 44f));
                sub.textWrappingMode = TextWrappingModes.NoWrap;
                sub.enableAutoSizing = true;
                sub.fontSizeMin = 22f;
                sub.fontSizeMax = 34f;
                sub.text = r.DisplaySubtitle;

                y -= 50f;
                var ing = ReefUI.MakeText("Ingredients", card, font, 27f, FontStyles.Normal, new Color(1f, 1f, 1f, 0.78f), TextAlignmentOptions.Top);
                ReefUI.Place(ing.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(cardW - 40f, cardH + y - 12f));
                ing.enableAutoSizing = true;
                ing.fontSizeMin = 18f;
                ing.fontSizeMax = 27f;
                ing.text = r.IngredientSummary;

                cards.Add(card);
            }
        }

        void HighlightCard(bool snap)
        {
            for (int i = 0; i < cardFrames.Count; i++) cardFrames[i].enabled = i == menuIndex;
            if (snap) foreach (var c in cards) c.localScale = Vector3.one;
        }

        // ------------------------------------------------------------------ tracker (top right)

        void BuildTracker()
        {
            const float W = 690f;
            trackerCanvas = ReefUI.MakeCanvas("Reef Recipe Tracker", rig.Hub, new Vector2(W, 600f), 35);
            trackerGroup = trackerCanvas.GetComponent<CanvasGroup>();
            trackerGroup.alpha = 0f;
            var root = (RectTransform)trackerCanvas.transform;

            var bg = ReefUI.MakePanel("Background", root, panelColor);
            ReefUI.Stretch(bg.rectTransform, 0, 0, 0, 0);
            var bar = ReefUI.MakeImage("Accent", root, accent);
            bar.rectTransform.anchorMin = new Vector2(0f, 1f);
            bar.rectTransform.anchorMax = new Vector2(1f, 1f);
            bar.rectTransform.pivot = new Vector2(0.5f, 1f);
            bar.rectTransform.offsetMin = new Vector2(32f, -12f);
            bar.rectTransform.offsetMax = new Vector2(-32f, -5f);

            var col = root.gameObject.AddComponent<VerticalLayoutGroup>();
            col.padding = new RectOffset(32, 32, 30, 24);
            col.spacing = 14f;
            col.childControlWidth = true;
            col.childControlHeight = true;
            col.childForceExpandWidth = true;
            col.childForceExpandHeight = false;
            var fitter = root.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            // The background and accent bar are ignored by the layout.
            bg.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            bar.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            // Header: picture + names
            var header = ReefUI.NewRect("Header", root);
            var hl = header.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 22f;
            hl.childAlignment = TextAnchor.MiddleLeft;
            hl.childControlWidth = true;
            hl.childControlHeight = true;
            hl.childForceExpandWidth = false;
            hl.childForceExpandHeight = false;
            header.gameObject.AddComponent<LayoutElement>().preferredHeight = 180f;

            trackerImage = ReefUI.MakeRawImage("Picture", header);
            var ple = trackerImage.gameObject.AddComponent<LayoutElement>();
            ple.preferredWidth = ple.minWidth = 180f;
            ple.preferredHeight = ple.minHeight = 180f;

            var names = ReefUI.NewRect("Names", header);
            var nl = names.gameObject.AddComponent<VerticalLayoutGroup>();
            nl.childAlignment = TextAnchor.MiddleLeft;
            nl.childControlWidth = true;
            nl.childControlHeight = true;
            nl.childForceExpandWidth = true;
            nl.childForceExpandHeight = false;
            nl.spacing = 4f;
            names.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            var label = ReefUI.MakeText("Label", names, font, 24f, FontStyles.Bold | FontStyles.UpperCase, accent);
            label.characterSpacing = 6f;
            label.text = "Your recipe";
            trackerTitle = ReefUI.MakeText("Title", names, font, 50f, FontStyles.Bold, textColor);
            trackerSubtitle = ReefUI.MakeText("English", names, font, 30f, FontStyles.Italic, new Color(0.85f, 0.93f, 1f, 1f));

            var divider = ReefUI.MakeImage("Divider", root, new Color(1f, 1f, 1f, 0.18f));
            divider.gameObject.AddComponent<LayoutElement>().preferredHeight = 3f;

            rowsRoot = ReefUI.NewRect("Ingredients", root);
            var rl = rowsRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            rl.spacing = 8f;
            rl.childControlWidth = true;
            rl.childControlHeight = true;
            rl.childForceExpandWidth = true;
            rl.childForceExpandHeight = false;

            var hint = ReefUI.MakeText("Hint", root, font, 24f, FontStyles.Normal, new Color(1f, 1f, 1f, 0.6f));
            hint.text = trackerHint;
        }

        void FillTracker()
        {
            var r = CurrentRecipe;
            trackerTitle.text = r.DisplayTitle;
            trackerSubtitle.text = r.DisplaySubtitle;
            trackerSubtitle.gameObject.SetActive(!string.IsNullOrEmpty(r.DisplaySubtitle));
            trackerImage.rectTransform.sizeDelta = new Vector2(180f, 180f);
            ReefUI.SetCover(trackerImage, r.image);

            foreach (var row in rows) if (row.rt != null) Destroy(row.rt.gameObject);
            rows.Clear();
            for (int i = 0; i < r.ingredients.Count; i++)
            {
                var row = new Row();
                row.rt = ReefUI.NewRect("Row " + (i + 1), rowsRoot);
                var h = row.rt.gameObject.AddComponent<HorizontalLayoutGroup>();
                h.spacing = 18f;
                h.childAlignment = TextAnchor.MiddleLeft;
                h.childControlWidth = true;
                h.childControlHeight = true;
                h.childForceExpandWidth = false;
                h.childForceExpandHeight = false;
                row.rt.gameObject.AddComponent<LayoutElement>().preferredHeight = 54f;

                var boxRt = ReefUI.NewRect("Box", row.rt);
                var ble = boxRt.gameObject.AddComponent<LayoutElement>();
                ble.preferredWidth = ble.minWidth = 46f;
                ble.preferredHeight = ble.minHeight = 46f;
                row.box = ReefUI.MakeImage("Outline", boxRt, new Color(1f, 1f, 1f, 0.8f), ReefUI.BoxSprite());
                ReefUI.Stretch(row.box.rectTransform, 0, 0, 0, 0);
                row.check = ReefUI.MakeImage("Check", boxRt, doneColor, ReefUI.CheckSprite());
                ReefUI.Stretch(row.check.rectTransform, -6, -6, -10, 2);

                row.label = ReefUI.MakeText("Label", row.rt, font, 36f, FontStyles.Normal, textColor, TextAlignmentOptions.MidlineLeft);
                row.label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
                row.label.text = r.ingredients[i].label;
                row.count = ReefUI.MakeText("Count", row.rt, font, 36f, FontStyles.Bold, textColor, TextAlignmentOptions.MidlineRight);
                row.count.gameObject.AddComponent<LayoutElement>().preferredWidth = 90f;
                rows.Add(row);
            }
            RefreshRows();
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)trackerCanvas.transform);
        }

        void RefreshRows()
        {
            var list = CurrentRecipe.ingredients;
            for (int i = 0; i < rows.Count && i < list.Count; i++)
            {
                bool done = found[i] >= list[i].count;
                var row = rows[i];
                row.check.enabled = done;
                row.box.color = done ? doneColor : new Color(1f, 1f, 1f, 0.8f);
                row.label.color = done ? doneColor : textColor;
                row.label.fontStyle = done ? FontStyles.Strikethrough : FontStyles.Normal;
                row.count.color = done ? doneColor : textColor;
                row.count.text = found[i] + "/" + list[i].count;
            }
        }

        // ------------------------------------------------------------------ Maikaʻi! card

        void BuildComplete()
        {
            const float W = 2700f, H = 980f, pad = 70f;
            completeCanvas = ReefUI.MakeCanvas("Reef Recipe Complete", rig.Hub, new Vector2(W, H), 50);
            completeGroup = completeCanvas.GetComponent<CanvasGroup>();
            completeGroup.alpha = 0f;
            var root = (RectTransform)completeCanvas.transform;

            var bg = ReefUI.MakePanel("Background", root, panelColor);
            ReefUI.Stretch(bg.rectTransform, 0, 0, 0, 0);
            var frame = ReefUI.MakePanel("Frame", root, accent);
            ReefUI.Stretch(frame.rectTransform, -10, -10, -10, -10);
            frame.transform.SetAsFirstSibling();

            float img = H - 2 * pad;
            completeImage = ReefUI.MakeRawImage("Picture", root);
            ReefUI.Place(completeImage.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(pad, 0f), new Vector2(img, img));

            float x0 = pad + img + 80f, colW = W - x0 - pad;
            var col = ReefUI.NewRect("Column", root);
            ReefUI.Place(col, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(x0, 0f), new Vector2(colW, img));

            var title = ReefUI.MakeText("Maikai", col, font, 190f, FontStyles.Bold, accent, TextAlignmentOptions.Center);
            ReefUI.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(colW, 230f));
            title.text = completeTitle;

            var line = ReefUI.MakeText("Line", col, font, 44f, FontStyles.Normal, textColor, TextAlignmentOptions.Center);
            ReefUI.Place(line.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -250f), new Vector2(colW, 60f));
            line.text = completeLine;

            completeName = ReefUI.MakeText("Name", col, font, 84f, FontStyles.Bold, textColor, TextAlignmentOptions.Center);
            ReefUI.Place(completeName.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -320f), new Vector2(colW, 100f));
            completeName.textWrappingMode = TextWrappingModes.NoWrap;
            completeName.enableAutoSizing = true;
            completeName.fontSizeMin = 40f;
            completeName.fontSizeMax = 84f;

            completeEnglish = ReefUI.MakeText("English", col, font, 44f, FontStyles.Italic, new Color(0.85f, 0.93f, 1f, 1f), TextAlignmentOptions.Center);
            ReefUI.Place(completeEnglish.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -425f), new Vector2(colW, 60f));

            completeButton = ReefUI.NewRect("Button", col);
            ReefUI.Place(completeButton, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 110f), new Vector2(620f, 150f));
            var btn = ReefUI.MakePanel("Fill", completeButton, accent, 0.4f);
            ReefUI.Stretch(btn.rectTransform, 0, 0, 0, 0);
            var btnText = ReefUI.MakeText("Text", completeButton, font, 76f, FontStyles.Bold, new Color(0.04f, 0.1f, 0.16f, 1f), TextAlignmentOptions.Center);
            ReefUI.Stretch(btnText.rectTransform, 0, 0, 0, 0);
            btnText.text = againButton;

            var hint = ReefUI.MakeText("Hint", col, font, 32f, FontStyles.Normal, new Color(1f, 1f, 1f, 0.7f), TextAlignmentOptions.Center);
            ReefUI.Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, 20f), new Vector2(colW, 44f));
            hint.text = "Press [A]";
        }

        void FillComplete()
        {
            var r = CurrentRecipe;
            ReefUI.SetCover(completeImage, r.image);
            completeName.text = r.DisplayTitle;
            completeEnglish.text = r.DisplaySubtitle;
        }
    }
}
