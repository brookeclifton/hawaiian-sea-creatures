using UnityEngine;
using UnityEngine.Rendering;

namespace Reef
{
    /// <summary>
    /// "Marine snow": thousands of tiny drifting specks around the viewer. On a stereo wall this
    /// is one of the strongest depth and motion cues you can add, and it is very cheap.
    ///
    /// The specks live in a box that follows the viewer's head. They stay fixed in the WORLD
    /// (so you swim through them), and when one falls outside the box it wraps around to the
    /// other side, so the field never runs out and never "pops". Specks fade near the box edges
    /// and very close to the eyes (things a few cm from the eyes are uncomfortable in stereo).
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class MarineSnow : MonoBehaviour
    {
        [Tooltip("Uses Reef/SoftParticle. Assigned by the Reef Builder.")]
        public Material material;
        [Range(100, 6000)] public int count = 2200;
        [Tooltip("Size of the box of specks around the viewer, in metres.")]
        public Vector3 volume = new Vector3(18f, 9f, 18f);
        [Tooltip("Push the box this far ahead of the viewer (you mostly look forward).")]
        public float forwardBias = 4f;
        public Vector2 sizeRange = new Vector2(0.006f, 0.022f);
        public Color color = new Color(0.85f, 0.95f, 1f, 0.55f);
        [Tooltip("Slow current carrying the specks, m/s.")]
        public Vector3 current = new Vector3(0.03f, -0.012f, 0.02f);
        [Tooltip("Random wobble speed, m/s.")]
        public float wobble = 0.02f;
        [Tooltip("Specks closer than this to the eyes fade out.")]
        public float nearFade = 0.9f;

        ParticleSystem ps;
        ParticleSystem.Particle[] buffer;
        ReefCCRig rig;
        Vector3[] seeds;

        void Start()
        {
            rig = ReefCCRig.Find();
            ps = GetComponent<ParticleSystem>();
            Configure();
            EmitAll();
        }

        void Configure()
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 1f;
            main.maxParticles = count;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 1e6f;
            main.startSpeed = 0f;
            main.gravityModifier = 0f;

            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;

            var r = GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sortMode = ParticleSystemSortMode.None;
            r.minParticleSize = 0f;
            r.maxParticleSize = 1f;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            if (material == null) material = Resources.Load<Material>("ReefMarineSnow");
            if (material != null) r.sharedMaterial = material;

            ps.Play();
        }

        Vector3 Center()
        {
            if (rig == null) return transform.position;
            return rig.ViewOrigin + rig.ViewForward * forwardBias;
        }

        void EmitAll()
        {
            buffer = new ParticleSystem.Particle[count];
            seeds = new Vector3[count];
            Vector3 c = Center();
            var ep = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                ep.position = c + Vector3.Scale(new Vector3(Random.value - 0.5f, Random.value - 0.5f, Random.value - 0.5f), volume);
                ep.startSize = Random.Range(sizeRange.x, sizeRange.y);
                ep.startLifetime = 1e6f;
                ep.startColor = color;
                ep.velocity = current;
                ps.Emit(ep, 1);
                seeds[i] = new Vector3(Random.value * 100f, Random.value * 100f, Random.value * 100f);
            }
        }

        void LateUpdate()
        {
            if (ps == null || buffer == null) return;
            int n = ps.GetParticles(buffer);
            Vector3 c = Center();
            Vector3 half = volume * 0.5f;
            Vector3 eye = rig != null ? rig.ViewOrigin : c;
            float t = Time.time * 0.25f;
            float baseA = color.a;

            for (int i = 0; i < n; i++)
            {
                Vector3 p = buffer[i].position;
                Vector3 rel = p - c;

                // Wrap around the box so the field is endless.
                if (rel.x > half.x) p.x -= volume.x; else if (rel.x < -half.x) p.x += volume.x;
                if (rel.y > half.y) p.y -= volume.y; else if (rel.y < -half.y) p.y += volume.y;
                if (rel.z > half.z) p.z -= volume.z; else if (rel.z < -half.z) p.z += volume.z;
                rel = p - c;

                // Fade near the box edges and near the eyes.
                float edge = Mathf.Max(Mathf.Abs(rel.x) / half.x, Mathf.Max(Mathf.Abs(rel.y) / half.y, Mathf.Abs(rel.z) / half.z));
                float a = Mathf.Clamp01((1f - edge) / 0.25f);
                float de = Vector3.Distance(p, eye);
                a *= Mathf.Clamp01((de - nearFade * 0.5f) / Mathf.Max(0.01f, nearFade));

                Color32 col = color;
                col.a = (byte)(baseA * a * 255f);
                buffer[i].startColor = col;
                buffer[i].position = p;

                if (i < seeds.Length && wobble > 0f)
                {
                    Vector3 s = seeds[i];
                    buffer[i].velocity = current + new Vector3(
                        Mathf.PerlinNoise(s.x, t) - 0.5f,
                        (Mathf.PerlinNoise(s.y, t) - 0.5f) * 0.5f,
                        Mathf.PerlinNoise(s.z, t) - 0.5f) * (wobble * 2f);
                }
            }
            ps.SetParticles(buffer, n);
        }
    }
}
