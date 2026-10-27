using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Reef.EditorTools
{
    /// <summary>
    /// Procedural helpers for the Reef Builder: noise, placeholder meshes (coral, rock, shells,
    /// teeth, fish, shark, turtle), terrain textures and tileable, looping caustic frames.
    /// Placeholders exist so the whole experience (swimming, selecting, info panels) works end to
    /// end before any Sketchfab model is imported. They're meant to be replaced.
    /// </summary>
    public static class ReefProcedural
    {
        // ------------------------------------------------------------------ noise

        static uint Hash(uint x)
        {
            x ^= x >> 16; x *= 0x7feb352dU;
            x ^= x >> 15; x *= 0x846ca68bU;
            x ^= x >> 16;
            return x;
        }

        public static float Hash01(int x, int y, int z, int seed)
        {
            unchecked
            {
                uint h = Hash((uint)x * 73856093U ^ (uint)y * 19349663U ^ (uint)z * 83492791U ^ (uint)seed * 2654435761U);
                return (h & 0xFFFFFF) / 16777215f;
            }
        }

        static float Fade(float t) => t * t * (3f - 2f * t);

        /// <summary>GLSL-style smoothstep (Unity's Mathf.SmoothStep is an interpolation, not this).</summary>
        public static float SStep(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        public static float ValueNoise3(Vector3 p, int seed)
        {
            int x0 = Mathf.FloorToInt(p.x), y0 = Mathf.FloorToInt(p.y), z0 = Mathf.FloorToInt(p.z);
            float fx = Fade(p.x - x0), fy = Fade(p.y - y0), fz = Fade(p.z - z0);
            float c000 = Hash01(x0, y0, z0, seed), c100 = Hash01(x0 + 1, y0, z0, seed);
            float c010 = Hash01(x0, y0 + 1, z0, seed), c110 = Hash01(x0 + 1, y0 + 1, z0, seed);
            float c001 = Hash01(x0, y0, z0 + 1, seed), c101 = Hash01(x0 + 1, y0, z0 + 1, seed);
            float c011 = Hash01(x0, y0 + 1, z0 + 1, seed), c111 = Hash01(x0 + 1, y0 + 1, z0 + 1, seed);
            float x00 = Mathf.Lerp(c000, c100, fx), x10 = Mathf.Lerp(c010, c110, fx);
            float x01 = Mathf.Lerp(c001, c101, fx), x11 = Mathf.Lerp(c011, c111, fx);
            return Mathf.Lerp(Mathf.Lerp(x00, x10, fy), Mathf.Lerp(x01, x11, fy), fz);
        }

        public static float Fbm3(Vector3 p, int octaves, int seed)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += ValueNoise3(p, seed + i * 131) * amp;
                norm += amp;
                p *= 2.03f;
                amp *= 0.5f;
            }
            return sum / norm;
        }

        public static float Fbm2(float x, float y, int octaves, int seed)
        {
            float ox = (seed % 997) * 13.37f, oy = (seed % 991) * 7.77f;
            float sum = 0f, amp = 0.5f, norm = 0f, f = 1f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Mathf.PerlinNoise(x * f + ox + i * 17.1f, y * f + oy + i * 5.3f) * amp;
                norm += amp;
                f *= 2.01f;
                amp *= 0.5f;
            }
            return sum / norm;
        }

        // ------------------------------------------------------------------ mesh building

        public class MeshBuilder
        {
            public readonly List<Vector3> v = new List<Vector3>();
            public readonly List<int> t = new List<int>();

            public void Append(MeshBuilder other, Matrix4x4 m)
            {
                int o = v.Count;
                foreach (var p in other.v) v.Add(m.MultiplyPoint3x4(p));
                bool flip = m.determinant < 0f;
                for (int i = 0; i < other.t.Count; i += 3)
                {
                    if (flip) { t.Add(other.t[i] + o); t.Add(other.t[i + 2] + o); t.Add(other.t[i + 1] + o); }
                    else { t.Add(other.t[i] + o); t.Add(other.t[i + 1] + o); t.Add(other.t[i + 2] + o); }
                }
            }

            public Mesh ToMesh(string name)
            {
                var m = new Mesh { name = name };
                if (v.Count > 65000) m.indexFormat = IndexFormat.UInt32;
                m.SetVertices(v);
                m.SetTriangles(t, 0);
                // Simple spherical UVs so textured materials don't break.
                var uv = new List<Vector2>(v.Count);
                Vector3 c = Vector3.zero;
                foreach (var p in v) c += p;
                if (v.Count > 0) c /= v.Count;
                foreach (var p in v)
                {
                    Vector3 d = (p - c).normalized;
                    uv.Add(new Vector2(0.5f + Mathf.Atan2(d.z, d.x) / (2f * Mathf.PI), 0.5f + Mathf.Asin(Mathf.Clamp(d.y, -1f, 1f)) / Mathf.PI));
                }
                m.SetUVs(0, uv);
                m.RecalculateNormals();
                m.RecalculateTangents();
                m.RecalculateBounds();
                return m;
            }
        }

        /// <summary>Unit icosphere (radius 1) with shared vertices (no seams).</summary>
        public static MeshBuilder Icosphere(int subdivisions)
        {
            var mb = new MeshBuilder();
            float g = (1f + Mathf.Sqrt(5f)) / 2f;
            Vector3[] baseV =
            {
                new Vector3(-1, g, 0), new Vector3(1, g, 0), new Vector3(-1, -g, 0), new Vector3(1, -g, 0),
                new Vector3(0, -1, g), new Vector3(0, 1, g), new Vector3(0, -1, -g), new Vector3(0, 1, -g),
                new Vector3(g, 0, -1), new Vector3(g, 0, 1), new Vector3(-g, 0, -1), new Vector3(-g, 0, 1)
            };
            foreach (var p in baseV) mb.v.Add(p.normalized);
            int[] faces =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
                1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
                4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
            };
            var tris = new List<int>(faces);
            for (int s = 0; s < subdivisions; s++)
            {
                var cache = new Dictionary<long, int>();
                var next = new List<int>(tris.Count * 4);
                for (int i = 0; i < tris.Count; i += 3)
                {
                    int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                    int ab = Mid(mb, cache, a, b), bc = Mid(mb, cache, b, c), ca = Mid(mb, cache, c, a);
                    next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                tris = next;
            }
            // Icosahedron above is wound for a left-handed (Unity) outward normal? Make sure normals face out.
            mb.t.AddRange(tris);
            FixWindingOutward(mb);
            return mb;
        }

        static int Mid(MeshBuilder mb, Dictionary<long, int> cache, int a, int b)
        {
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            if (cache.TryGetValue(key, out int idx)) return idx;
            idx = mb.v.Count;
            mb.v.Add(((mb.v[a] + mb.v[b]) * 0.5f).normalized);
            cache[key] = idx;
            return idx;
        }

        static void FixWindingOutward(MeshBuilder mb)
        {
            // For a convex shape around the origin, the face normal should point away from the centre.
            for (int i = 0; i < mb.t.Count; i += 3)
            {
                Vector3 a = mb.v[mb.t[i]], b = mb.v[mb.t[i + 1]], c = mb.v[mb.t[i + 2]];
                Vector3 n = Vector3.Cross(b - a, c - a); // Unity: clockwise = front face; Cross gives normal for CW when viewed from front
                Vector3 centre = (a + b + c) / 3f;
                if (Vector3.Dot(n, centre) < 0f)
                {
                    int tmp = mb.t[i + 1]; mb.t[i + 1] = mb.t[i + 2]; mb.t[i + 2] = tmp;
                }
            }
        }

        static MeshBuilder Ellipsoid(int subdiv, Vector3 radii, Vector3 centre)
        {
            var mb = Icosphere(subdiv);
            for (int i = 0; i < mb.v.Count; i++) mb.v[i] = Vector3.Scale(mb.v[i], radii) + centre;
            return mb;
        }

        // ------------------------------------------------------------------ placeholders: reef

        public static Mesh MoundCoral(int seed)
        {
            var mb = Icosphere(3);
            for (int i = 0; i < mb.v.Count; i++)
            {
                Vector3 d = mb.v[i];
                float lobes = Fbm3(d * 1.8f + Vector3.one * seed, 3, seed) - 0.5f;
                float fine = ValueNoise3(d * 9f, seed + 7) - 0.5f;
                Vector3 p = d * (1f + lobes * 0.55f + fine * 0.06f);
                p.y *= 0.62f;
                if (p.y < -0.08f) p.y = -0.08f + (p.y + 0.08f) * 0.15f;
                p.y += 0.1f;
                mb.v[i] = p * 0.55f;
            }
            return mb.ToMesh("Placeholder_MoundCoral_" + seed);
        }

        public static Mesh BranchingCoral(int seed, bool stubby)
        {
            var rng = new System.Random(seed);
            var mb = new MeshBuilder();
            int fingers = stubby ? 34 : 26;
            for (int i = 0; i < fingers; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float r = Mathf.Sqrt((float)rng.NextDouble()) * (stubby ? 0.42f : 0.36f);
                Vector3 basePos = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                float tilt = r * (stubby ? 70f : 45f) + (float)rng.NextDouble() * 10f;
                Vector3 dir = Quaternion.AngleAxis(tilt, Vector3.Cross(Vector3.up, basePos.sqrMagnitude > 1e-6f ? basePos.normalized : Vector3.right)) * Vector3.up;
                float len = (stubby ? 0.28f : 0.55f) * (0.6f + (float)rng.NextDouble() * 0.6f) * (1f - r * 0.6f);
                float rad = stubby ? 0.07f : 0.045f;
                AddFinger(mb, basePos, dir, len, rad * (0.8f + (float)rng.NextDouble() * 0.4f));
            }
            return mb.ToMesh("Placeholder_BranchingCoral_" + seed);
        }

        static void AddFinger(MeshBuilder mb, Vector3 basePos, Vector3 dir, float length, float radius)
        {
            const int sides = 6;
            Vector3 side = Vector3.Cross(dir, Vector3.forward);
            if (side.sqrMagnitude < 1e-4f) side = Vector3.Cross(dir, Vector3.right);
            side.Normalize();
            Vector3 side2 = Vector3.Cross(dir, side).normalized;
            int o = mb.v.Count;
            for (int ring = 0; ring < 3; ring++)
            {
                float h = ring == 0 ? -0.05f : ring == 1 ? length * 0.55f : length * 0.9f;
                float rr = ring == 0 ? radius * 1.15f : ring == 1 ? radius : radius * 0.9f;
                for (int s = 0; s < sides; s++)
                {
                    float a = s / (float)sides * Mathf.PI * 2f;
                    mb.v.Add(basePos + dir * h + (side * Mathf.Cos(a) + side2 * Mathf.Sin(a)) * rr);
                }
            }
            int tip = mb.v.Count;
            mb.v.Add(basePos + dir * (length + radius * 0.6f));
            for (int ring = 0; ring < 2; ring++)
            {
                for (int s = 0; s < sides; s++)
                {
                    int a0 = o + ring * sides + s, a1 = o + ring * sides + (s + 1) % sides;
                    int b0 = a0 + sides, b1 = a1 + sides;
                    mb.t.Add(a0); mb.t.Add(a1); mb.t.Add(b0);
                    mb.t.Add(a1); mb.t.Add(b1); mb.t.Add(b0);
                }
            }
            for (int s = 0; s < sides; s++)
            {
                int a0 = o + 2 * sides + s, a1 = o + 2 * sides + (s + 1) % sides;
                mb.t.Add(a0); mb.t.Add(a1); mb.t.Add(tip);
            }
        }

        public static Mesh PlateCoral(int seed)
        {
            var mb = Icosphere(3);
            for (int i = 0; i < mb.v.Count; i++)
            {
                Vector3 d = mb.v[i];
                float ang = Mathf.Atan2(d.z, d.x);
                float r = new Vector2(d.x, d.z).magnitude;
                float edgeWave = Mathf.Sin(ang * 5f + seed) * 0.12f * r * r;
                float radius = 1f + (ValueNoise3(new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * 2.5f, seed) - 0.5f) * 0.35f;
                Vector3 p = new Vector3(d.x * radius, d.y * 0.07f + edgeWave + r * r * 0.12f, d.z * radius);
                mb.v[i] = p * 0.5f + Vector3.up * 0.08f;
            }
            return mb.ToMesh("Placeholder_PlateCoral_" + seed);
        }

        public static Mesh Boulder(int seed)
        {
            var mb = Icosphere(2);
            for (int i = 0; i < mb.v.Count; i++)
            {
                Vector3 d = mb.v[i];
                float n = Fbm3(d * 1.4f + Vector3.one * (seed * 0.37f), 4, seed);
                float ridged = 1f - Mathf.Abs(ValueNoise3(d * 3.2f, seed + 3) * 2f - 1f);
                Vector3 p = d * (0.75f + n * 0.45f + ridged * 0.08f);
                p.y *= 0.62f;
                if (p.y < -0.12f) p.y = -0.12f + (p.y + 0.12f) * 0.2f;
                p.y += 0.1f;
                mb.v[i] = p * 0.55f;
            }
            return mb.ToMesh("Placeholder_Boulder_" + seed);
        }

        public static Mesh Shell(int seed, bool cone)
        {
            var mb = Icosphere(2);
            for (int i = 0; i < mb.v.Count; i++)
            {
                Vector3 d = mb.v[i];
                Vector3 p;
                if (cone)
                {
                    float taper = Mathf.Lerp(1f, 0.15f, Mathf.Clamp01((d.z + 1f) * 0.5f));
                    p = new Vector3(d.x * 0.38f * taper, d.y * 0.34f * taper, d.z * 0.5f);
                }
                else
                {
                    p = new Vector3(d.x * 0.3f, d.y * 0.22f, d.z * 0.5f); // cowrie-like
                    if (p.y < -0.05f) p.y = -0.05f + (p.y + 0.05f) * 0.3f;
                }
                mb.v[i] = p + Vector3.up * 0.1f;
            }
            return mb.ToMesh(cone ? "Placeholder_ConeShell_" + seed : "Placeholder_Cowrie_" + seed);
        }

        /// <summary>Flat, curved triangular tooth lying in the XZ plane (tip toward +Z).</summary>
        public static Mesh SharkTooth()
        {
            var outline = new List<Vector2>
            {
                new Vector2(-0.5f, 0f), new Vector2(-0.2f, -0.06f), new Vector2(0.2f, -0.06f), new Vector2(0.5f, 0f),
                new Vector2(0.36f, 0.22f), new Vector2(0.24f, 0.36f), new Vector2(0.14f, 0.36f),
                new Vector2(0.02f, 0.6f), new Vector2(-0.18f, 0.9f), new Vector2(-0.24f, 0.62f), new Vector2(-0.36f, 0.3f)
            };
            var mb = new MeshBuilder();
            Vector2 c = new Vector2(-0.02f, 0.28f);
            int n = outline.Count;
            // top
            int top = mb.v.Count;
            mb.v.Add(new Vector3(c.x, 0.09f, c.y));
            foreach (var p in outline) mb.v.Add(new Vector3(p.x, 0.02f, p.y));
            for (int i = 0; i < n; i++) { mb.t.Add(top); mb.t.Add(top + 1 + i); mb.t.Add(top + 1 + (i + 1) % n); }
            // bottom
            int bot = mb.v.Count;
            mb.v.Add(new Vector3(c.x, -0.05f, c.y));
            foreach (var p in outline) mb.v.Add(new Vector3(p.x, 0f, p.y));
            for (int i = 0; i < n; i++) { mb.t.Add(bot); mb.t.Add(bot + 1 + (i + 1) % n); mb.t.Add(bot + 1 + i); }
            // rim
            for (int i = 0; i < n; i++)
            {
                int a0 = top + 1 + i, a1 = top + 1 + (i + 1) % n;
                int b0 = bot + 1 + i, b1 = bot + 1 + (i + 1) % n;
                mb.t.Add(a0); mb.t.Add(b0); mb.t.Add(a1);
                mb.t.Add(a1); mb.t.Add(b0); mb.t.Add(b1);
            }
            FixWindingAgainst(mb, new Vector3(c.x, 0.02f, c.y));
            var m = mb.ToMesh("Placeholder_SharkTooth");
            return m;
        }

        static void FixWindingAgainst(MeshBuilder mb, Vector3 centre)
        {
            for (int i = 0; i < mb.t.Count; i += 3)
            {
                Vector3 a = mb.v[mb.t[i]], b = mb.v[mb.t[i + 1]], cc = mb.v[mb.t[i + 2]];
                Vector3 n = Vector3.Cross(b - a, cc - a);
                if (Vector3.Dot(n, (a + b + cc) / 3f - centre) < 0f)
                {
                    int tmp = mb.t[i + 1]; mb.t[i + 1] = mb.t[i + 2]; mb.t[i + 2] = tmp;
                }
            }
        }

        public static Mesh Urchin(int seed)
        {
            var mb = Icosphere(2);
            for (int i = 0; i < mb.v.Count; i++)
            {
                Vector3 d = mb.v[i];
                float spike = Hash01(i, seed, 3, seed) > 0.55f ? 1.9f : 0.45f;
                Vector3 p = d * spike * 0.5f;
                if (p.y < 0f) p.y *= 0.4f;
                mb.v[i] = p + Vector3.up * 0.12f;
            }
            return mb.ToMesh("Placeholder_Urchin_" + seed);
        }

        // ------------------------------------------------------------------ placeholders: animals
        // All face +Z with a total length of 1 unit (scale the instance to real length).

        public static Mesh Fish(float bodyDepth, float bodyWidth)
        {
            var mb = new MeshBuilder();
            mb.Append(Ellipsoid(2, new Vector3(bodyWidth * 0.5f, bodyDepth * 0.5f, 0.4f), new Vector3(0f, 0f, 0.08f)), Matrix4x4.identity);
            mb.Append(Ellipsoid(1, new Vector3(0.012f, bodyDepth * 0.42f, 0.11f), new Vector3(0f, 0f, -0.4f)), Matrix4x4.identity);           // tail fin
            mb.Append(Ellipsoid(1, new Vector3(0.01f, bodyDepth * 0.2f, 0.22f), new Vector3(0f, bodyDepth * 0.45f, -0.02f)), Matrix4x4.identity); // dorsal
            mb.Append(Ellipsoid(1, new Vector3(0.01f, bodyDepth * 0.16f, 0.18f), new Vector3(0f, -bodyDepth * 0.43f, -0.06f)), Matrix4x4.identity); // anal fin
            return mb.ToMesh("Placeholder_Fish_" + bodyDepth.ToString("0.00"));
        }

        public static Mesh Shark()
        {
            var body = Icosphere(3);
            for (int i = 0; i < body.v.Count; i++)
            {
                Vector3 d = body.v[i];
                float z = d.z;
                float taper = z < 0f ? Mathf.Lerp(1f, 0.18f, -z) : Mathf.Lerp(1f, 0.55f, z * z);
                body.v[i] = new Vector3(d.x * 0.075f * taper, d.y * 0.085f * taper - (z > 0.6f ? (z - 0.6f) * 0.05f : 0f), d.z * 0.42f + 0.02f);
            }
            var mb = new MeshBuilder();
            mb.Append(body, Matrix4x4.identity);
            mb.Append(Ellipsoid(1, new Vector3(0.01f, 0.1f, 0.07f), Vector3.zero), Matrix4x4.TRS(new Vector3(0f, 0.11f, 0.04f), Quaternion.Euler(-25f, 0f, 0f), Vector3.one));   // dorsal
            mb.Append(Ellipsoid(1, new Vector3(0.008f, 0.13f, 0.05f), Vector3.zero), Matrix4x4.TRS(new Vector3(0f, 0.07f, -0.44f), Quaternion.Euler(-40f, 0f, 0f), Vector3.one)); // upper tail
            mb.Append(Ellipsoid(1, new Vector3(0.008f, 0.08f, 0.04f), Vector3.zero), Matrix4x4.TRS(new Vector3(0f, -0.05f, -0.42f), Quaternion.Euler(35f, 0f, 0f), Vector3.one)); // lower tail
            mb.Append(Ellipsoid(1, new Vector3(0.13f, 0.006f, 0.05f), Vector3.zero), Matrix4x4.TRS(new Vector3(0.1f, -0.05f, 0.12f), Quaternion.Euler(0f, -30f, -15f), Vector3.one)); // pectoral R
            mb.Append(Ellipsoid(1, new Vector3(0.13f, 0.006f, 0.05f), Vector3.zero), Matrix4x4.TRS(new Vector3(-0.1f, -0.05f, 0.12f), Quaternion.Euler(0f, 30f, 15f), Vector3.one)); // pectoral L
            return mb.ToMesh("Placeholder_Shark");
        }

        public static Mesh Turtle()
        {
            var shell = Icosphere(3);
            for (int i = 0; i < shell.v.Count; i++)
            {
                Vector3 d = shell.v[i];
                float y = d.y > 0f ? d.y * 0.16f : d.y * 0.05f;
                shell.v[i] = new Vector3(d.x * 0.36f, y, d.z * 0.46f);
            }
            var mb = new MeshBuilder();
            mb.Append(shell, Matrix4x4.identity);
            mb.Append(Ellipsoid(2, new Vector3(0.075f, 0.065f, 0.1f), new Vector3(0f, 0.01f, 0.52f)), Matrix4x4.identity); // head
            mb.Append(Ellipsoid(1, new Vector3(0.26f, 0.018f, 0.07f), Vector3.zero), Matrix4x4.TRS(new Vector3(0.36f, -0.02f, 0.2f), Quaternion.Euler(0f, -28f, -8f), Vector3.one));
            mb.Append(Ellipsoid(1, new Vector3(0.26f, 0.018f, 0.07f), Vector3.zero), Matrix4x4.TRS(new Vector3(-0.36f, -0.02f, 0.2f), Quaternion.Euler(0f, 28f, 8f), Vector3.one));
            mb.Append(Ellipsoid(1, new Vector3(0.12f, 0.015f, 0.06f), Vector3.zero), Matrix4x4.TRS(new Vector3(0.2f, -0.03f, -0.4f), Quaternion.Euler(0f, 35f, 0f), Vector3.one));
            mb.Append(Ellipsoid(1, new Vector3(0.12f, 0.015f, 0.06f), Vector3.zero), Matrix4x4.TRS(new Vector3(-0.2f, -0.03f, -0.4f), Quaternion.Euler(0f, -35f, 0f), Vector3.one));
            return mb.ToMesh("Placeholder_Turtle");
        }

        /// <summary>Open, tapered tube hanging down from y=0 to y=-length (for light shafts).</summary>
        public static Mesh LightShaftCone(float topRadius, float bottomRadius, float length, int sides)
        {
            var m = new Mesh { name = "LightShaftCone" };
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            for (int s = 0; s <= sides; s++)
            {
                float u = s / (float)sides;
                float a = u * Mathf.PI * 2f;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                float slope = (bottomRadius - topRadius) / length;
                Vector3 normal = new Vector3(dir.x, slope, dir.z).normalized;
                v.Add(dir * topRadius); n.Add(normal); uv.Add(new Vector2(u, 0f));
                v.Add(dir * bottomRadius + Vector3.down * length); n.Add(normal); uv.Add(new Vector2(u, 1f));
            }
            for (int s = 0; s < sides; s++)
            {
                int a0 = s * 2, a1 = s * 2 + 1, b0 = s * 2 + 2, b1 = s * 2 + 3;
                // Outward-facing (clockwise when seen from outside).
                t.Add(a0); t.Add(b0); t.Add(a1);
                t.Add(b0); t.Add(b1); t.Add(a1);
            }
            m.SetVertices(v);
            m.SetNormals(n);
            m.SetUVs(0, uv);
            m.SetTriangles(t, 0);
            m.RecalculateBounds();
            return m;
        }

        // ------------------------------------------------------------------ textures

        public enum GroundKind { Sand, ReefRock, Rubble }

        public static Texture2D GroundTexture(GroundKind kind, int size, int seed)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size, v = y / (float)size;
                    Color c;
                    switch (kind)
                    {
                        case GroundKind.Sand:
                        {
                            float n = TileFbm(u, v, 8, 4, seed);
                            float ripple = Mathf.Sin((u * 14f + TileFbm(u, v, 3, 2, seed + 1) * 1.5f) * Mathf.PI * 2f) * 0.5f + 0.5f;
                            float grain = Hash01(x, y, 0, seed);
                            c = Color.Lerp(new Color(0.72f, 0.66f, 0.52f), new Color(0.86f, 0.81f, 0.67f), n * 0.7f + ripple * 0.3f);
                            c *= 0.93f + grain * 0.1f;
                            break;
                        }
                        case GroundKind.ReefRock:
                        {
                            float n = TileFbm(u, v, 6, 5, seed);
                            float algae = SStep(0.45f, 0.7f, TileFbm(u, v, 4, 3, seed + 9));
                            float spots = Hash01(x / 3, y / 3, 1, seed) > 0.93f ? 1f : 0f;
                            c = Color.Lerp(new Color(0.22f, 0.21f, 0.2f), new Color(0.42f, 0.39f, 0.34f), n);
                            c = Color.Lerp(c, new Color(0.30f, 0.36f, 0.20f), algae * 0.6f);
                            c = Color.Lerp(c, new Color(0.62f, 0.42f, 0.52f), spots * 0.5f); // coralline algae specks
                            break;
                        }
                        default:
                        {
                            float n = TileFbm(u, v, 10, 4, seed);
                            float chunks = SStep(0.5f, 0.62f, TileFbm(u, v, 16, 2, seed + 5));
                            c = Color.Lerp(new Color(0.60f, 0.57f, 0.50f), new Color(0.80f, 0.77f, 0.70f), n);
                            c = Color.Lerp(c, new Color(0.9f, 0.88f, 0.82f), chunks * 0.6f);
                            break;
                        }
                    }
                    c.a = 1f;
                    px[y * size + x] = c;
                }
            }
            tex.SetPixels32(px);
            tex.Apply(true);
            return tex;
        }

        /// <summary>Tileable fbm on the unit square (periodic in u and v).</summary>
        static float TileFbm(float u, float v, int baseFreq, int octaves, int seed)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            int f = baseFreq;
            for (int i = 0; i < octaves; i++)
            {
                sum += TileValueNoise(u * f, v * f, f, seed + i * 31) * amp;
                norm += amp;
                f *= 2;
                amp *= 0.5f;
            }
            return sum / norm;
        }

        static float TileValueNoise(float x, float y, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = Fade(x - x0), fy = Fade(y - y0);
            int xa = ((x0 % period) + period) % period, xb = (xa + 1) % period;
            int ya = ((y0 % period) + period) % period, yb = (ya + 1) % period;
            float a = Hash01(xa, ya, 0, seed), b = Hash01(xb, ya, 0, seed);
            float c = Hash01(xa, yb, 0, seed), d = Hash01(xb, yb, 0, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        public static Texture2D SoftDot(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * a * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply(true);
            return tex;
        }

        // ------------------------------------------------------------------ caustics

        /// <summary>
        /// Tileable, seamlessly looping caustic frames made from two layers of animated Voronoi
        /// cell edges (feature points move in small closed circles, so the loop is exact).
        /// Values are ~0.35 in the dark areas and 1 on the bright filaments, ready to use as a
        /// light cookie (the cookie multiplies the sun).
        /// </summary>
        public static Texture2D[] CausticFrames(int frames, int res, int seed, System.Action<float> progress = null)
        {
            var result = new Texture2D[frames];
            var layers = new[]
            {
                new CausticLayer(7, 0.11f, seed, 1.0f),
                new CausticLayer(12, 0.09f, seed + 101, 0.55f)
            };
            var px = new Color32[res * res];
            for (int f = 0; f < frames; f++)
            {
                progress?.Invoke(f / (float)frames);
                float time = f / (float)frames; // 0..1 loop
                for (int y = 0; y < res; y++)
                {
                    for (int x = 0; x < res; x++)
                    {
                        float u = (x + 0.5f) / res, v = (y + 0.5f) / res;
                        // Periodic domain warp (integer frequencies keep it tileable and looping)
                        float wu = u + 0.018f * Mathf.Sin(2f * Mathf.PI * (3f * v + time));
                        float wv = v + 0.018f * Mathf.Sin(2f * Mathf.PI * (3f * u - time));
                        float c = 0f;
                        foreach (var L in layers) c += L.Sample(wu, wv, time) * L.weight;
                        c = Mathf.Clamp01(c);
                        float val = Mathf.Clamp01(0.35f + 0.9f * Mathf.Pow(c, 1.3f));
                        byte b = (byte)(val * 255f);
                        px[y * res + x] = new Color32(b, b, b, 255);
                    }
                }
                var tex = new Texture2D(res, res, TextureFormat.RGBA32, true, true)
                {
                    name = "Caustic_" + f.ToString("00"),
                    wrapMode = TextureWrapMode.Repeat,
                    filterMode = FilterMode.Trilinear,
                    anisoLevel = 4
                };
                tex.SetPixels32(px);
                tex.Apply(true, false);
                result[f] = tex;
            }
            return result;
        }

        class CausticLayer
        {
            readonly int cells;
            readonly float width;
            readonly int seed;
            public readonly float weight;
            readonly Vector2[] basePos;
            readonly float[] phase;
            readonly int[] freq;
            readonly Vector2[] current;
            float preparedTime = -1f;

            public CausticLayer(int cells, float width, int seed, float weight)
            {
                this.cells = cells; this.width = width; this.seed = seed; this.weight = weight;
                int n = cells * cells;
                basePos = new Vector2[n];
                phase = new float[n];
                freq = new int[n];
                current = new Vector2[n];
                for (int y = 0; y < cells; y++)
                    for (int x = 0; x < cells; x++)
                    {
                        int i = y * cells + x;
                        basePos[i] = new Vector2(0.2f + 0.6f * Hash01(x, y, 1, seed), 0.2f + 0.6f * Hash01(x, y, 2, seed));
                        phase[i] = Hash01(x, y, 3, seed) * Mathf.PI * 2f;
                        freq[i] = Hash01(x, y, 4, seed) > 0.5f ? 1 : 2;
                    }
            }

            void Prepare(float time)
            {
                if (Mathf.Approximately(time, preparedTime)) return;
                preparedTime = time;
                for (int i = 0; i < current.Length; i++)
                {
                    float a = 2f * Mathf.PI * freq[i] * time + phase[i];
                    current[i] = basePos[i] + new Vector2(0.22f * Mathf.Cos(a), 0.22f * Mathf.Sin(a));
                }
            }

            public float Sample(float u, float v, float time)
            {
                Prepare(time);
                float gx = u * cells, gy = v * cells;
                int ix = Mathf.FloorToInt(gx), iy = Mathf.FloorToInt(gy);
                float f1 = 99f, f2 = 99f;
                for (int dy = -1; dy <= 1; dy++)
                {
                    int cy = iy + dy;
                    int wy = ((cy % cells) + cells) % cells;
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int cx = ix + dx;
                        int wx = ((cx % cells) + cells) % cells;
                        Vector2 o = current[wy * cells + wx];
                        float ddx = gx - (cx + o.x), ddy = gy - (cy + o.y);
                        float d = Mathf.Sqrt(ddx * ddx + ddy * ddy);
                        if (d < f1) { f2 = f1; f1 = d; } else if (d < f2) f2 = d;
                    }
                }
                float edge = f2 - f1;
                float line = 1f - SStep(0f, width, edge);
                return line * line;
            }
        }
    }
}
