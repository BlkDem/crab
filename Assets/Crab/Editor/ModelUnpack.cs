// The GLB models with their pictures taken out: glTFast keeps a GLB's embedded textures as raw
// RGBA (about 22 MB for a 2048 map with its mips), so a build came to 1.8 GB. Each GLB is unpacked
// into a .gltf + .bin and image files beside it; the images are then ordinary texture assets that
// Unity compresses (colour sRGB, the rest linear; normal maps in the finer BC7). The scenes use the
// unpacked models. Derived files, not kept in git: they are remade when missing or older than the GLB.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Crab.EditorTools
{
    public static class ModelUnpack
    {
        public const string Src = "Assets/Crab/Models", Dst = "Assets/Crab/Art/Unpacked";

        /// <summary>"World/rift" → the unpacked model's path.</summary>
        public static string PathOf(string rel) => $"{Dst}/{rel}/{Path.GetFileName(rel)}.gltf";

        [MenuItem("Crab/Unpack Models")]
        public static void All()
        {
            var todo = new List<(string glb, string rel)>();
            foreach (var glb in Directory.GetFiles(Src, "*.glb", SearchOption.AllDirectories))
            {
                var rel = glb.Replace('\\', '/').Substring(Src.Length + 1);
                rel = rel.Substring(0, rel.Length - 4);
                var gltf = PathOf(rel);
                if (!File.Exists(gltf) || File.GetLastWriteTimeUtc(gltf) < File.GetLastWriteTimeUtc(glb)) todo.Add((glb, rel));
            }
            if (todo.Count == 0) return;
            // the images first, imported as textures, then the models that point at them
            var models = new List<(string path, string json, byte[] bin)>();
            foreach (var (glb, rel) in todo) models.Add(Unpack(glb, rel));
            AssetDatabase.Refresh();
            foreach (var (path, json, bin) in models)
            {
                File.WriteAllBytes(Path.ChangeExtension(path, ".bin"), bin);
                File.WriteAllText(path, json);
            }
            AssetDatabase.Refresh();
            Debug.Log($"unpacked {todo.Count} models");
        }

        static (string, string, byte[]) Unpack(string glb, string rel)
        {
            var data = File.ReadAllBytes(glb);
            string json = null;
            byte[] bin = null;
            for (int at = 12; at + 8 <= data.Length;)
            {
                int length = System.BitConverter.ToInt32(data, at);
                uint type = System.BitConverter.ToUInt32(data, at + 4);
                if (type == 0x4E4F534A) json = Encoding.UTF8.GetString(data, at + 8, length);
                else if (type == 0x004E4942) { bin = new byte[length]; System.Array.Copy(data, at + 8, bin, 0, length); }
                at += 8 + length;
            }
            var root = JObject.Parse(json);
            var dir = $"{Dst}/{rel}";
            Directory.CreateDirectory(dir);
            var name = Path.GetFileName(rel);
            // what each picture is for: colour, a normal map, or data (roughness, metal, occlusion)
            var role = new Dictionary<int, char>();
            int Source(JToken tex) => tex == null ? -1 : (int)root["textures"][(int)tex["index"]]["source"];
            void Mark(JToken tex, char r) { int s = Source(tex); if (s >= 0 && (!role.ContainsKey(s) || r == 'n')) role[s] = r; }
            foreach (var m in root["materials"] ?? new JArray())
            {
                Mark(m["pbrMetallicRoughness"]?["baseColorTexture"], 'c');
                Mark(m["emissiveTexture"], 'c');
                Mark(m["normalTexture"], 'n');
                Mark(m["pbrMetallicRoughness"]?["metallicRoughnessTexture"], 'd');
                Mark(m["occlusionTexture"], 'd');
            }
            var images = (JArray)root["images"] ?? new JArray();
            var views = (JArray)root["bufferViews"];
            for (int i = 0; i < images.Count; i++)
            {
                var img = (JObject)images[i];
                if (img["bufferView"] == null) continue;
                var view = views[(int)img["bufferView"]];
                int offset = (int?)view["byteOffset"] ?? 0, length = (int)view["byteLength"];
                string ext = (string)img["mimeType"] == "image/png" ? ".png" : ".jpg";
                string file = $"{name}_{i}_{(role.TryGetValue(i, out var r) ? r : 'c')}{ext}";
                using (var f = File.Create($"{dir}/{file}")) f.Write(bin, offset, length);
                img.Remove("bufferView");
                img.Remove("mimeType");
                img["uri"] = file;
            }
            root["buffers"][0]["uri"] = name + ".bin";
            return (PathOf(rel), root.ToString(Newtonsoft.Json.Formatting.None), bin);
        }
    }

    /// <summary>How the unpacked pictures are imported: compressed, with mips, by their role.</summary>
    sealed class UnpackedTextures : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ModelUnpack.Dst)) return;
            var ti = (TextureImporter)assetImporter;
            var stem = Path.GetFileNameWithoutExtension(assetPath);
            char role = stem[stem.Length - 1];
            // glTFast's shaders read a normal map as plain RGB (as glTF stores it), not Unity's packed kind
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = role == 'c';
            ti.mipmapEnabled = true;
            ti.maxTextureSize = 2048;
            ti.textureCompression = role == 'n' ? TextureImporterCompression.CompressedHQ : TextureImporterCompression.Compressed;
            ti.anisoLevel = 2;
            ti.isReadable = false;
        }
    }
}
