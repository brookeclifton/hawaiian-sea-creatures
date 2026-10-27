using System.Collections.Generic;
using UnityEngine;

namespace Reef.EditorTools
{
    /// <summary>
    /// The shape of the reef as a function of (x, z): a gently sloping sand bottom, parallel
    /// "spurs" of reef rock separated by sand "grooves" (the spur-and-groove pattern typical of
    /// exposed Hawaiian fringing reefs), plus isolated patch reefs out on the sand.
    /// The same function drives the terrain heights, the ground painting, and where coral,
    /// shells and fish are placed, so everything lines up. Deterministic for a given seed.
    /// </summary>
    public class ReefField
    {
        public struct PatchReef
        {
            public Vector2 centre;
            public float radius;
            public float height;
        }

        readonly ReefBuildSettings s;
        readonly int seed;
        public readonly List<PatchReef> patches = new List<PatchReef>();

        public ReefField(ReefBuildSettings settings)
        {
            s = settings;
            seed = settings.seed;
            PlacePatchReefs();
        }

        static float SStep(float a, float b, float x) => ReefProcedural.SStep(a, b, x);

        void PlacePatchReefs()
        {
            var rng = new System.Random(seed * 13 + 5);
            Vector2 spawn = new Vector2(s.spawnHeadPosition.x, s.spawnHeadPosition.z);

            // A "hero" patch reef a little ahead and to the left of the spawn point, so the
            // first thing the class sees is a coral head with life around it.
            Vector2 hero = spawn + new Vector2(-4f, 11f);
            patches.Add(new PatchReef { centre = hero, radius = s.patchReefRadius.y * 0.95f, height = s.patchReefHeight.y * 0.9f });

            float halfX = s.boundsSize.x * 0.5f + s.scatterMargin * 0.5f;
            float halfZ = s.boundsSize.z * 0.5f + s.scatterMargin * 0.5f;
            int attempts = 0;
            while (patches.Count < s.patchReefCount && attempts++ < 2000)
            {
                float x = s.boundsCenter.x + ((float)rng.NextDouble() * 2f - 1f) * halfX;
                float z = s.boundsCenter.z + ((float)rng.NextDouble() * 2f - 1f) * halfZ;
                var c = new Vector2(x, z);
                if (Vector2.Distance(c, spawn) < 9f) continue;
                if (SpurMask(x, z) > 0.05f) continue;
                bool clash = false;
                foreach (var p in patches) if (Vector2.Distance(p.centre, c) < 12f) { clash = true; break; }
                if (clash) continue;
                float r = Mathf.Lerp(s.patchReefRadius.x, s.patchReefRadius.y, (float)rng.NextDouble());
                float h = Mathf.Lerp(s.patchReefHeight.x, s.patchReefHeight.y, (float)rng.NextDouble());
                patches.Add(new PatchReef { centre = c, radius = r, height = h });
            }
        }

        /// <summary>Sand level at (x, z).</summary>
        public float Sand(float x, float z)
        {
            float half = s.terrainSize * 0.5f;
            float t = Mathf.InverseLerp(s.boundsCenter.z - half, s.boundsCenter.z + half, z);
            float y = Mathf.Lerp(s.sandHeightSouth, s.sandHeightNorth, t);
            y += (ReefProcedural.Fbm2(x * 0.02f, z * 0.02f, 3, seed) - 0.5f) * 1.2f;
            y += (ReefProcedural.Fbm2(x * 0.12f, z * 0.12f, 2, seed + 5) - 0.5f) * 0.15f;
            return y;
        }

        /// <summary>0 = sand groove, 1 = middle of a spur top.</summary>
        public float SpurMask(float x, float z) => Spur(x, z, out _);

        float Spur(float x, float z, out float heightScale)
        {
            float warp = (Mathf.PerlinNoise(z * 0.02f + seed * 0.013f, 3.1f) - 0.5f) * 12f
                       + (Mathf.PerlinNoise(z * 0.07f, seed * 0.021f + 1.7f) - 0.5f) * 3f;
            // A groove (sand channel) runs straight out from the spawn point at x = 0.
            float u = (x - s.spawnHeadPosition.x + warp * SStep(s.spurStartZ, s.spurStartZ + 20f, z)) / Mathf.Max(2f, s.spurSpacing);
            float cell = Mathf.Floor(u);
            float d = Mathf.Abs((u - cell) - 0.5f); // 0 at spur centre, 0.5 at groove centre
            float w = s.spurWidth * 0.5f;
            float across = 1f - SStep(w * 0.45f, w, d);

            float along = SStep(s.spurStartZ, s.spurStartZ + 12f, z) * (1f - SStep(s.spurEndZ - 12f, s.spurEndZ, z));
            float gaps = SStep(0.28f, 0.45f, Mathf.PerlinNoise(cell * 3.7f + seed * 0.01f, z * 0.03f + 0.5f));
            heightScale = 0.55f + 0.6f * ReefProcedural.Hash01((int)cell, 7, 0, seed);
            return Mathf.Clamp01(across * along * gaps);
        }

        /// <summary>0 = away from any patch reef, 1 = centre of one.</summary>
        public float PatchMask(float x, float z)
        {
            float best = 0f;
            var p = new Vector2(x, z);
            foreach (var pr in patches)
            {
                float d = Vector2.Distance(p, pr.centre) / pr.radius;
                if (d >= 1f) continue;
                best = Mathf.Max(best, 1f - SStep(0.45f, 1f, d));
            }
            return best;
        }

        float PatchHeight(float x, float z)
        {
            float best = 0f;
            var p = new Vector2(x, z);
            foreach (var pr in patches)
            {
                float d = Vector2.Distance(p, pr.centre) / pr.radius;
                if (d >= 1f) continue;
                float bump = 1f - SStep(0.3f, 1f, d);
                float lumpy = 0.8f + 0.4f * ReefProcedural.Fbm2(x * 0.4f, z * 0.4f, 3, seed + 21);
                best = Mathf.Max(best, pr.height * Mathf.Pow(bump, 0.8f) * lumpy);
            }
            return best;
        }

        /// <summary>Seafloor height (world Y) at (x, z).</summary>
        public float Height(float x, float z)
        {
            float m = Spur(x, z, out float hs);
            float rough = (ReefProcedural.Fbm2(x * 0.35f, z * 0.35f, 3, seed + 11) - 0.5f) * 0.9f
                        + (ReefProcedural.Fbm2(x * 1.1f, z * 1.1f, 2, seed + 12) - 0.5f) * 0.25f;
            float spur = m * (s.spurHeight * hs + rough);
            return Sand(x, z) + Mathf.Max(spur, PatchHeight(x, z));
        }

        public bool InZone(ReefZone zone, float x, float z, float slopeDegrees)
        {
            float m = SpurMask(x, z);
            float p = PatchMask(x, z);
            switch (zone)
            {
                case ReefZone.SpurTops: return m > 0.7f && slopeDegrees < 32f;
                case ReefZone.SpurSides: return (m > 0.12f && m < 0.8f) || (m > 0.3f && slopeDegrees >= 25f);
                case ReefZone.SandChannels: return m < 0.04f && p < 0.04f && slopeDegrees < 20f;
                case ReefZone.PatchReefs: return p > 0.3f;
                case ReefZone.ReefAnywhere: return m > 0.3f || p > 0.3f;
                default: return true;
            }
        }

        /// <summary>Ground paint weights (sand, reef rock, rubble) at (x, z).</summary>
        public Vector3 Splat(float x, float z, float slopeDegrees)
        {
            float r = Mathf.Max(SpurMask(x, z), PatchMask(x, z));
            float rock = Mathf.Max(SStep(0.35f, 0.7f, r), SStep(28f, 45f, slopeDegrees) * SStep(0.05f, 0.2f, r));
            float noise = ReefProcedural.Fbm2(x * 0.15f, z * 0.15f, 2, seed + 40);
            float rubble = SStep(0.03f, 0.3f, r) * (1f - rock) * (0.4f + 0.6f * noise);
            float sand = Mathf.Max(0f, 1f - rock - rubble);
            float sum = rock + rubble + sand;
            return new Vector3(sand / sum, rock / sum, rubble / sum);
        }
    }
}
