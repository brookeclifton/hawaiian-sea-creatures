using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Reef
{
    [Serializable] public class SelectableEvent : UnityEvent<SelectableSpecies> { }

    /// <summary>
    /// Finds the species the viewer is looking at, highlights it, and lets them examine it
    /// (info panel) or, if the game is turned on, collect it.
    ///
    /// Put this on CC_HUB. It works from the true view ray (head through the centre of the wall),
    /// scores every SelectableSpecies inside a cone, prefers the one nearest the centre, and checks
    /// that rock or coral is not in the way. Small objects get a minimum "capture radius" so a
    /// 3 cm shark tooth is as easy to pick as a turtle.
    ///
    /// Buttons: A examine, B close, X collect (when collecting is on), D-pad left/right cycles
    /// between everything currently in the cone.
    ///
    /// Hook gameplay in through the events (onTargetChanged / onExamined / onCollected) or
    /// the static C# events, so "read about it" and "add it to a basket" can be swapped
    /// without touching this script.
    /// </summary>
    [DefaultExecutionOrder(10)]
    public class SpeciesSelector : MonoBehaviour
    {
        [Header("Aiming")]
        [Tooltip("How far away something can be selected, in metres. Keep it inside the fog visibility.")]
        public float maxDistance = 10f;
        [Tooltip("Half-angle of the aiming cone in degrees. The wall is ~72 degrees wide; 10-14 feels right.")]
        public float coneAngle = 12f;
        [Tooltip("Lower = the current target is 'stickier' and harder to steal (reduces flicker).")]
        [UnityEngine.Range(0.3f, 1f)] public float stickiness = 0.7f;
        [Tooltip("Rock/coral/terrain layers that block the view to a target.")]
        public LayerMask occlusionMask = 1;
        public bool checkOcclusion = true;
        [Tooltip("Line-of-sight raycasts allowed per frame.")]
        public int maxOcclusionChecksPerFrame = 6;

        [Header("Parts (created automatically if empty)")]
        public SelectionReticle reticle;
        public SpeciesInfoPanel infoPanel;
        [Tooltip("Plays SpeciesData.namePronunciation when a panel opens.")]
        public AudioSource voice;

        [Header("Collecting (optional game)")]
        [Tooltip("Turn on to let X add the target to the basket.")]
        public bool collectingEnabled = false;
        public SpeciesBasket basket;

        [Header("Behaviour")]
        [Tooltip("Also draw a glowing rim on the targeted model. Off = only the selection ring shows.")]
        public bool highlightModel = false;
        [Tooltip("Hold the examined animal still (no swimming or bobbing) while its info panel is open, so it can be read.")]
        public bool holdStillWhileExamining = true;
        [Tooltip("Keep the examined species highlighted while its panel is open, even if you look away.")]
        public bool lockTargetWhileExamining = true;
        [Tooltip("Close the panel automatically if the examined species gets this far away (0 = never).")]
        public float autoCloseDistance = 25f;

        [Header("Events")]
        public SelectableEvent onTargetChanged = new SelectableEvent();
        public SelectableEvent onExamined = new SelectableEvent();
        public SelectableEvent onCollected = new SelectableEvent();

        public static event Action<SelectableSpecies> TargetChanged;
        public static event Action<SelectableSpecies> Examined;
        public static event Action<SelectableSpecies> CollectedAny;

        /// <summary>
        /// Optional rule from a game (e.g. the recipe game): return false to make something
        /// impossible to target right now (for example, seaweed once the recipe has enough).
        /// </summary>
        public static Func<SelectableSpecies, bool> SelectionFilter;

        public SelectableSpecies CurrentTarget { get; private set; }
        public SelectableSpecies ExaminedTarget { get; private set; }
        public IReadOnlyList<SelectableSpecies> CandidatesInView => candidateList;

        struct Candidate
        {
            public SelectableSpecies s;
            public float score;
            public float distance;
        }

        ReefCCRig rig;
        readonly List<Candidate> candidates = new List<Candidate>(64);
        readonly List<SelectableSpecies> candidateList = new List<SelectableSpecies>(64);
        SelectableSpecies manualTarget;
        float manualUntil;
        float closeAt = -1f;
        static readonly Comparison<Candidate> ByScore = (a, b) => a.score.CompareTo(b.score);

        void Start()
        {
            rig = GetComponent<ReefCCRig>();
            if (rig == null) rig = ReefCCRig.Find();

            if (reticle == null)
            {
                reticle = GetComponentInChildren<SelectionReticle>();
                if (reticle == null) reticle = new GameObject("ReefSelectionReticle").AddComponent<SelectionReticle>();
            }
            if (infoPanel == null)
            {
                infoPanel = GetComponentInChildren<SpeciesInfoPanel>();
                if (infoPanel == null)
                {
                    var go = new GameObject("ReefInfoPanel", typeof(RectTransform));
                    go.transform.SetParent(transform, false);
                    infoPanel = go.AddComponent<SpeciesInfoPanel>();
                }
            }
            if (voice == null)
            {
                voice = gameObject.AddComponent<AudioSource>();
                voice.playOnAwake = false;
                voice.spatialBlend = 0f;
            }
            if (basket == null) basket = GetComponent<SpeciesBasket>();
        }

        void OnEnable()
        {
            if (reticle != null) reticle.gameObject.SetActive(true);
        }

        /// <summary>Switching the selector off (e.g. while a menu is up) clears the highlight, ring and card.</summary>
        void OnDisable()
        {
            manualTarget = null;
            closeAt = -1f;
            if (CurrentTarget != null) CurrentTarget.SetHighlighted(false, Color.white);
            CurrentTarget = null;
            if (ExaminedTarget != null) ExaminedTarget.SetHeld(false);
            ExaminedTarget = null;
            if (infoPanel != null) infoPanel.Hide();
            if (reticle != null)
            {
                reticle.SetExamining(false);
                reticle.SetTarget(null);
                reticle.gameObject.SetActive(false);
            }
        }

        void Update()
        {
            if (rig == null) { rig = ReefCCRig.Find(); if (rig == null) return; }

            Vector3 origin = rig.ViewOrigin;
            Vector3 forward = rig.ViewForward;

            GatherCandidates(origin, forward);
            SetTarget(ChooseTarget(origin));

            // Examined species went away (collected, disabled, swam off)?
            if (ExaminedTarget != null)
            {
                if (ExaminedTarget.IsCollected)
                {
                    // Leave the "added to basket" message up for a moment.
                    if (closeAt < 0f) closeAt = Time.time + 1.8f;
                    else if (Time.time >= closeAt) CloseExamine();
                }
                else
                {
                    bool gone = ExaminedTarget == null || !ExaminedTarget.isActiveAndEnabled;
                    bool far = autoCloseDistance > 0f && Vector3.Distance(origin, ExaminedTarget.FocusPoint) > autoCloseDistance;
                    if (gone || far) CloseExamine();
                }
            }

            if (ReefInput.CycleNextPressed) Cycle(+1, origin);
            if (ReefInput.CyclePrevPressed) Cycle(-1, origin);

            if (ReefInput.ExaminePressed && CurrentTarget != null) Examine(CurrentTarget);
            if (ReefInput.ClosePressed) CloseExamine();

            if (collectingEnabled && ReefInput.CollectPressed)
            {
                var t = ExaminedTarget != null ? ExaminedTarget : CurrentTarget;
                if (t != null) TryCollect(t);
            }
        }

        // ------------------------------------------------------------------ targeting

        void GatherCandidates(Vector3 origin, Vector3 forward)
        {
            candidates.Clear();
            candidateList.Clear();
            var all = SelectableSpecies.All;
            for (int i = 0; i < all.Count; i++)
            {
                var s = all[i];
                if (s == null || !s.CanSelect) continue;
                if (SelectionFilter != null && !SelectionFilter(s)) continue;

                Vector3 to = s.FocusPoint - origin;
                float d = to.magnitude;
                float r = s.CaptureRadius;
                if (d < 0.3f || d - r > maxDistance) continue;

                float angle = Vector3.Angle(forward, to);
                float angularRadius = Mathf.Atan2(r, d) * Mathf.Rad2Deg;
                float effective = Mathf.Max(0f, angle - angularRadius);
                if (effective > coneAngle) continue;

                float score = (effective / coneAngle) * 0.65f + Mathf.Clamp01(d / maxDistance) * 0.35f;
                if (s == CurrentTarget) score *= stickiness;
                candidates.Add(new Candidate { s = s, score = score, distance = d });
            }
            candidates.Sort(ByScore);
            for (int i = 0; i < candidates.Count; i++) candidateList.Add(candidates[i].s);
        }

        SelectableSpecies ChooseTarget(Vector3 origin)
        {
            if (lockTargetWhileExamining && ExaminedTarget != null && ExaminedTarget.CanSelect)
                return ExaminedTarget;

            if (manualTarget != null)
            {
                if (Time.time < manualUntil && candidateList.Contains(manualTarget)) return manualTarget;
                manualTarget = null;
            }

            int checks = 0;
            for (int i = 0; i < candidates.Count; i++)
            {
                var c = candidates[i];
                if (!checkOcclusion) return c.s;
                if (checks >= maxOcclusionChecksPerFrame) break;
                checks++;
                if (IsVisible(origin, c.s)) return c.s;
            }
            return null;
        }

        /// <summary>True if nothing on the occlusion layers sits between the head and the target.</summary>
        public bool IsVisible(Vector3 origin, SelectableSpecies s)
        {
            Vector3 target = s.FocusPoint;
            Vector3 to = target - origin;
            float d = to.magnitude;
            if (d < 1e-3f) return true;
            float stopShort = Mathf.Min(s.CaptureRadius * 0.5f, d * 0.5f);
            if (Physics.Raycast(origin, to / d, out RaycastHit hit, d - stopShort, occlusionMask, QueryTriggerInteraction.Ignore))
            {
                return hit.collider.transform.IsChildOf(s.transform);
            }
            return true;
        }

        void SetTarget(SelectableSpecies s)
        {
            if (s == CurrentTarget)
            {
                if (s != null && highlightModel) s.SetHighlighted(true, s.AccentColor, s == ExaminedTarget ? 1.4f : 1f);
                return;
            }

            if (CurrentTarget != null) CurrentTarget.SetHighlighted(false, Color.white);
            CurrentTarget = s;
            if (s != null && highlightModel) s.SetHighlighted(true, s.AccentColor, s == ExaminedTarget ? 1.4f : 1f);
            if (reticle != null) reticle.SetTarget(s);

            onTargetChanged.Invoke(s);
            TargetChanged?.Invoke(s);
        }

        void Cycle(int direction, Vector3 origin)
        {
            if (candidateList.Count == 0) return;
            int start = CurrentTarget != null ? candidateList.IndexOf(CurrentTarget) : -1;
            for (int step = 1; step <= candidateList.Count; step++)
            {
                int idx = ((start + direction * step) % candidateList.Count + candidateList.Count) % candidateList.Count;
                var s = candidateList[idx];
                if (s == CurrentTarget) continue;
                if (checkOcclusion && !IsVisible(origin, s)) continue;
                manualTarget = s;
                manualUntil = Time.time + 3f;
                if (ExaminedTarget != null) CloseExamine();
                SetTarget(s);
                return;
            }
        }

        // ------------------------------------------------------------------ actions

        /// <summary>Open the info panel for a species.</summary>
        public void Examine(SelectableSpecies s)
        {
            if (s == null) return;
            closeAt = -1f;
            if (ExaminedTarget != null && ExaminedTarget != s) ExaminedTarget.SetHeld(false);
            ExaminedTarget = s;
            if (holdStillWhileExamining) s.SetHeld(true);
            if (infoPanel != null) infoPanel.Show(s, collectingEnabled, basket);
            if (reticle != null) reticle.SetExamining(true);
            if (voice != null && s.species != null && s.species.namePronunciation != null)
                voice.PlayOneShot(s.species.namePronunciation);
            if (highlightModel) s.SetHighlighted(true, s.AccentColor, 1.4f);
            onExamined.Invoke(s);
            Examined?.Invoke(s);
        }

        public void CloseExamine()
        {
            closeAt = -1f;
            if (ExaminedTarget == null && (infoPanel == null || !infoPanel.IsOpen)) return;
            if (ExaminedTarget != null) ExaminedTarget.SetHeld(false);
            ExaminedTarget = null;
            if (infoPanel != null) infoPanel.Hide();
            if (reticle != null) reticle.SetExamining(false);
        }

        /// <summary>Add a target to the basket (only when collecting is enabled).</summary>
        public bool TryCollect(SelectableSpecies s)
        {
            if (s == null || s.IsCollected) return false;
            if (s.species != null && !s.species.collectible) return false;

            bool added = basket != null ? basket.Add(s) : AddWithoutBasket(s);
            if (!added) return false;

            if (infoPanel != null && infoPanel.IsOpen && ExaminedTarget == s)
                infoPanel.ShowCollectedMessage(basket);

            onCollected.Invoke(s);
            CollectedAny?.Invoke(s);
            return true;
        }

        /// <summary>
        /// Mark something as collected for a game (bypasses the basket and the 'collectible' flag)
        /// and show a message on its open info card.
        /// </summary>
        public void CollectWithMessage(SelectableSpecies s, string message)
        {
            if (s == null || s.IsCollected) return;
            s.Collect();
            ShowCardMessage(s, message);
            onCollected.Invoke(s);
            CollectedAny?.Invoke(s);
        }

        /// <summary>Replace the hint line at the bottom of the open info card (if it belongs to this target).</summary>
        public void ShowCardMessage(SelectableSpecies s, string message)
        {
            if (infoPanel != null && infoPanel.IsOpen && ExaminedTarget == s && !string.IsNullOrEmpty(message))
                infoPanel.ShowFooterMessage(message);
        }

        static bool AddWithoutBasket(SelectableSpecies s)
        {
            s.Collect();
            return true;
        }
    }
}
