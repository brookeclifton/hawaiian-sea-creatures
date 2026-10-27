using UnityEngine;

namespace Reef
{
    /// <summary>
    /// Moves a key character (tiger shark, green sea turtle, ...) along a SwimPath at a calm,
    /// slightly varying speed, facing where it's going, with a gentle bob and a little banking.
    /// If the viewer swims into the route, the animal eases sideways to keep a respectful
    /// distance (also keeps a 3.5 m shark from filling the whole wall).
    ///
    /// With no path assigned it "rests" in place with a slow breathing bob (e.g. a turtle
    /// resting on the sand under a coral head).
    ///
    /// While the viewer is reading its info panel (SelectableSpecies.IsHeld) it holds still.
    /// </summary>
    public class PathSwimmer : MonoBehaviour
    {
        public SwimPath path;
        [Tooltip("Swimming speed in m/s. Tiger sharks cruise ~0.5-1, green sea turtles ~0.3-0.5.")]
        public float speed = 0.8f;
        [Tooltip("Speed varies up/down by this fraction over time.")]
        [Range(0f, 0.8f)] public float speedVariation = 0.2f;
        public float variationPeriod = 18f;
        [Tooltip("Where on the path to start, 0-1.")]
        [Range(0f, 1f)] public float startAt = 0f;
        [Tooltip("For open paths: swim back and forth instead of stopping at the end.")]
        public bool pingPong = true;

        [Header("Motion feel")]
        public float bobAmplitude = 0.08f;
        public float bobPeriod = 7f;
        public float maxBank = 12f;
        [Tooltip("How quickly the body turns to face the direction of travel.")]
        public float turnSharpness = 2.5f;
        [Tooltip("Keep at least this far above the seafloor.")]
        public float minHeightAboveFloor = 0.5f;

        [Header("Respect the viewer")]
        [Tooltip("If the viewer is closer than this, ease sideways away from them.")]
        public float viewerClearance = 3f;
        public float clearanceSharpness = 1.2f;

        [Header("Animation")]
        public bool syncAnimatorSpeed = true;
        [Tooltip("Animator speed when swimming at the base speed.")]
        public float animatorSpeedAtBase = 1f;

        [Header("Resting (no path)")]
        public float restBreathAmplitude = 0.015f;
        public float restBreathPeriod = 5f;

        float distance;
        int direction = 1;
        Vector3 offset;
        float bank;
        float phase;
        Animator anim;
        ReefAnimationDriver driver;
        SelectableSpecies selectable;
        ReefCCRig rig;
        Vector3 restPosition;
        Quaternion restRotation;
        Vector3 lastPos;

        void Start()
        {
            rig = ReefCCRig.Find();
            anim = GetComponentInChildren<Animator>();
            driver = GetComponent<ReefAnimationDriver>();
            selectable = GetComponent<SelectableSpecies>();
            phase = Random.value * 100f;
            restPosition = transform.position;
            restRotation = transform.rotation;
            if (path != null)
            {
                distance = startAt * path.Length;
                transform.position = path.GetPoint(distance);
                transform.rotation = Quaternion.LookRotation(path.GetDirection(distance), Vector3.up);
            }
            lastPos = transform.position;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (selectable != null && selectable.IsHeld)
            {
                Hold();
                return;
            }

            if (path == null || path.Length < 0.1f)
            {
                Rest();
                return;
            }

            float t = Time.time;
            float v = speed * (1f + speedVariation * Mathf.Sin((t + phase) * 2f * Mathf.PI / Mathf.Max(1f, variationPeriod)));
            distance += v * dt * direction;
            if (!path.closedLoop)
            {
                if (distance >= path.Length) { distance = path.Length; if (pingPong) direction = -1; }
                if (distance <= 0f) { distance = 0f; if (pingPong) direction = 1; }
            }

            Vector3 p = path.GetPoint(distance);
            Vector3 travel = path.GetDirection(distance) * direction;
            p.y += Mathf.Sin((t + phase) * 2f * Mathf.PI / Mathf.Max(0.5f, bobPeriod)) * bobAmplitude;

            // Ease sideways away from the viewer.
            Vector3 wantOffset = Vector3.zero;
            if (rig != null && viewerClearance > 0f)
            {
                Vector3 away = p - rig.ViewOrigin;
                float d = away.magnitude;
                if (d < viewerClearance && d > 1e-3f)
                {
                    Vector3 side = Vector3.ProjectOnPlane(away, travel);
                    if (side.sqrMagnitude < 1e-4f) side = Vector3.Cross(travel, Vector3.up);
                    wantOffset = side.normalized * (viewerClearance - d);
                }
            }
            offset = Vector3.Lerp(offset, wantOffset, 1f - Mathf.Exp(-clearanceSharpness * dt));
            p += offset;

            Terrain terrain = Terrain.activeTerrain;
            if (terrain != null)
            {
                float floor = terrain.SampleHeight(p) + terrain.transform.position.y + minHeightAboveFloor;
                if (p.y < floor) p.y = floor;
            }

            Vector3 moved = p - lastPos;
            Vector3 face = moved.sqrMagnitude > 1e-8f ? moved.normalized : travel;
            float yawRate = Vector3.SignedAngle(Flat(transform.forward), Flat(face), Vector3.up) / dt;
            bank = Mathf.Lerp(bank, Mathf.Clamp(-yawRate * 0.4f, -maxBank, maxBank), 1f - Mathf.Exp(-2f * dt));
            Quaternion target = Quaternion.LookRotation(face, Vector3.up) * Quaternion.Euler(0f, 0f, bank);
            transform.SetPositionAndRotation(p, Quaternion.Slerp(transform.rotation, target, 1f - Mathf.Exp(-turnSharpness * dt)));
            lastPos = p;

            if (syncAnimatorSpeed)
            {
                float a = animatorSpeedAtBase * Mathf.Clamp(v / Mathf.Max(0.05f, speed), 0.4f, 2f);
                if (driver != null) driver.SetSpeed(a);
                else if (anim != null) anim.speed = a;
            }
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        /// <summary>Stay exactly where it is, with a slow idle animation.</summary>
        void Hold()
        {
            lastPos = transform.position;
            if (syncAnimatorSpeed)
            {
                if (driver != null) driver.SetSpeed(animatorSpeedAtBase * 0.3f);
                else if (anim != null) anim.speed = animatorSpeedAtBase * 0.3f;
            }
        }

        void Rest()
        {
            float t = Time.time + phase;
            transform.SetPositionAndRotation(
                restPosition + Vector3.up * (Mathf.Sin(t * 2f * Mathf.PI / Mathf.Max(0.5f, restBreathPeriod)) * restBreathAmplitude),
                restRotation);
            if (syncAnimatorSpeed)
            {
                if (driver != null) driver.SetSpeed(animatorSpeedAtBase * 0.4f);
                else if (anim != null) anim.speed = animatorSpeedAtBase * 0.4f;
            }
        }
    }
}
