using System.Collections.Generic;
using UnityEngine;

namespace Reef
{
    public enum SchoolBehaviour
    {
        [Tooltip("Tight, coordinated school (e.g. many tangs, goatfish).")]
        Schooling,
        [Tooltip("Loose group that drifts around together without strong alignment.")]
        LooseAggregation,
        [Tooltip("Solitary fish that each hover near their own spot close to the bottom and dart about.")]
        Territorial
    }

    /// <summary>
    /// Spawns and moves a group of ambient fish around this object's position with lightweight
    /// flocking ("boids"): stay together, line up, don't collide, stay near home, avoid rock and
    /// coral, stay above the seafloor and below the surface, and scatter from the viewer if they
    /// swim too close. No physics or NavMesh needed; 20-40 fish per school is cheap.
    ///
    /// The fish prefab should face +Z at real-world scale (see the README). If it has an Animator
    /// its speed follows the fish's swimming speed; if it has no animation, give it the
    /// Reef/FishSwim material for a procedural tail wiggle.
    /// </summary>
    public class FishSchool : MonoBehaviour
    {
        [Header("Fish")]
        public GameObject fishPrefab;
        [Tooltip("Optional extra prefabs mixed into the school.")]
        public GameObject[] variants;
        [Min(1)] public int count = 20;
        public Vector2 scaleRange = new Vector2(0.85f, 1.15f);
        [Tooltip("Used for fish whose prefab doesn't already have a SelectableSpecies.")]
        public SpeciesData species;
        public bool makeSelectable = true;
        [Tooltip("0 = random every play. Any other number gives the same school every time.")]
        public int seed = 0;

        [Header("Behaviour")]
        public SchoolBehaviour behaviour = SchoolBehaviour.Schooling;
        [Tooltip("Fish try to stay inside this radius around the school's position.")]
        public float homeRadius = 6f;
        [Tooltip("Vertical half-size of the home area (schools are usually flatter than they are wide).")]
        public float homeHalfHeight = 2f;
        public float cruiseSpeed = 0.6f;
        public float minSpeed = 0.2f;
        public float maxSpeed = 2.0f;
        [Tooltip("Maximum turn rate in degrees per second.")]
        public float turnRate = 140f;

        [Header("Flocking")]
        public float neighbourRadius = 1.4f;
        public float separationDistance = 0.35f;
        public float cohesionWeight = 1f;
        public float alignmentWeight = 1.2f;
        public float separationWeight = 2.2f;
        public float homeWeight = 1f;
        public float wanderWeight = 0.4f;

        [Header("Territorial")]
        [Tooltip("Each territorial fish hovers within this radius of its own spot.")]
        public float territoryRadius = 1.2f;
        [Tooltip("Height range above the seafloor for territorial fish.")]
        public Vector2 hoverHeight = new Vector2(0.3f, 1.2f);

        [Header("Awareness")]
        [Tooltip("Fish flee if the viewer's head gets closer than this.")]
        public float viewerAvoidRadius = 2.2f;
        public float viewerAvoidWeight = 3.5f;
        [Tooltip("Rock / coral layers fish steer around.")]
        public LayerMask obstacleMask = 1;
        public float obstacleLookAhead = 1.2f;
        public float obstacleWeight = 5f;
        public float floorClearance = 0.3f;
        [Tooltip("World height of the water surface.")]
        public float surfaceHeight = 12f;

        [Header("Look")]
        [Tooltip("How much the fish tilt into turns, in degrees.")]
        public float maxBank = 10f;
        [Tooltip("Maximum nose-up/down angle.")]
        public float maxPitch = 35f;
        public bool syncAnimatorSpeed = true;

        class Fish
        {
            public Transform t;
            public Vector3 pos, vel;
            public float speed, speedPref, phase;
            public Vector3 home;
            public Vector3 avoid;
            public float dartTimer;
            public float dartActive;
            public Vector3 dartTarget;
            public float bank;
            public Animator anim;
            public ReefAnimationDriver driver;
            public SelectableSpecies selectable;
        }

        readonly List<Fish> fish = new List<Fish>();
        ReefCCRig rig;
        Terrain terrain;
        int frame;
        System.Random rng;

        public int Count => fish.Count;

        float R01() => (float)rng.NextDouble();
        float Range(float a, float b) => a + (b - a) * R01();
        Vector3 InsideUnitSphere()
        {
            Vector3 v;
            do { v = new Vector3(R01() * 2f - 1f, R01() * 2f - 1f, R01() * 2f - 1f); } while (v.sqrMagnitude > 1f);
            return v;
        }

        void Start()
        {
            rig = ReefCCRig.Find();
            terrain = Terrain.activeTerrain;
            rng = new System.Random(seed != 0 ? seed : System.Environment.TickCount ^ name.GetHashCode());
            Spawn();
        }

        void Spawn()
        {
            if (fishPrefab == null)
            {
                Debug.LogWarning("[Reef] FishSchool '" + name + "' has no fish prefab.", this);
                return;
            }

            for (int i = 0; i < count; i++)
            {
                GameObject prefab = fishPrefab;
                if (variants != null && variants.Length > 0 && R01() < 0.35f)
                {
                    var v = variants[rng.Next(variants.Length)];
                    if (v != null) prefab = v;
                }

                Vector3 home = transform.position;
                Vector3 p;
                if (behaviour == SchoolBehaviour.Territorial)
                {
                    Vector3 o = InsideUnitSphere() * homeRadius;
                    home = transform.position + new Vector3(o.x, 0f, o.z);
                    float fh = FloorHeight(home);
                    if (float.IsInfinity(fh)) fh = transform.position.y - 0.5f;
                    home.y = fh + Range(hoverHeight.x, hoverHeight.y);
                    p = home + InsideUnitSphere() * territoryRadius * 0.5f;
                }
                else
                {
                    Vector3 o = InsideUnitSphere();
                    p = transform.position + new Vector3(o.x * homeRadius, o.y * homeHalfHeight, o.z * homeRadius) * 0.6f;
                }
                p.y = Mathf.Max(p.y, FloorHeight(p) + floorClearance + 0.2f);

                Quaternion rot = Quaternion.Euler(0f, Range(0f, 360f), 0f);
                GameObject go = Instantiate(prefab, p, rot, transform);
                go.name = prefab.name + "_" + i;
                float s = Range(scaleRange.x, scaleRange.y);
                go.transform.localScale = prefab.transform.localScale * s;

                if (makeSelectable)
                {
                    var sel = go.GetComponent<SelectableSpecies>();
                    if (sel == null) sel = go.AddComponent<SelectableSpecies>();
                    if (sel.species == null) sel.species = species;
                    sel.Measure();
                }

                var f = new Fish
                {
                    t = go.transform,
                    pos = p,
                    vel = rot * Vector3.forward * cruiseSpeed,
                    speed = cruiseSpeed,
                    speedPref = Range(0.8f, 1.2f),
                    phase = Range(0f, 100f),
                    home = home,
                    dartTimer = Range(2f, 8f),
                    anim = go.GetComponentInChildren<Animator>(),
                    driver = go.GetComponent<ReefAnimationDriver>(),
                    selectable = go.GetComponent<SelectableSpecies>()
                };
                if (f.anim != null) f.anim.Update(Range(0f, 2f)); // desync animation cycles
                fish.Add(f);
            }
        }

        float FloorHeight(Vector3 p)
        {
            if (terrain == null) terrain = Terrain.activeTerrain;
            if (terrain == null) return float.NegativeInfinity;
            return terrain.SampleHeight(p) + terrain.transform.position.y;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || fish.Count == 0) return;
            frame++;

            Vector3 viewer = rig != null ? rig.ViewOrigin : new Vector3(0f, -9999f, 0f);
            float time = Time.time;
            bool schooling = behaviour != SchoolBehaviour.Territorial;
            float cohesionScale = behaviour == SchoolBehaviour.LooseAggregation ? 0.3f : 1f;
            float alignScale = behaviour == SchoolBehaviour.LooseAggregation ? 0.25f : 1f;
            float nr2 = neighbourRadius * neighbourRadius;
            float sd2 = separationDistance * separationDistance;

            for (int i = 0; i < fish.Count; i++)
            {
                Fish f = fish[i];
                if (f.t == null) continue;
                if (f.selectable != null && f.selectable.IsHeld)
                {
                    // The viewer is reading about this one: hold still, idle slowly.
                    f.vel = Vector3.zero;
                    f.speed = 0f;
                    if (syncAnimatorSpeed)
                    {
                        if (f.driver != null) f.driver.SetSpeed(0.3f);
                        else if (f.anim != null) f.anim.speed = 0.3f;
                    }
                    continue;
                }
                Vector3 p = f.pos;
                Vector3 steer = Vector3.zero;
                float targetSpeed = cruiseSpeed * f.speedPref;

                // --- Flocking
                if (schooling)
                {
                    Vector3 center = Vector3.zero, align = Vector3.zero, sep = Vector3.zero;
                    int n = 0;
                    for (int j = 0; j < fish.Count; j++)
                    {
                        if (j == i) continue;
                        Vector3 d = fish[j].pos - p;
                        float d2 = d.sqrMagnitude;
                        if (d2 > nr2) continue;
                        n++;
                        center += fish[j].pos;
                        align += fish[j].vel;
                        if (d2 < sd2) sep -= d / Mathf.Max(d2, 0.0004f) * separationDistance;
                    }
                    if (n > 0)
                    {
                        center /= n;
                        steer += (center - p) * (cohesionWeight * cohesionScale);
                        steer += (align / n - f.vel) * (alignmentWeight * alignScale);
                    }
                    steer += sep * separationWeight;

                    // Stay near home (ellipsoid)
                    Vector3 toHome = transform.position - p;
                    Vector3 scaled = new Vector3(toHome.x / homeRadius, toHome.y / Mathf.Max(0.1f, homeHalfHeight), toHome.z / homeRadius);
                    float e = scaled.magnitude;
                    if (e > 0.6f) steer += toHome.normalized * (homeWeight * (e - 0.6f) * 3f);
                }
                else
                {
                    // Territorial: hover near own spot, occasionally dart.
                    f.dartTimer -= dt;
                    if (f.dartTimer <= 0f)
                    {
                        f.dartTimer = Range(3f, 9f);
                        f.dartActive = Range(0.8f, 1.6f);
                        Vector3 o = InsideUnitSphere() * territoryRadius;
                        f.dartTarget = f.home + new Vector3(o.x, o.y * 0.4f, o.z);
                    }
                    Vector3 goal = f.home;
                    if (f.dartActive > 0f)
                    {
                        f.dartActive -= dt;
                        goal = f.dartTarget;
                    }
                    Vector3 toGoal = goal - p;
                    float dist = toGoal.magnitude;
                    steer += toGoal.normalized * Mathf.Min(dist, 2f) * homeWeight * 1.5f;
                    targetSpeed = Mathf.Lerp(minSpeed, cruiseSpeed * 1.4f, Mathf.Clamp01(dist / Mathf.Max(0.1f, territoryRadius)));
                    for (int j = 0; j < fish.Count; j++)
                    {
                        if (j == i) continue;
                        Vector3 d = fish[j].pos - p;
                        float d2 = d.sqrMagnitude;
                        if (d2 < sd2 * 4f) steer -= d / Mathf.Max(d2, 0.0004f) * separationDistance;
                    }
                }

                // --- Wander (smooth pseudo-random)
                float w = time * 0.35f + f.phase;
                steer += new Vector3(Mathf.PerlinNoise(w, 0.3f) - 0.5f, (Mathf.PerlinNoise(0.7f, w) - 0.5f) * 0.3f, Mathf.PerlinNoise(w, 5.1f) - 0.5f) * (wanderWeight * 2f);

                // --- Viewer avoidance
                Vector3 fromViewer = p - viewer;
                float vd = fromViewer.magnitude;
                if (vd < viewerAvoidRadius && vd > 1e-3f)
                {
                    float k = 1f - vd / viewerAvoidRadius;
                    steer += fromViewer / vd * (viewerAvoidWeight * k * 2f);
                    targetSpeed = Mathf.Lerp(targetSpeed, maxSpeed, k);
                }

                // --- Obstacles (raycast staggered across frames, result cached)
                Vector3 dir = f.vel.sqrMagnitude > 1e-6f ? f.vel.normalized : f.t.forward;
                if (((frame + i) & 3) == 0)
                {
                    f.avoid = Vector3.zero;
                    float lookDist = obstacleLookAhead + f.speed * 0.5f;
                    if (Physics.Raycast(p, dir, out RaycastHit hit, lookDist, obstacleMask, QueryTriggerInteraction.Ignore))
                    {
                        float k = 1f - hit.distance / lookDist;
                        Vector3 away = Vector3.ProjectOnPlane(hit.normal, dir);
                        if (away.sqrMagnitude < 1e-4f) away = Vector3.Cross(dir, Vector3.up);
                        f.avoid = (away.normalized + hit.normal) * (obstacleWeight * k);
                    }
                }
                steer += f.avoid;

                // --- Floor and surface
                float floor = FloorHeight(p) + floorClearance;
                if (p.y < floor + 0.6f) steer.y += (floor + 0.6f - p.y) * 4f;
                if (p.y > surfaceHeight - 1f) steer.y -= (p.y - (surfaceHeight - 1f)) * 4f;

                // --- Integrate with turn-rate limit
                Vector3 desired = f.vel + steer * dt * 2f;
                Vector3 desiredDir = desired.sqrMagnitude > 1e-6f ? desired.normalized : dir;
                float maxPitchSin = Mathf.Sin(maxPitch * Mathf.Deg2Rad);
                desiredDir.y = Mathf.Clamp(desiredDir.y, -maxPitchSin, maxPitchSin);
                desiredDir.Normalize();
                Vector3 newDir = Vector3.RotateTowards(dir, desiredDir, turnRate * Mathf.Deg2Rad * dt, 0f);

                targetSpeed = Mathf.Clamp(targetSpeed, minSpeed, maxSpeed);
                f.speed = Mathf.MoveTowards(f.speed, targetSpeed, dt * 1.5f);
                f.vel = newDir * f.speed;
                p += f.vel * dt;
                if (p.y < floor) p.y = floor;
                f.pos = p;

                // --- Orientation: face travel direction, bank a little into turns
                float yawRate = Vector3.SignedAngle(new Vector3(dir.x, 0f, dir.z), new Vector3(newDir.x, 0f, newDir.z), Vector3.up) / dt;
                f.bank = Mathf.Lerp(f.bank, Mathf.Clamp(-yawRate * 0.15f, -maxBank, maxBank), 1f - Mathf.Exp(-4f * dt));
                Quaternion lookRot = Quaternion.LookRotation(newDir, Vector3.up) * Quaternion.Euler(0f, 0f, f.bank);
                f.t.SetPositionAndRotation(p, Quaternion.Slerp(f.t.rotation, lookRot, 1f - Mathf.Exp(-10f * dt)));

                if (syncAnimatorSpeed)
                {
                    float a = Mathf.Clamp(f.speed / Mathf.Max(0.05f, cruiseSpeed), 0.5f, 2.5f);
                    if (f.driver != null) f.driver.SetSpeed(a);
                    else if (f.anim != null) f.anim.speed = a;
                }
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.6f);
            Matrix4x4 m = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(transform.position, Quaternion.identity, new Vector3(homeRadius, homeHalfHeight, homeRadius));
            Gizmos.DrawWireSphere(Vector3.zero, 1f);
            Gizmos.matrix = m;
        }
    }
}
