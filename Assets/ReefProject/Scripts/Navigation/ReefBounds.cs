using UnityEngine;

namespace Reef
{
    /// <summary>
    /// The swimmable box for the reef patch, plus the spawn point.
    /// The swim controller slows the viewer down smoothly as they approach an edge
    /// (no invisible-wall "thunk", which feels bad on a big stereo wall) and never
    /// lets the head leave the box or break the water surface.
    /// </summary>
    public class ReefBounds : MonoBehaviour
    {
        [Tooltip("Size of the swimmable box in metres. The box is centred on this GameObject.")]
        public Vector3 size = new Vector3(80f, 24f, 80f);

        [Tooltip("Within this many metres of an edge, outward speed is progressively reduced.")]
        public float softMargin = 5f;

        [Tooltip("World height of the water surface. The head is kept this far below it.")]
        public float surfaceHeight = 12f;

        [Tooltip("Keep the viewer's head at least this far below the surface.")]
        public float surfaceClearance = 0.6f;

        [Tooltip("Where the viewer's HEAD starts. Its Y rotation sets the starting facing direction.")]
        public Transform spawnPoint;

        public Vector3 Min => transform.position - size * 0.5f;
        public Vector3 Max
        {
            get
            {
                Vector3 m = transform.position + size * 0.5f;
                m.y = Mathf.Min(m.y, surfaceHeight - surfaceClearance);
                return m;
            }
        }

        /// <summary>
        /// Takes a proposed head position and velocity, damps outward velocity near the edges
        /// and hard-clamps the position inside the box.
        /// </summary>
        public Vector3 Constrain(Vector3 proposed, ref Vector3 velocity)
        {
            Vector3 min = Min, max = Max;
            for (int axis = 0; axis < 3; axis++)
            {
                float p = proposed[axis];
                float v = velocity[axis];
                float distToMax = max[axis] - p;
                float distToMin = p - min[axis];

                if (softMargin > 0.01f)
                {
                    if (v > 0f && distToMax < softMargin) v *= Mathf.Clamp01(distToMax / softMargin);
                    if (v < 0f && distToMin < softMargin) v *= Mathf.Clamp01(distToMin / softMargin);
                }

                if (p > max[axis]) { p = max[axis]; if (v > 0f) v = 0f; }
                if (p < min[axis]) { p = min[axis]; if (v < 0f) v = 0f; }

                proposed[axis] = p;
                velocity[axis] = v;
            }
            return proposed;
        }

        public bool Contains(Vector3 p)
        {
            Vector3 min = Min, max = Max;
            return p.x >= min.x && p.x <= max.x && p.y >= min.y && p.y <= max.y && p.z >= min.z && p.z <= max.z;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.6f);
            Vector3 min = Min, max = Max;
            Gizmos.DrawWireCube((min + max) * 0.5f, max - min);

            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.15f);
            Gizmos.DrawCube(new Vector3(transform.position.x, surfaceHeight, transform.position.z), new Vector3(size.x, 0.02f, size.z));

            if (spawnPoint != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(spawnPoint.position, 0.4f);
                Gizmos.DrawRay(spawnPoint.position, spawnPoint.forward * 3f);
            }
        }
    }
}
