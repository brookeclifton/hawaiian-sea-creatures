using System.Collections.Generic;
using UnityEngine;

namespace Reef
{
    /// <summary>
    /// A smooth (Catmull-Rom) swimming route through this object's children, in order.
    /// Move the child waypoints in the Scene view to reshape it; the curve is drawn as a gizmo.
    /// Used by PathSwimmer for the "key characters" (tiger shark, turtles) so their routes are
    /// designed, predictable and pass nicely in front of the audience.
    /// </summary>
    public class SwimPath : MonoBehaviour
    {
        public bool closedLoop = true;
        [Range(4, 64)] public int samplesPerSegment = 24;
        public Color gizmoColor = new Color(1f, 0.5f, 0.2f, 0.9f);

        readonly List<Vector3> samples = new List<Vector3>();
        readonly List<float> cumulative = new List<float>();
        float length;
        bool built;

        public float Length { get { EnsureBuilt(); return length; } }

        void Awake() => Rebuild();
        void OnValidate() => built = false;

        void EnsureBuilt()
        {
            if (!built || (!Application.isPlaying && transform.hasChanged)) Rebuild();
        }

        public void Rebuild()
        {
            built = true;
            samples.Clear();
            cumulative.Clear();
            length = 0f;

            int n = transform.childCount;
            if (n < 2) return;
            var pts = new Vector3[n];
            for (int i = 0; i < n; i++) pts[i] = transform.GetChild(i).position;

            int segments = closedLoop ? n : n - 1;
            for (int s = 0; s < segments; s++)
            {
                Vector3 p0 = pts[Wrap(s - 1, n)];
                Vector3 p1 = pts[Wrap(s, n)];
                Vector3 p2 = pts[Wrap(s + 1, n)];
                Vector3 p3 = pts[Wrap(s + 2, n)];
                if (!closedLoop)
                {
                    if (s == 0) p0 = p1 + (p1 - p2);
                    if (s == segments - 1) p3 = p2 + (p2 - p1);
                }
                for (int k = 0; k < samplesPerSegment; k++)
                {
                    float t = k / (float)samplesPerSegment;
                    samples.Add(CatmullRom(p0, p1, p2, p3, t));
                }
            }
            samples.Add(closedLoop ? samples[0] : pts[n - 1]);

            cumulative.Add(0f);
            for (int i = 1; i < samples.Count; i++)
            {
                length += Vector3.Distance(samples[i - 1], samples[i]);
                cumulative.Add(length);
            }
        }

        static int Wrap(int i, int n) => ((i % n) + n) % n;

        static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        /// <summary>Point at a distance (metres) along the path. Wraps for loops, clamps otherwise.</summary>
        public Vector3 GetPoint(float distance)
        {
            EnsureBuilt();
            if (samples.Count == 0) return transform.position;
            if (samples.Count == 1 || length <= 1e-4f) return samples[0];
            distance = closedLoop ? Mathf.Repeat(distance, length) : Mathf.Clamp(distance, 0f, length);

            int lo = 0, hi = cumulative.Count - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (cumulative[mid] < distance) lo = mid; else hi = mid;
            }
            float segLen = cumulative[hi] - cumulative[lo];
            float f = segLen > 1e-5f ? (distance - cumulative[lo]) / segLen : 0f;
            return Vector3.Lerp(samples[lo], samples[hi], f);
        }

        public Vector3 GetDirection(float distance)
        {
            Vector3 a = GetPoint(distance - 0.25f);
            Vector3 b = GetPoint(distance + 0.25f);
            Vector3 d = b - a;
            return d.sqrMagnitude > 1e-8f ? d.normalized : transform.forward;
        }

        /// <summary>Distance along the path of the sample closest to a world point.</summary>
        public float ClosestDistance(Vector3 world)
        {
            EnsureBuilt();
            float best = float.MaxValue, at = 0f;
            for (int i = 0; i < samples.Count; i++)
            {
                float d = (samples[i] - world).sqrMagnitude;
                if (d < best) { best = d; at = cumulative[i]; }
            }
            return at;
        }

        void OnDrawGizmos()
        {
            Rebuild();
            if (samples.Count < 2) return;
            Gizmos.color = gizmoColor;
            for (int i = 1; i < samples.Count; i++) Gizmos.DrawLine(samples[i - 1], samples[i]);
            for (int i = 0; i < transform.childCount; i++) Gizmos.DrawWireSphere(transform.GetChild(i).position, 0.25f);
        }
    }
}
