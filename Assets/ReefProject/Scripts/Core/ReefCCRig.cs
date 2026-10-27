using System.Collections.Generic;
using UnityEngine;

namespace Reef
{
    /// <summary>
    /// Finds and caches the important parts of the CyberCANOE rig (CC_HUB / CC_HEAD / CC_FRONT
    /// and the three cameras) so every other reef script can ask one place for
    /// "where is the viewer" and "which way is the wall".
    ///
    /// Put this on CC_HUB (the Reef Builder does it for you).
    ///
    /// Why this matters on the wall: the cameras use an off-axis projection built from
    /// CC_HEAD and the CC_FRONT screen quad, so camera.transform.forward is NOT a reliable
    /// "look direction". The true view direction is head -> centre of the physical screen.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public class ReefCCRig : MonoBehaviour
    {
        public static ReefCCRig Instance { get; private set; }

        [Tooltip("CC_HEAD (the point between the viewer's eyes). Found automatically if empty.")]
        public Transform head;

        [Tooltip("CC_FRONT (the quad that represents the physical display wall). Found automatically if empty.")]
        public Transform frontScreen;

        [Tooltip("All cameras under CC_HUB (CenterCamera, CC_LEFT_CAMERA, CC_RIGHT_CAMERA). Found automatically if empty.")]
        public Camera[] cameras;

        /// <summary>The CC_HUB transform. Move this to move the viewer.</summary>
        public Transform Hub => transform;

        /// <summary>World position of the viewer's head (midpoint between the eyes).</summary>
        public Vector3 ViewOrigin => head != null ? head.position : transform.position + Vector3.up * 1.76f;

        /// <summary>World direction from the head through the centre of the wall.</summary>
        public Vector3 ViewForward
        {
            get
            {
                if (head == null || frontScreen == null) return transform.forward;
                Vector3 d = frontScreen.position - head.position;
                return d.sqrMagnitude > 1e-6f ? d.normalized : transform.forward;
            }
        }

        /// <summary>World-space centre of the physical wall.</summary>
        public Vector3 ScreenCenter => frontScreen != null ? frontScreen.position : ViewOrigin + ViewForward * 4f;

        /// <summary>Distance from the head to the wall. Objects at this distance have zero stereo parallax.</summary>
        public float ScreenDistance => head != null && frontScreen != null ? Vector3.Distance(head.position, frontScreen.position) : 4f;

        /// <summary>Physical wall width and height in metres (CC_FRONT's scale).</summary>
        public Vector2 ScreenSize => frontScreen != null ? new Vector2(Mathf.Abs(frontScreen.lossyScale.x), Mathf.Abs(frontScreen.lossyScale.y)) : new Vector2(6.19f, 1.75f);

        /// <summary>Head position in CC_HUB local space.</summary>
        public Vector3 HeadLocal => head != null ? transform.InverseTransformPoint(head.position) : Vector3.up * 1.76f;

        void Awake()
        {
            Instance = this;
            AutoFind();
        }

        void OnEnable()
        {
            if (Instance == null) Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Locate CC_HEAD, CC_FRONT and cameras by name if they were not assigned.</summary>
        public void AutoFind()
        {
            if (head == null) head = FindDeep(transform, "CC_HEAD");
            if (frontScreen == null) frontScreen = FindDeep(transform, "CC_FRONT");
            if (cameras == null || cameras.Length == 0) cameras = GetComponentsInChildren<Camera>(true);

            if (head == null) Debug.LogWarning("[Reef] ReefCCRig could not find CC_HEAD under " + name + ".", this);
            if (frontScreen == null) Debug.LogWarning("[Reef] ReefCCRig could not find CC_FRONT under " + name + ".", this);
        }

        /// <summary>Convenience lookup used by the other reef scripts.</summary>
        public static ReefCCRig Find()
        {
            if (Instance != null) return Instance;
            var rig = FindAnyObjectByType<ReefCCRig>();
            if (rig != null) { Instance = rig; rig.AutoFind(); }
            return rig;
        }

        public static Transform FindDeep(Transform root, string childName)
        {
            if (root == null) return null;
            if (root.name == childName) return root;
            var stack = new Stack<Transform>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                var t = stack.Pop();
                if (t.name == childName) return t;
                for (int i = 0; i < t.childCount; i++) stack.Push(t.GetChild(i));
            }
            return null;
        }

        void OnDrawGizmosSelected()
        {
            if (head == null || frontScreen == null) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(head.position, frontScreen.position);
            Gizmos.DrawWireSphere(head.position, 0.1f);
        }
    }
}
