using UnityEngine;

namespace Reef
{
    /// <summary>
    /// Swim navigation for the CyberCANOE rig. Replaces DPadFlyer (and switches it off for you).
    ///
    /// Put this on CC_HUB. It moves and turns CC_HUB, which carries the head, both eye cameras
    /// and the virtual wall with it.
    ///
    /// Designed for a shared stereo wall where one person drives and a whole class watches:
    ///  - water-like inertia (you glide to a stop instead of halting instantly)
    ///  - capped, smoothed turn rates; no camera roll, ever
    ///  - pitch is limited and slowly auto-levels to the horizon when the stick is released
    ///  - rotation happens around the viewer's head, not around the floor of the hub
    ///  - collides with rocks, coral and the seafloor; slows down softly at the reef bounds
    ///
    /// Controls (see ReefInput): left stick swim, right stick look, RT/LT rise/sink,
    /// click left stick to boost, hold View to glide back to the start.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(ReefCCRig))]
    public class SwimController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Swimmable area + spawn point. Found automatically if empty.")]
        public ReefBounds bounds;

        [Header("Swimming")]
        [Tooltip("Cruising speed in m/s. A relaxed snorkeller is ~0.5-1 m/s; the demo feels good at 1.5-2.")]
        public float swimSpeed = 1.8f;
        [Tooltip("Speed multiplier while the left stick is clicked in.")]
        public float boostMultiplier = 1.8f;
        [Tooltip("Rise/sink speed in m/s at full trigger.")]
        public float verticalSpeed = 1.2f;
        [Tooltip("How quickly you reach full speed when pushing the stick (higher = snappier).")]
        public float acceleration = 1.6f;
        [Tooltip("How quickly you glide to a stop when you let go (higher = stops sooner).")]
        public float waterDrag = 1.1f;
        [Tooltip("If on, pushing forward swims toward where you are looking (including up/down). If off, forward is always level.")]
        public bool swimWhereYouLook = true;
        [Tooltip("Stop all movement input (e.g. during a guided moment). Looking still works.")]
        public bool movementLocked = false;
        [Tooltip("Stop turning/looking input (e.g. while a menu is on screen).")]
        public bool lookLocked = false;

        [Header("Looking")]
        [Tooltip("Maximum turn speed left/right in degrees per second.")]
        public float yawSpeed = 50f;
        [Tooltip("Maximum look up/down speed in degrees per second.")]
        public float pitchSpeed = 30f;
        [Tooltip("How far up/down you can look. Keep this modest on a shared wall.")]
        public float maxPitch = 30f;
        public bool invertPitch = false;
        [Tooltip("Smoothing of turning (higher = more responsive, lower = floatier).")]
        public float lookSmoothing = 6f;
        [Tooltip("Slowly return to a level horizon after the right stick has been idle.")]
        public bool autoLevel = true;
        public float autoLevelDelay = 2f;
        public float autoLevelSpeed = 8f;

        [Header("Collision")]
        public bool collide = true;
        [Tooltip("Radius of the viewer's 'body' sphere around the head.")]
        public float headRadius = 0.5f;
        [Tooltip("Layers the viewer bumps into (seafloor, rocks, coral).")]
        public LayerMask collisionMask = 1; // Default layer

        [Header("Return to start (hold View / R)")]
        public float resetHoldSeconds = 1f;
        public float resetGlideSeconds = 2.5f;

        [Header("Start")]
        [Tooltip("Place the head at the ReefBounds spawn point when Play starts.")]
        public bool moveToSpawnOnStart = true;

        [Header("CyberCANOE compatibility")]
        [Tooltip("Switch off DPadFlyer on this object so the two controllers don't fight.")]
        public bool disableDPadFlyer = true;
        [Tooltip("Switch off CCaux_Waypointer. Its keys (S, R, X, Space) and buttons (X, Y, B) clash with swimming and selection.")]
        public bool disableWaypointer = true;
        [Tooltip("Switch off SpaceMouseFlyAround. Its 3Dconnexion plugin is Intel-only, so on Apple Silicon Macs it throws an error every frame.")]
        public bool disableSpaceMouse = true;
        [Tooltip("Also read W A S D / arrows / Space / C etc. Handy when developing without a controller.")]
        public bool keyboardFallback = true;

        public Vector3 Velocity => velocity;
        public bool IsReturningToStart => resetting;
        public float Yaw => yaw;
        public float Pitch => pitch;

        ReefCCRig rig;
        Vector3 velocity;
        float yaw, pitch;
        Vector2 lookVelocity;
        float lookIdleTime;
        bool rotationInitialised;

        float resetHeldTime;
        bool resetNeedsRelease;
        bool resetting;
        float resetT;
        Vector3 resetFromHead;
        float resetFromYaw, resetFromPitch;

        Vector3 startHead;
        float startYaw;

        SphereCollider probe;
        readonly Collider[] overlapBuffer = new Collider[16];

        void Awake()
        {
            rig = GetComponent<ReefCCRig>();

            if (disableDPadFlyer) DisableBehaviour("DPadFlyer");
            if (disableWaypointer) DisableBehaviour("CCaux_Waypointer");
            if (disableSpaceMouse) DisableBehaviour("SpaceMouseFlyAround");

            if (Quaternion.Angle(transform.rotation, Quaternion.identity) > 0.01f)
            {
                Debug.LogWarning("[Reef] CC_HUB should start with rotation (0,0,0). CC_CAMERA_SETUP offsets the eyes along world X at Start, " +
                                 "so a rotated hub gives wrong stereo. Leave the hub unrotated and set the starting direction on the ReefBounds spawn point instead.", this);
            }
        }

        void DisableBehaviour(string typeName)
        {
            foreach (var b in GetComponentsInChildren<Behaviour>(true))
            {
                if (b == null || !b.enabled || b.GetType().Name != typeName) continue;
                b.enabled = false;
                Debug.Log("[Reef] SwimController disabled " + typeName + " on " + b.name + ".", this);
            }
        }

        void Start()
        {
            if (bounds == null) bounds = FindAnyObjectByType<ReefBounds>();
            if (rig.head == null) rig.AutoFind();

            // A small trigger sphere used only for de-penetration checks. It sits on the
            // Ignore Raycast layer so selection and fish raycasts never hit it.
            if (rig.head != null)
            {
                var go = new GameObject("ReefHeadProbe");
                go.layer = 2; // Ignore Raycast
                go.transform.SetParent(rig.head, false);
                probe = go.AddComponent<SphereCollider>();
                probe.isTrigger = true;
                probe.radius = headRadius;
            }

            yaw = 0f;
            pitch = 0f;
            if (moveToSpawnOnStart && bounds != null && bounds.spawnPoint != null)
            {
                // Position only. Rotation is applied on the first Update, after CC_CAMERA_SETUP.Start
                // has placed the eye cameras (it assumes an unrotated hub).
                transform.position += bounds.spawnPoint.position - rig.ViewOrigin;
                yaw = bounds.spawnPoint.eulerAngles.y;
            }
            startHead = rig.ViewOrigin;
            startYaw = yaw;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || rig == null) return;
            ReefInput.KeyboardEnabled = keyboardFallback;

            if (!rotationInitialised)
            {
                ApplyRotation(yaw, pitch);
                rotationInitialised = true;
                // CC_CAMERA_SETUP may have moved CC_HEAD (from CCUnityConfig.xml) during Start; re-centre on the spawn.
                if (moveToSpawnOnStart && bounds != null && bounds.spawnPoint != null)
                    transform.position += bounds.spawnPoint.position - rig.ViewOrigin;
                startHead = rig.ViewOrigin;
            }

            HandleResetInput(dt);
            if (resetting)
            {
                UpdateReset(dt);
                return;
            }

            UpdateLook(dt);
            UpdateMove(dt);
        }

        // ------------------------------------------------------------------ look

        static float ResponseCurve(float x)
        {
            // Half linear, half squared: fine control near the centre, full speed at the edge.
            return 0.5f * x + 0.5f * x * Mathf.Abs(x);
        }

        void UpdateLook(float dt)
        {
            Vector2 look = lookLocked ? Vector2.zero : ReefInput.Look;
            look.x = ResponseCurve(look.x);
            look.y = ResponseCurve(look.y);
            if (invertPitch) look.y = -look.y;

            Vector2 target = new Vector2(look.x * yawSpeed, look.y * pitchSpeed);
            lookVelocity = Vector2.Lerp(lookVelocity, target, 1f - Mathf.Exp(-lookSmoothing * dt));

            yaw += lookVelocity.x * dt;
            pitch -= lookVelocity.y * dt; // Unity: positive X rotation looks down

            if (look.sqrMagnitude > 0.0004f) lookIdleTime = 0f;
            else lookIdleTime += dt;

            if (autoLevel && lookIdleTime > autoLevelDelay)
                pitch = Mathf.MoveTowards(pitch, 0f, autoLevelSpeed * dt);

            pitch = Mathf.Clamp(pitch, -maxPitch, maxPitch);
            if (yaw > 360f || yaw < -360f) yaw = Mathf.Repeat(yaw, 360f);

            ApplyRotation(yaw, pitch);
        }

        /// <summary>Rotate CC_HUB to (pitch, yaw, 0) while keeping the head where it is.</summary>
        void ApplyRotation(float newYaw, float newPitch)
        {
            Vector3 headWorld = rig.ViewOrigin;
            Vector3 headLocal = rig.HeadLocal;
            transform.rotation = Quaternion.Euler(newPitch, newYaw, 0f);
            transform.position = headWorld - transform.TransformVector(headLocal);
        }

        // ------------------------------------------------------------------ move

        void UpdateMove(float dt)
        {
            Vector2 move = movementLocked ? Vector2.zero : ReefInput.Move;
            float vertical = movementLocked ? 0f : ReefInput.Vertical;

            Quaternion yawOnly = Quaternion.Euler(0f, yaw, 0f);
            Vector3 forward = swimWhereYouLook ? transform.rotation * Vector3.forward : yawOnly * Vector3.forward;
            Vector3 right = yawOnly * Vector3.right;

            float speed = swimSpeed * (ReefInput.Boost ? boostMultiplier : 1f);
            Vector3 desired = (forward * move.y + right * move.x) * speed + Vector3.up * (vertical * verticalSpeed);

            bool hasInput = move.sqrMagnitude > 0.0025f || Mathf.Abs(vertical) > 0.05f;
            float k = hasInput ? acceleration : waterDrag;
            velocity = Vector3.Lerp(velocity, desired, 1f - Mathf.Exp(-k * dt));
            if (!hasInput && velocity.sqrMagnitude < 0.0004f) velocity = Vector3.zero;

            Vector3 head = rig.ViewOrigin;
            Vector3 delta = velocity * dt;
            Vector3 newHead = collide ? CollideAndSlide(head, delta) : head + delta;
            if (bounds != null) newHead = bounds.Constrain(newHead, ref velocity);
            if (collide) newHead = KeepAboveTerrain(newHead);

            transform.position += newHead - head;
        }

        Vector3 CollideAndSlide(Vector3 start, Vector3 delta)
        {
            const float skin = 0.03f;
            Vector3 pos = start;
            for (int i = 0; i < 3; i++)
            {
                float dist = delta.magnitude;
                if (dist < 1e-5f) break;
                Vector3 dir = delta / dist;
                if (Physics.SphereCast(pos, headRadius, dir, out RaycastHit hit, dist + skin, collisionMask, QueryTriggerInteraction.Ignore))
                {
                    float travel = Mathf.Max(0f, hit.distance - skin);
                    pos += dir * travel;
                    delta = Vector3.ProjectOnPlane(delta - dir * travel, hit.normal);
                    float into = Vector3.Dot(velocity, hit.normal);
                    if (into < 0f) velocity -= hit.normal * into;
                }
                else
                {
                    pos += delta;
                    break;
                }
            }
            return Depenetrate(pos);
        }

        Vector3 Depenetrate(Vector3 pos)
        {
            if (probe == null) return pos;
            int n = Physics.OverlapSphereNonAlloc(pos, headRadius, overlapBuffer, collisionMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                Collider other = overlapBuffer[i];
                if (other == null || other == probe || other is TerrainCollider) continue;
                if (other.transform.IsChildOf(transform)) continue;
                if (Physics.ComputePenetration(probe, pos, Quaternion.identity,
                                               other, other.transform.position, other.transform.rotation,
                                               out Vector3 dir, out float distance))
                {
                    pos += dir * (distance + 0.005f);
                    float into = Vector3.Dot(velocity, dir);
                    if (into < 0f) velocity -= dir * into;
                }
            }
            return pos;
        }

        Vector3 KeepAboveTerrain(Vector3 pos)
        {
            Terrain t = Terrain.activeTerrain;
            if (t == null) return pos;
            float floor = t.SampleHeight(pos) + t.transform.position.y + headRadius;
            if (pos.y < floor)
            {
                pos.y = floor;
                if (velocity.y < 0f) velocity.y = 0f;
            }
            return pos;
        }

        // ------------------------------------------------------------------ return to start

        Vector3 SpawnHead => (bounds != null && bounds.spawnPoint != null) ? bounds.spawnPoint.position : startHead;
        float SpawnYaw => (bounds != null && bounds.spawnPoint != null) ? bounds.spawnPoint.eulerAngles.y : startYaw;

        void HandleResetInput(float dt)
        {
            if (ReefInput.ResetHeld)
            {
                if (resetNeedsRelease || resetting) return;
                resetHeldTime += dt;
                if (resetHeldTime >= resetHoldSeconds) BeginReturnToStart();
            }
            else
            {
                resetHeldTime = 0f;
                resetNeedsRelease = false;
            }
        }

        /// <summary>Smoothly glide back to the spawn point (also callable from other scripts).</summary>
        public void BeginReturnToStart()
        {
            resetting = true;
            resetNeedsRelease = true;
            resetT = 0f;
            resetFromHead = rig.ViewOrigin;
            resetFromYaw = yaw;
            resetFromPitch = pitch;
            velocity = Vector3.zero;
            lookVelocity = Vector2.zero;
        }

        void UpdateReset(float dt)
        {
            resetT += dt / Mathf.Max(0.1f, resetGlideSeconds);
            float s = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(resetT));
            yaw = Mathf.LerpAngle(resetFromYaw, SpawnYaw, s);
            pitch = Mathf.Lerp(resetFromPitch, 0f, s);
            ApplyRotation(yaw, pitch);
            Vector3 head = Vector3.Lerp(resetFromHead, SpawnHead, s);
            transform.position += head - rig.ViewOrigin;
            if (resetT >= 1f) resetting = false;
        }

        /// <summary>Instantly move the viewer's head to a position and heading.</summary>
        public void Teleport(Vector3 headPosition, float newYaw)
        {
            yaw = newYaw;
            pitch = 0f;
            velocity = Vector3.zero;
            ApplyRotation(yaw, pitch);
            transform.position += headPosition - rig.ViewOrigin;
        }
    }
}
