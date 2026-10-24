// Ground textures made in code: the warm rippled sand of the beach, the grey ash of Limbo with
// old cobbles showing through, the orange dunes of Wrath; each with a normal map. Saved as PNG assets (tileable).
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Crab.EditorTools
{
    public static class TextureGen
    {
        public const string Dir = "Assets/Crab/Art/Generated";

        /// <summary>GLSL smoothstep: 0 below a, 1 above b, smooth between (Mathf.SmoothStep is an
        /// interpolation between its first two arguments, not this).</summary>
        static float Smooth(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3 - 2 * t);
        }

        /// <summary>Tileable value noise: octaves of smooth noise on a torus.</summary>
        static float Fbm(float u, float v, int octaves, int seed, float baseFreq)
        {
            float sum = 0, amp = 0.5f, norm = 0;
            for (int o = 0; o < octaves; o++)
            {
                int f = (int)(baseFreq * (1 << o));
                sum += amp * Tile(u * f, v * f, f, seed + o * 17);
                norm += amp;
                amp *= 0.5f;
            }
            return sum / norm;
        }

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + seed * 2147483;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0xffff) / 65535f;
            }
        }

        static float Tile(float x, float y, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3 - 2 * fx);
            fy = fy * fy * (3 - 2 * fy);
            int Wx(int a) => ((a % period) + period) % period;
            float a00 = Hash(Wx(x0), Wx(y0), seed), a10 = Hash(Wx(x0 + 1), Wx(y0), seed), a01 = Hash(Wx(x0), Wx(y0 + 1), seed), a11 = Hash(Wx(x0 + 1), Wx(y0 + 1), seed);
            return Mathf.Lerp(Mathf.Lerp(a00, a10, fx), Mathf.Lerp(a01, a11, fx), fy);
        }

        /// <summary>Distance to the nearest cell edge of a tileable voronoi (for cobbles).</summary>
        static float Cells(float u, float v, int n, int seed, out float id)
        {
            float x = u * n, y = v * n;
            int cx = Mathf.FloorToInt(x), cy = Mathf.FloorToInt(y);
            float d1 = 9, d2 = 9;
            id = 0;
            for (int j = -1; j <= 1; j++)
                for (int i = -1; i <= 1; i++)
                {
                    int gx = cx + i, gy = cy + j;
                    int wx = ((gx % n) + n) % n, wy = ((gy % n) + n) % n;
                    float px = gx + 0.15f + 0.7f * Hash(wx, wy, seed), py = gy + 0.15f + 0.7f * Hash(wx, wy, seed + 1);
                    float d = Mathf.Sqrt((px - x) * (px - x) + (py - y) * (py - y));
                    if (d < d1) { d2 = d1; d1 = d; id = Hash(wx, wy, seed + 2); }
                    else if (d < d2) d2 = d;
                }
            return d2 - d1;
        }

        public static (Texture2D albedo, Texture2D normal) Sand() => Make("sand", 1024, (u, v) =>
        {
            float n = Fbm(u, v, 5, 3, 8);
            float ripple = Mathf.Sin((v * 18 + Fbm(u, v, 3, 9, 4) * 3) * Mathf.PI * 2) * 0.5f + 0.5f;
            float grain = Hash((int)(u * 1024), (int)(v * 1024), 5);
            float h = n * 0.5f + ripple * 0.35f + grain * 0.15f;
            var c = Color.Lerp(new Color(0.78f, 0.66f, 0.47f), new Color(0.93f, 0.84f, 0.66f), n * 0.7f + ripple * 0.3f);
            c = Color.Lerp(c, new Color(0.55f, 0.45f, 0.33f), grain > 0.93f ? 0.6f : 0);
            if (grain < 0.015f) c = new Color(0.97f, 0.95f, 0.9f);   // shell bits
            return (c, h);
        }, 1.6f);

        public static (Texture2D albedo, Texture2D normal) Ash() => Make("ash", 1024, (u, v) =>
        {
            float n = Fbm(u, v, 6, 21, 6);
            float edge = Cells(u, v, 7, 31, out float id);
            float stones = Smooth(0.02f, 0.09f, edge);
            float showing = Smooth(0.45f, 0.65f, Fbm(u, v, 3, 41, 2));   // cobbles show through the ash in patches
            float h = Mathf.Lerp(n * 0.6f, stones * (0.6f + 0.2f * id) + n * 0.2f, showing);
            var ash = Color.Lerp(new Color(0.34f, 0.35f, 0.35f), new Color(0.55f, 0.56f, 0.55f), n);
            var cob = Color.Lerp(new Color(0.38f, 0.39f, 0.39f), new Color(0.6f, 0.61f, 0.59f), id * 0.6f + n * 0.4f) * Mathf.Lerp(0.45f, 1, stones);
            var c = Color.Lerp(ash, cob, showing);
            float moss = Smooth(0.62f, 0.75f, Fbm(u, v, 4, 51, 4)) * (1 - stones * showing);
            c = Color.Lerp(c, new Color(0.16f, 0.24f, 0.18f), moss * 0.6f);
            return (c, h);
        }, 2.2f);

        public static (Texture2D albedo, Texture2D normal) Dunes() => Make("dunes", 1024, (u, v) =>
        {
            // wind ripples on orange sand; in patches the sand gives way to cracked, scorched clay
            float n = Fbm(u, v, 5, 61, 6);
            float ripple = Mathf.Sin((u * 14 + v * 6 + Fbm(u, v, 3, 67, 3) * 2.5f) * Mathf.PI * 2) * 0.5f + 0.5f;
            float grain = Hash((int)(u * 1024), (int)(v * 1024), 71);
            float edge = Cells(u, v, 9, 73, out float id);
            float crack = 1 - Smooth(0.0f, 0.05f, edge);
            float clay = Smooth(0.5f, 0.66f, Fbm(u, v, 3, 79, 2));
            float h = Mathf.Lerp(n * 0.5f + ripple * 0.35f + grain * 0.15f, 0.7f - crack * 0.6f + n * 0.15f, clay);
            var sand = Color.Lerp(new Color(0.62f, 0.33f, 0.17f), new Color(0.86f, 0.55f, 0.3f), n * 0.6f + ripple * 0.4f);
            sand = Color.Lerp(sand, new Color(0.4f, 0.2f, 0.12f), grain > 0.94f ? 0.6f : 0);
            var dry = Color.Lerp(new Color(0.42f, 0.22f, 0.14f), new Color(0.6f, 0.34f, 0.2f), id * 0.5f + n * 0.5f);
            dry = Color.Lerp(dry, new Color(0.12f, 0.06f, 0.04f), crack);
            var c = Color.Lerp(sand, dry, clay);
            float soot = Smooth(0.64f, 0.8f, Fbm(u, v, 4, 83, 4));
            c = Color.Lerp(c, new Color(0.14f, 0.1f, 0.09f), soot * 0.55f);
            return (c, h);
        }, 1.8f);

        public static (Texture2D albedo, Texture2D normal) Gloom() => Make("gloom", 1024, (u, v) =>
        {
            // the dark plain around the golden city: soot-black dust over slate, glints of gold dust
            float n = Fbm(u, v, 6, 91, 5);
            float edge = Cells(u, v, 6, 93, out float id);
            float slate = Smooth(0.5f, 0.7f, Fbm(u, v, 3, 97, 2));
            float h = Mathf.Lerp(n * 0.6f, Smooth(0.0f, 0.08f, edge) * 0.7f + n * 0.2f, slate);
            var dust = Color.Lerp(new Color(0.12f, 0.1f, 0.11f), new Color(0.24f, 0.21f, 0.2f), n);
            var rock = Color.Lerp(new Color(0.16f, 0.15f, 0.17f), new Color(0.3f, 0.28f, 0.3f), id * 0.6f + n * 0.4f) * Mathf.Lerp(0.5f, 1, Smooth(0.0f, 0.08f, edge));
            var c = Color.Lerp(dust, rock, slate);
            float glint = Hash((int)(u * 1024), (int)(v * 1024), 99);
            if (glint > 0.992f) c = new Color(0.85f, 0.65f, 0.25f);
            return (c, h);
        }, 1.6f);

        public static (Texture2D albedo, Texture2D normal) Paving() => Make("paving", 1024, (u, v) =>
        {
            // the city's streets: big square flags of golden stone, worn in the middle, dark joints
            const int N = 6;
            float x = u * N, y = v * N;
            float fx = x - Mathf.Floor(x), fy = y - Mathf.Floor(y);
            int ix = ((int)Mathf.Floor(x)) % N, iy = ((int)Mathf.Floor(y)) % N;
            float joint = Mathf.Min(Mathf.Min(fx, 1 - fx), Mathf.Min(fy, 1 - fy));
            float j = Smooth(0.0f, 0.04f, joint);
            float n = Fbm(u, v, 5, 101, 8);
            float tone = Hash(ix, iy, 103);
            var stone = Color.Lerp(new Color(0.62f, 0.5f, 0.3f), new Color(0.86f, 0.72f, 0.46f), tone * 0.6f + n * 0.4f);
            var c = Color.Lerp(new Color(0.18f, 0.13f, 0.08f), stone, j);
            float wear = Smooth(0.55f, 0.75f, Fbm(u, v, 3, 107, 3));
            c = Color.Lerp(c, new Color(0.95f, 0.8f, 0.45f), wear * j * 0.35f);
            return (c, j * 0.8f + n * 0.2f);
        }, 2.0f);

        static (Texture2D, Texture2D) Make(string name, int size, System.Func<float, float, (Color, float)> f, float bump)
        {
            Directory.CreateDirectory(Dir);
            var col = new Color[size * size];
            var hgt = new float[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    var (c, h) = f(x / (float)size, y / (float)size);
                    col[y * size + x] = c;
                    hgt[y * size + x] = h;
                }
            var nrm = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float hl = hgt[y * size + (x + size - 1) % size], hr = hgt[y * size + (x + 1) % size];
                    float hd = hgt[((y + size - 1) % size) * size + x], hu = hgt[((y + 1) % size) * size + x];
                    var n = new Vector3((hl - hr) * bump * size / 64f, (hd - hu) * bump * size / 64f, 1).normalized;
                    nrm[y * size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1);
                }
            return (Save($"{name}_c", size, col, false), Save($"{name}_n", size, nrm, true));
        }

        static Texture2D Save(string name, int size, Color[] px, bool normal)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false, normal);
            t.SetPixels(px);
            t.Apply();
            string path = $"{Dir}/{name}.png";
            File.WriteAllBytes(path, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            imp.sRGBTexture = !normal;
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.anisoLevel = 8;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
