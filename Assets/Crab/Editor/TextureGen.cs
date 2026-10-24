// Ground textures made in code: the warm rippled sand of the beach and the grey ash of Limbo with
// old cobbles showing through; each with a normal map. Saved as PNG assets (tileable).
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Crab.EditorTools
{
    public static class TextureGen
    {
        public const string Dir = "Assets/Crab/Art/Generated";

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
            float stones = Mathf.SmoothStep(0.02f, 0.09f, edge);
            float showing = Mathf.SmoothStep(0.45f, 0.65f, Fbm(u, v, 3, 41, 2));   // cobbles show through the ash in patches
            float h = Mathf.Lerp(n * 0.6f, stones * (0.6f + 0.2f * id) + n * 0.2f, showing);
            var ash = Color.Lerp(new Color(0.34f, 0.35f, 0.35f), new Color(0.55f, 0.56f, 0.55f), n);
            var cob = Color.Lerp(new Color(0.38f, 0.39f, 0.39f), new Color(0.6f, 0.61f, 0.59f), id * 0.6f + n * 0.4f) * Mathf.Lerp(0.45f, 1, stones);
            var c = Color.Lerp(ash, cob, showing);
            float moss = Mathf.SmoothStep(0.62f, 0.75f, Fbm(u, v, 4, 51, 4)) * (1 - stones * showing);
            c = Color.Lerp(c, new Color(0.16f, 0.24f, 0.18f), moss * 0.6f);
            return (c, h);
        }, 2.2f);

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
