// Ground made from a height function: a grid mesh with world-scaled UVs and a collider.
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Crab.EditorTools
{
    public static class GroundMesh
    {
        public const string Dir = "Assets/Crab/Art/Generated";

        public static Mesh Build(string name, Rect area, float step, Func<float, float, float> height, float uvScale)
        {
            int nx = Mathf.CeilToInt(area.width / step) + 1, nz = Mathf.CeilToInt(area.height / step) + 1;
            var v = new Vector3[nx * nz];
            var uv = new Vector2[v.Length];
            for (int j = 0; j < nz; j++)
                for (int i = 0; i < nx; i++)
                {
                    float x = area.xMin + i * step, z = area.yMin + j * step;
                    v[j * nx + i] = new Vector3(x, height(x, z), z);
                    uv[j * nx + i] = new Vector2(x, z) * uvScale;
                }
            var t = new int[(nx - 1) * (nz - 1) * 6];
            int k = 0;
            for (int j = 0; j < nz - 1; j++)
                for (int i = 0; i < nx - 1; i++)
                {
                    int a = j * nx + i, b = a + 1, c = a + nx, d = c + 1;
                    t[k++] = a; t[k++] = c; t[k++] = b;
                    t[k++] = b; t[k++] = c; t[k++] = d;
                }
            var m = new Mesh { name = name, indexFormat = IndexFormat.UInt32, vertices = v, uv = uv, triangles = t };
            m.RecalculateNormals();
            m.RecalculateTangents();
            m.RecalculateBounds();
            Directory.CreateDirectory(Dir);
            string path = $"{Dir}/{name}.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }
    }
}
