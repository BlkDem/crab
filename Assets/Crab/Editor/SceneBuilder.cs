// Builds the game's scenes from code (Crab > Build Scenes): the menu, the prologue's beach,
// Limbo and the circle of Wrath. Models come from Tools/blender (GLB through glTFast); the ground, materials, prefabs of
// the creatures, lights, fog, sky and post-processing are made here. Running it again rebuilds
// everything — changes that should last belong in this file.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Crab.Core;
using Crab.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;
using Random = System.Random;
using SceneSetup = Crab.Game.SceneSetup;

namespace Crab.EditorTools
{
    public static class SceneBuilder
    {
        public const string SceneDir = "Assets/Crab/Scenes";
        const string Gen = "Assets/Crab/Art/Generated";
        const string Models = "Assets/Crab/Models";
        static Random rnd;
        static float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
        static Library lib;
        static Func<float, float, float> ground;

        [MenuItem("Crab/Build Scenes")]
        public static void BuildAll()
        {
            Directory.CreateDirectory(SceneDir);
            Directory.CreateDirectory(Gen);
            ConfigureProject();
            lib = BuildLibrary();
            BuildPrologue();
            BuildLimbo();
            BuildWrath();
            BuildMenu();
            EditorBuildSettings.scenes = new[] { "Menu", "Prologue", "Limbo", "Wrath" }.Select(n => new EditorBuildSettingsScene($"{SceneDir}/{n}.unity", true)).ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("Crab scenes built");
        }

        // ------------------------------------------------------------------ project
        static void ConfigureProject()
        {
            foreach (var path in new[] { "Assets/Settings/PC_RPAsset.asset", "Assets/Settings/Mobile_RPAsset.asset" })
            {
                var a = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
                if (!a) continue;
                var so = new SerializedObject(a);
                void Set(string p, Action<SerializedProperty> f) { var sp = so.FindProperty(p); if (sp != null) f(sp); }
                Set("m_RequireDepthTexture", p => p.boolValue = true);   // the sea's foam reads the depth
                Set("m_ShadowDistance", p => p.floatValue = 70);
                Set("m_ShadowCascadeCount", p => p.intValue = 2);
                Set("m_MainLightShadowmapResolution", p => p.intValue = 2048);
                Set("m_SoftShadowsSupported", p => p.boolValue = true);
                Set("m_SupportsHDR", p => p.boolValue = true);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            PlayerSettings.productName = "Краб против ада";
            PlayerSettings.companyName = "umolab";
        }

        // ------------------------------------------------------------------ library
        static Material Mat(string name, string shader, Action<Material> setup = null)
        {
            string path = $"{Gen}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m)
            {
                m = new Material(Shader.Find(shader));
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = Shader.Find(shader);
            setup?.Invoke(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        static GameObject Glb(string path) => AssetDatabase.LoadAssetAtPath<GameObject>($"{Models}/{path}.glb");

        static Library BuildLibrary()
        {
            string path = $"{Gen}/Library.asset";
            var l = AssetDatabase.LoadAssetAtPath<Library>(path);
            if (!l) { l = ScriptableObject.CreateInstance<Library>(); AssetDatabase.CreateAsset(l, path); }
            l.hero = Glb("Hero/hero");
            l.items = new List<Library.Named>();
            foreach (var (id, file) in new[] { (ItemId.Stick, "stick"), (ItemId.Stone, "stone"), (ItemId.Crowbar, "crowbar"), (ItemId.Knife, "knife"), (ItemId.Pistol, "pistol"), (ItemId.MachineGun, "machinegun") })
                l.items.Add(new Library.Named { name = id.ToString(), model = Glb("Items/" + file) });
            l.stone = Glb("Items/stone");
            l.gull = Glb("World/gull");
            l.morsel = Glb("World/morsel");
            l.guardian = Glb("Enemies/guardian");
            l.colossus = Glb("Enemies/colossus");
            l.armor = Glb("Items/armor");
            l.drone = Glb("Items/drone");
            l.particle = Mat("ParticleSoft", "Crab/Soft");
            l.particleAdditive = Mat("ParticleGlow", "Crab/Glow", m => m.SetFloat("_Round", 1));
            l.tracer = Mat("Tracer", "Crab/Glow", m => m.SetFloat("_Round", 0));
            l.beam = Mat("Beam", "Crab/Glow", m => { m.SetFloat("_Round", 0); m.SetFloat("_Rim", 1); m.SetColor("_Tint", new Color(0.7f, 1f, 0.85f, 0.85f)); });
            l.rift = Mat("Rift", "Crab/Rift");
            l.water = Mat("Sea", "Crab/Water");
            l.fire = Mat("Fire", "Crab/Glow", m => { m.SetFloat("_Round", 0); m.SetFloat("_Rim", 1); m.SetColor("_Tint", new Color(1f, 0.55f, 0.15f, 1f)); });
            l.shade = EnemyPrefab<Shade>("Shade", Glb("Enemies/shade"), 0.35f, 1.8f, e => { e.maxHealth = 40; e.speed = 1.8f; e.attackRange = 1.7f; e.damage = 9; e.windup = 0.7f; e.attackCooldown = 1.5f; });
            l.crawler = EnemyPrefab<Crawler>("Crawler", Glb("Enemies/crawler"), 0.35f, 0.6f, e => { e.maxHealth = 28; e.speed = 3.6f; e.attackRange = 1.1f; e.damage = 8; e.windup = 0.35f; e.attackCooldown = 1.2f; });
            l.imp = EnemyPrefab<Imp>("Imp", Glb("Enemies/imp"), 0.35f, 1.3f, e => { e.maxHealth = 45; e.speed = 3.4f; e.attackRange = 1.4f; e.damage = 11; e.windup = 0.4f; e.attackCooldown = 1.1f; e.aggro = 16; });
            l.brute = EnemyPrefab<Brute>("Brute", Glb("Enemies/brute"), 0.8f, 3.0f, e => { e.maxHealth = 220; e.speed = 1.7f; e.attackRange = 2.6f; e.damage = 26; e.windup = 1.0f; e.attackCooldown = 2.4f; e.aggro = 14; });
            l.burrower = EnemyPrefab<Burrower>("Burrower", Glb("Enemies/burrower"), 0.5f, 0.9f, e => { e.maxHealth = 70; e.speed = 1.6f; e.attackRange = 1.6f; e.damage = 14; e.windup = 0.5f; e.attackCooldown = 1.6f; e.aggro = 15; });
            EditorUtility.SetDirty(l);
            return l;
        }

        static GameObject EnemyPrefab<T>(string name, GameObject model, float radius, float height, Action<T> setup) where T : Enemy
        {
            var go = new GameObject(name);
            var cc = go.AddComponent<CharacterController>();
            cc.radius = radius;
            cc.height = height;
            cc.center = new Vector3(0, height / 2 + 0.05f, 0);
            cc.stepOffset = Mathf.Min(0.3f, height * 0.4f);
            var m = (GameObject)PrefabUtility.InstantiatePrefab(model, go.transform);
            m.name = "Model";
            var e = go.AddComponent<T>();
            e.model = m.transform;
            setup(e);
            // a body to hit: the character controller catches shots and blows
            Directory.CreateDirectory("Assets/Crab/Prefabs");
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"Assets/Crab/Prefabs/{name}.prefab");
            UnityEngine.Object.DestroyImmediate(go);
            return prefab;
        }

        // ------------------------------------------------------------------ placing things
        static Transform Group(string name, Transform parent = null)
        {
            var t = new GameObject(name).transform;
            if (parent) t.SetParent(parent, false);
            return t;
        }

        static float H(float x, float z) => ground(x, z);

        /// <summary>A model instance standing on the ground; colliders: "mesh" (exact), "box", or none.</summary>
        static GameObject Place(GameObject model, Transform parent, float x, float z, float yaw = 0, float scale = 1, string collider = "mesh", float sink = 0.05f)
        {
            if (!model) { Debug.LogWarning("missing model"); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
            go.transform.position = new Vector3(x, H(x, z) - sink * scale, z);
            go.transform.rotation = Quaternion.Euler(0, yaw, 0);
            go.transform.localScale = Vector3.one * scale;
            if (collider == "mesh")
                foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
                {
                    if (mf.name.StartsWith("Glow")) continue;
                    mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                }
            else if (collider == "box")
            {
                var b = Bounds(go);
                var bc = go.AddComponent<BoxCollider>();
                bc.center = go.transform.InverseTransformPoint(b.center);
                bc.size = Vector3.Scale(b.size, new Vector3(1 / scale, 1 / scale, 1 / scale));
            }
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.On;
            return go;
        }

        static Bounds Bounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        /// <summary>One named child of a multi-variant model (beach_rocks > Rock2), placed alone.</summary>
        static GameObject Variant(GameObject model, string child, Transform parent, float x, float z, float yaw, float scale, string collider = "mesh")
        {
            var go = Place(model, parent, x, z, yaw, scale, collider: "none");
            foreach (Transform c in go.GetComponentsInChildren<Transform>(true).ToArray())
                if (c.parent == go.transform && c.name != child) UnityEngine.Object.DestroyImmediate(c.gameObject);
            if (collider == "mesh")
                foreach (var mf in go.GetComponentsInChildren<MeshFilter>()) mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
            return go;
        }

        static Light AddLight(Transform parent, string name, Vector3 pos, Color c, float intensity, float range, LightType type = LightType.Point)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = type;
            l.color = c;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            return l;
        }

        static GameObject Ground(string name, Rect area, Material mat, float uvScale)
        {
            var mesh = GroundMesh.Build(name, area, 1f, ground, uvScale);
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            go.isStatic = true;
            return go;
        }

        static GameObject Sea(Material mat, float y, float size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
            go.name = "Sea";
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            // a finer grid than the built-in plane, for the waves
            var mesh = GroundMesh.Build("SeaGrid", new Rect(-size / 2, -size / 2, size, size), 1.5f, (x, z) => 0, 0.1f);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.transform.position = new Vector3(0, y, 0);
            go.GetComponent<Renderer>().sharedMaterial = mat;
            go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        // ------------------------------------------------------------------ hero, camera, light, UI
        static Hero BuildHero(Vector3 pos, float yaw, out CameraRig rig)
        {
            var go = new GameObject("Hero");
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            var cc = go.AddComponent<CharacterController>();
            cc.radius = 0.34f;
            cc.height = 0.6f;
            cc.center = new Vector3(0, 0.32f, 0);
            cc.stepOffset = 0.25f;
            cc.slopeLimit = 50;
            cc.skinWidth = 0.03f;
            var model = (GameObject)PrefabUtility.InstantiatePrefab(lib.hero, go.transform);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            var crabRig = go.AddComponent<CrabRig>();
            crabRig.body = CrabRig.BodyOf(model.transform);
            crabRig.gripRight = CrabRig.Find(model.transform, "GripR");
            crabRig.gripLeft = CrabRig.Find(model.transform, "GripL");
            var ctrl = go.AddComponent<CrabController>();
            ctrl.rig = crabRig;
            var hero = go.AddComponent<Hero>();
            var combat = go.AddComponent<HeroCombat>();
            var power = go.AddComponent<MatterPower>();
            hero.Controller = ctrl;
            hero.Rig = crabRig;
            hero.Combat = combat;
            hero.Power = power;
            combat.hero = hero;
            power.hero = hero;
            var core = new GameObject("Core").transform;
            core.SetParent(go.transform, false);
            core.localPosition = new Vector3(0, 0.28f, 0);
            hero.Core = core;
            hero.sparkLight = AddLight(core, "Spark", core.position + Vector3.up * 0.25f, new Color(1f, 0.85f, 0.5f), 0.6f, 2.5f);
            hero.sparkLight.enabled = false;
            // the first-person view sits between the eyes
            var eyeL = CrabRig.Find(model.transform, "EyeL");
            var eyeR = CrabRig.Find(model.transform, "EyeR");
            var eye = new GameObject("EyePoint").transform;
            eye.SetParent(go.transform, false);
            var mid = eyeL && eyeR ? (Bounds(eyeL.gameObject).center + Bounds(eyeR.gameObject).center) / 2 : go.transform.position + Vector3.up * 0.35f;
            eye.position = mid + Vector3.up * 0.05f + go.transform.forward * 0.05f;

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 600;
            camGo.AddComponent<AudioListener>();
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            rig = camGo.AddComponent<CameraRig>();
            rig.target = go.transform;
            rig.eye = eye;
            rig.cam = cam;
            camGo.transform.position = pos + Quaternion.Euler(18, yaw, 0) * new Vector3(0, 0, -3.6f) + Vector3.up * 0.75f;
            camGo.transform.LookAt(pos + Vector3.up * 0.6f);
            ctrl.cam = rig;
            hero.Cam = rig;
            return hero;
        }

        static Light Sun(Color color, float intensity, Vector3 euler)
        {
            var go = new GameObject("Sun");
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.color = color;
            l.intensity = intensity;
            l.shadows = LightShadows.Soft;
            l.shadowStrength = 0.85f;
            go.transform.rotation = Quaternion.Euler(euler);
            RenderSettings.sun = l;
            return l;
        }

        static void Environment(Color skyTint, Color ground, float exposure, Color ambSky, Color ambEq, Color ambGround, Color fog, float fogDensity, float sunSize = 0.04f, float atmosphere = 0.85f)
        {
            var sky = Mat($"Sky_{EditorSceneManager.GetActiveScene().name}", "Skybox/Procedural", m =>
            {
                m.SetColor("_SkyTint", skyTint);
                m.SetColor("_GroundColor", ground);
                m.SetFloat("_Exposure", exposure);
                m.SetFloat("_SunSize", sunSize);
                m.SetFloat("_AtmosphereThickness", atmosphere);
            });
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = ambSky;
            RenderSettings.ambientEquatorColor = ambEq;
            RenderSettings.ambientGroundColor = ambGround;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = fog;
            RenderSettings.fogDensity = fogDensity;
        }

        static void Post(string name, float exposure, float saturation, Color filter, float bloom, float vignette, float contrast = 10)
        {
            string path = $"{Gen}/Volume_{name}.asset";
            AssetDatabase.DeleteAsset(path);
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(p, path);
            var tm = p.Add<Tonemapping>(true); tm.mode.value = TonemappingMode.ACES;
            var b = p.Add<Bloom>(true); b.intensity.value = bloom; b.threshold.value = 0.9f; b.scatter.value = 0.7f;
            var ca = p.Add<ColorAdjustments>(true); ca.postExposure.value = exposure; ca.saturation.value = saturation; ca.colorFilter.value = filter; ca.contrast.value = contrast;
            var v = p.Add<Vignette>(true); v.intensity.value = vignette; v.smoothness.value = 0.45f;
            foreach (var c in p.components) AssetDatabase.AddObjectToAsset(c, p);
            EditorUtility.SetDirty(p);
            var go = new GameObject("Post");
            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = p;
        }

        static PanelSettings Panel()
        {
            string path = "Assets/Crab/UI/CrabPanel.asset";
            var ps = AssetDatabase.LoadAssetAtPath<PanelSettings>(path);
            if (!ps) { ps = ScriptableObject.CreateInstance<PanelSettings>(); AssetDatabase.CreateAsset(ps, path); }
            ps.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>("Assets/Crab/UI/CrabTheme.tss");
            ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            ps.referenceResolution = new Vector2Int(1600, 900);
            ps.match = 0.5f;
            EditorUtility.SetDirty(ps);
            return ps;
        }

        static void Ui<T>() where T : MonoBehaviour
        {
            var go = new GameObject("UI");
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = Panel();
            go.AddComponent<T>();
        }

        static SceneSetup Setup(string name, Transform start)
        {
            var go = new GameObject("SceneSetup");
            var s = go.AddComponent<SceneSetup>();
            s.library = lib;
            s.sceneName = name;
            s.start = start;
            return s;
        }

        static UnityEngine.SceneManagement.Scene NewScene(string name)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = name;
            rnd = new Random(name.GetHashCode() & 0xffff);
            return scene;
        }

        static void SaveScene(UnityEngine.SceneManagement.Scene scene, string name)
        {
            EditorSceneManager.SaveScene(scene, $"{SceneDir}/{name}.unity");
        }

        // ------------------------------------------------------------------ the beach (prologue and menu)
        static float BeachHeight(float x, float z)
        {
            // the beach rises gently from the sea (-Z) to dunes (+Z); the seabed drops away
            float h = z * 0.045f;
            if (z < -4) h -= (-4 - z) * 0.09f;
            float dunes = Mathf.Clamp01((z - 12) / 10f);
            h += dunes * (Mathf.PerlinNoise(x * 0.06f + 3, z * 0.06f) * 2.5f);
            h += (Mathf.PerlinNoise(x * 0.25f, z * 0.25f) - 0.5f) * 0.12f;
            return h;
        }

        static (Material sand, Material sea) BeachMaterials()
        {
            var (sc, sn) = TextureGen.Sand();
            var sand = Mat("Sand", "Universal Render Pipeline/Lit", m =>
            {
                m.SetTexture("_BaseMap", sc);
                m.SetTexture("_BumpMap", sn);
                m.EnableKeyword("_NORMALMAP");
                m.SetFloat("_BumpScale", 1);
                m.SetFloat("_Smoothness", 0.15f);
            });
            var sea = Mat("SeaBeach", "Crab/Water", m =>
            {
                m.SetColor("_Shallow", new Color(0.3f, 0.8f, 0.78f));
                m.SetColor("_Deep", new Color(0.02f, 0.25f, 0.4f));
                m.SetColor("_Sky", new Color(0.7f, 0.85f, 0.95f));
            });
            return (sand, sea);
        }

        static void BeachScenery(Transform root, bool full)
        {
            var palm = Glb("World/palm");
            var rocks = Glb("World/beach_rocks");
            var props = Group("Scenery", root);
            foreach (var (x, z) in new[] { (-16f, 16f), (-9f, 19f), (-3f, 23f), (5f, 17f), (11f, 21f), (18f, 15f), (-22f, 22f), (24f, 24f), (0f, 30f), (-12f, 28f), (13f, 31f) })
                Place(palm, props, x + R(-1, 1), z + R(-1, 1), R(0, 360), R(0.85f, 1.2f), collider: "none");
            for (int i = 0; i < 16; i++)
            {
                float x = R(-30, 30), z = R(-6, 30);
                if (Mathf.Abs(x) < 4 && z < 10) continue;
                Variant(rocks, $"Rock{rnd.Next(4)}", props, x, z, R(0, 360), R(0.5f, 1.4f));
            }
            if (!full) return;
            var weed = Glb("World/seaweed");
            for (int i = 0; i < 10; i++) Place(weed, props, R(-25, 25), R(-3, 6), R(0, 360), R(0.8f, 1.4f), collider: "none", sink: 0);
        }

        // ------------------------------------------------------------------ prologue
        static void BuildPrologue()
        {
            var scene = NewScene("Prologue");
            ground = BeachHeight;
            var (sand, sea) = BeachMaterials();
            var root = Group("Beach");
            Ground("BeachGround", new Rect(-60, -40, 120, 100), sand, 0.25f).transform.SetParent(root, false);
            Sea(sea, -0.25f, 400).transform.SetParent(root, false);
            Environment(new Color(0.5f, 0.5f, 0.52f), new Color(0.6f, 0.55f, 0.45f), 1.25f, new Color(0.55f, 0.65f, 0.8f), new Color(0.7f, 0.65f, 0.55f), new Color(0.35f, 0.3f, 0.25f), new Color(0.7f, 0.8f, 0.9f), 0.004f);
            var sun = Sun(new Color(1f, 0.93f, 0.8f), 1.6f, new Vector3(42, -30, 0));
            Post("Prologue", 0.1f, 12, Color.white, 0.6f, 0.2f);
            BeachScenery(root, true);

            var start = Group("Start", root);
            start.position = new Vector3(0, H(0, 2), 2);
            start.rotation = Quaternion.Euler(0, 180, 0);
            var hero = BuildHero(start.position, 180, out var cam);
            var setup = Setup("Prologue", start);

            var dir = new GameObject("Prologue").AddComponent<PrologueDirector>();
            // food
            foreach (var (x, z) in new[] { (-4f, -1f), (5f, -3f), (2.5f, 6f) })
            {
                var m = Place(lib.morsel, root, x, z, R(0, 360), 1.6f, collider: "none", sink: 0);
                var e = m.AddComponent<Edible>();
                e.title = "Рыбёшка";
                e.heal = 15;
                dir.morsels.Add(e);
            }
            foreach (var (x, z) in new[] { (-8f, 3f), (9f, 1f) })
            {
                var w = Place(Glb("World/seaweed"), root, x, z, R(0, 360), 1.2f, collider: "none", sink: 0);
                var e = w.AddComponent<Edible>();
                e.title = "Водоросли";
                e.heal = 20;
            }
            // the shelter from the gull
            var shelter = Place(Glb("World/shelter"), root, 8, 4.5f, 20, 1.0f);
            var zone = new GameObject("ShelterZone").AddComponent<BoxCollider>();
            zone.isTrigger = true;
            zone.transform.SetParent(shelter.transform, false);
            zone.center = new Vector3(0, 0.35f, 0);
            zone.size = new Vector3(2.6f, 0.75f, 2.0f);
            dir.shelter = zone;
            // the gull
            var gull = (GameObject)PrefabUtility.InstantiatePrefab(lib.gull);
            gull.transform.position = new Vector3(0, 10, 5);
            gull.transform.localScale = Vector3.one * 1.3f;
            var g = gull.AddComponent<Gull>();
            g.circleCenter = new Vector3(0, H(0, 4), 4);
            dir.gull = g;
            // the big wave: a long slab of water rolling up the beach
            var waveGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            waveGo.name = "Wave";
            UnityEngine.Object.DestroyImmediate(waveGo.GetComponent<Collider>());
            waveGo.transform.localScale = new Vector3(90, 0.9f, 4);
            waveGo.transform.position = new Vector3(0, 0.1f, -16);
            waveGo.GetComponent<Renderer>().sharedMaterial = sea;
            var wave = waveGo.AddComponent<Wave>();
            wave.startZ = -16;
            wave.reachZ = 9;
            wave.speed = 5.2f;
            waveGo.SetActive(false);
            dir.wave = wave;
            // the storm
            var storm = new GameObject("Storm").AddComponent<Storm>();
            storm.sun = sun;
            storm.rain = Rain(storm.transform);
            dir.storm = storm;
            // driftwood: the first matter the crab ever draws in
            var drift = Place(Glb("World/driftwood"), root, -5, 9, 30, 1.2f, collider: "none", sink: 0);
            var src = drift.AddComponent<MatterSource>();
            src.kind = Matter.Organic;
            src.amount = 8;
            dir.driftwood = src;
            Place(Glb("World/driftwood"), root, 6, 11, -60, 1.0f, collider: "none", sink: 0).AddComponent<MatterSource>().kind = Matter.Organic;
            // the rift that opens at the end
            var rift = RiftAt(root, 0, 19, 180, "prologue_exit");
            rift.leadsTo = "Limbo";
            rift.delay = 0.5f;
            dir.rift = rift;
            dir.sparkLight = hero.sparkLight;
            Ui<GameUI>();
            SaveScene(scene, "Prologue");
        }

        static ParticleSystem Rain(Transform parent)
        {
            var go = new GameObject("Rain");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.startLifetime = 1.2f;
            main.startSpeed = 18;
            main.startSize = 0.03f;
            main.startColor = new Color(0.75f, 0.82f, 0.95f, 0.45f);
            main.maxParticles = 3000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission;
            em.rateOverTime = 0;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(40, 1, 40);
            go.transform.rotation = Quaternion.Euler(90, 0, 0);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.lengthScale = 6;
            r.sharedMaterial = lib.particle;
            return ps;
        }

        static Rift RiftAt(Transform parent, float x, float z, float yaw, string id)
        {
            var frame = Place(Glb("World/rift"), parent, x, z, yaw, 1.1f);
            var rift = frame.AddComponent<Rift>();
            rift.id = id;
            rift.reach = 3.2f;
            var disc = GameObject.CreatePrimitive(PrimitiveType.Quad);
            disc.name = "Membrane";
            UnityEngine.Object.DestroyImmediate(disc.GetComponent<Collider>());
            disc.transform.SetParent(frame.transform, false);
            disc.transform.localPosition = new Vector3(0, 1.75f, 0);
            disc.transform.localScale = new Vector3(2.4f, 3.3f, 1);
            disc.GetComponent<Renderer>().sharedMaterial = lib.rift;
            disc.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            rift.membrane = disc.GetComponent<Renderer>();
            rift.glow = AddLight(frame.transform, "Glow", frame.transform.position + Vector3.up * 1.8f + frame.transform.forward * 0.8f, new Color(0.45f, 1f, 0.65f), 2.5f, 12);
            return rift;
        }

        // ------------------------------------------------------------------ Limbo
        static float LimboHeight(float x, float z)
        {
            float h = 0;
            if (z < -26) h -= (-26 - z) * 0.12f;   // down to the grey sea
            h += (Mathf.PerlinNoise(x * 0.05f + 7, z * 0.05f + 3) - 0.5f) * 1.2f;
            h += (Mathf.PerlinNoise(x * 0.2f, z * 0.2f) - 0.5f) * 0.15f;
            // a flatter square for the Guardian
            float sq = Mathf.Clamp01(1 - (new Vector2(x, z - 72).magnitude - 14) / 6f);
            return Mathf.Lerp(h, 0.2f, sq);
        }

        static void BuildLimbo()
        {
            var scene = NewScene("Limbo");
            ground = LimboHeight;
            var (ac, an) = TextureGen.Ash();
            var ash = Mat("Ash", "Universal Render Pipeline/Lit", m =>
            {
                m.SetTexture("_BaseMap", ac);
                m.SetTexture("_BumpMap", an);
                m.EnableKeyword("_NORMALMAP");
                m.SetFloat("_Smoothness", 0.08f);
            });
            var sea = Mat("SeaLimbo", "Crab/Water", m =>
            {
                m.SetColor("_Shallow", new Color(0.32f, 0.38f, 0.38f));
                m.SetColor("_Deep", new Color(0.08f, 0.11f, 0.12f));
                m.SetColor("_Sky", new Color(0.5f, 0.55f, 0.56f));
                m.SetFloat("_WaveHeight", 0.18f);
            });
            var root = Group("Limbo");
            Ground("LimboGround", new Rect(-80, -60, 160, 170), ash, 0.25f).transform.SetParent(root, false);
            Sea(sea, -1.2f, 500).transform.SetParent(root, false);
            Environment(new Color(0.38f, 0.42f, 0.44f), new Color(0.2f, 0.21f, 0.21f), 0.7f, new Color(0.5f, 0.57f, 0.6f), new Color(0.36f, 0.4f, 0.4f), new Color(0.16f, 0.16f, 0.15f), new Color(0.4f, 0.46f, 0.47f), 0.018f, 0.0f);
            Sun(new Color(0.8f, 0.88f, 0.9f), 1.1f, new Vector3(50, 30, 0));
            // a cold, washed-out world; the orange crab is the one warm thing in it
            Post("Limbo", 0.45f, -22, new Color(0.92f, 1f, 0.97f), 0.9f, 0.3f, 14);

            // the town: houses along two streets, a tower, walls, columns, dead trees, lamps
            var town = Group("Town", root);
            var houses = new[] { Glb("World/house0"), Glb("World/house1") };
            var placed = new List<Vector3>();
            void House(float x, float z, float yaw, int v)
            {
                Place(houses[v], town, x, z, yaw, 1, sink: 0.2f);
                placed.Add(new Vector3(x, 0, z));
            }
            for (int i = 0; i < 6; i++)
            {
                float z = -12 + i * 11;
                House(-11 - R(0, 2), z, 90 + R(-6, 6), i % 2);
                House(11 + R(0, 2), z + 3, -90 + R(-6, 6), (i + 1) % 2);
                if (i % 2 == 0) House(-22 - R(0, 3), z + 4, R(0, 360), (i + 1) % 2);
                if (i % 2 == 1) House(23 + R(0, 3), z, R(0, 360), i % 2);
            }
            Place(Glb("World/tower"), town, -20, 34, 25, 1, sink: 0.2f);
            Place(Glb("World/tower"), town, 21, 52, -35, 1.1f, sink: 0.2f);
            foreach (var (x, z, yaw) in new[] { (-6f, -16f, 10f), (7f, 26f, -80f), (-30f, 8f, 90f), (32f, 30f, 60f), (-5f, 60f, 0f), (6f, 61f, 0f) })
                Place(Glb("World/wall"), town, x, z, yaw, 1, sink: 0.2f);
            var lamp = Glb("World/lamp");
            for (int i = 0; i < 9; i++) Place(lamp, town, (i % 2 == 0 ? -5.5f : 5.5f) + R(-0.3f, 0.3f), -14 + i * 8, i % 2 == 0 ? 90 : -90, 1, collider: "box");
            var tree = Glb("World/deadtree");
            for (int i = 0; i < 12; i++)
            {
                float x = R(-45, 45), z = R(-20, 80);
                if (Mathf.Abs(x) < 8 || placed.Any(p => Vector3.Distance(p, new Vector3(x, 0, z)) < 7)) continue;
                Place(tree, town, x, z, R(0, 360), R(0.8f, 1.3f), collider: "box");
            }
            var col = Glb("World/column");
            for (int i = 0; i < 6; i++) Place(col, town, R(-30, 30), R(-15, 55), R(0, 360), R(0.8f, 1.1f));
            var rocks = Glb("World/beach_rocks");
            for (int i = 0; i < 10; i++) Variant(rocks, $"Rock{rnd.Next(4)}", town, R(-50, 50), R(-30, -20), R(0, 360), R(0.6f, 1.6f));

            // the start on the grey shore and the beacons
            var start = Group("Start", root);
            start.position = new Vector3(0, H(0, -22), -22);
            start.rotation = Quaternion.Euler(0, 0, 0);
            var hero = BuildHero(start.position, 0, out _);
            Setup("Limbo", start);
            var dir = new GameObject("Limbo").AddComponent<LimboDirector>();
            dir.beacons.Add(BeaconAt(root, 3, -19, -30, "shore"));
            dir.beacons.Add(BeaconAt(root, 0, 12, 180, "town"));
            dir.beacons.Add(BeaconAt(root, 3, 50, 180, "square"));

            // the first shades
            foreach (var (x, z) in new[] { (-3f, -7f), (4f, -4f) })
            {
                var s = (GameObject)PrefabUtility.InstantiatePrefab(lib.shade);
                s.transform.position = new Vector3(x, H(x, z), z);
                s.transform.rotation = Quaternion.Euler(0, 180, 0);
                dir.firstShades.Add(s.GetComponent<Enemy>());
            }

            // matter: rubble (mineral; teaches stones), planks (organic), scrap (metal; teaches the crowbar and the knife)
            void Source(string model, float x, float z, Matter kind, int amount, float regrow, ItemId[] teaches, string thought)
            {
                var go = Place(Glb("World/" + model), root, x, z, R(0, 360), 1, collider: "none", sink: 0.05f);
                var ms = go.AddComponent<MatterSource>();
                ms.kind = kind;
                ms.amount = amount;
                ms.regrow = regrow;
                ms.teaches = teaches.ToList();
                ms.firstThought = thought;
                ms.reach = 2.6f;
            }
            const string rubbleT = "Камень. Из него можно сделать… просто камень. И метнуть.";
            const string scrapT = "Металл! Он тяжёлый и послушный. Лом. Нож. Я вижу, как их сложить.";
            foreach (var (x, z) in new[] { (-4f, -14f), (6f, 4f), (-16f, 20f), (16f, 36f), (-8f, 44f) }) Source("rubble", x, z, Matter.Mineral, 6, 0, new[] { ItemId.Stone }, rubbleT);
            foreach (var (x, z) in new[] { (5f, -12f), (-7f, 8f), (18f, 14f), (-18f, 40f) }) Source("planks", x, z, Matter.Organic, 6, 0, new ItemId[0], null);
            foreach (var (x, z) in new[] { (-6f, -2f), (8f, 18f), (-24f, 14f), (26f, 22f), (2f, 36f), (-3f, 54f) }) Source("scrap", x, z, Matter.Metal, 7, 0, new[] { ItemId.Crowbar, ItemId.Knife }, scrapT);

            // the rifts
            dir.rifts.Add(RiftWithSpawns(root, -30, 16, 70, "west", 0.2f));
            dir.rifts.Add(RiftWithSpawns(root, 31, 24, -70, "east", 0.5f));
            dir.rifts.Add(RiftWithSpawns(root, 0, 38, 180, "north", 0.5f));

            // the Guardian's square: a ring of columns and broken walls; matter that comes back for the fight
            var square = Group("Square", root);
            square.position = new Vector3(0, H(0, 72), 72);
            for (int i = 0; i < 10; i++)
            {
                float a = i / 10f * Mathf.PI * 2;
                if (Mathf.Abs(Mathf.Sin(a) + 1) < 0.25f) continue;   // the way in from the south stays open
                float x = Mathf.Cos(a) * 17, z = 72 + Mathf.Sin(a) * 17;
                Place(i % 3 == 0 ? Glb("World/wall") : col, square, x, z, -a * Mathf.Rad2Deg + 90, i % 3 == 0 ? 0.7f : 1.2f);
            }
            foreach (var (x, z) in new[] { (-11f, 66f), (11f, 66f), (-12f, 80f), (12f, 80f) }) Source("scrap", x, z, Matter.Metal, 5, 22, new ItemId[0], null);
            foreach (var (x, z) in new[] { (0f, 60f), (-13f, 73f), (13f, 73f) }) Source("rubble", x, z, Matter.Mineral, 4, 25, new ItemId[0], null);
            var g = (GameObject)PrefabUtility.InstantiatePrefab(lib.guardian);
            var gRoot = new GameObject("Guardian");
            gRoot.transform.position = new Vector3(0, H(0, 74), 74);
            gRoot.transform.rotation = Quaternion.Euler(0, 180, 0);
            g.transform.SetParent(gRoot.transform, false);
            var capsule = gRoot.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0, 3, 0);
            capsule.height = 6.5f;
            capsule.radius = 0.8f;   // the heart (0.4 m) sticks out in front, so shots at it reach it
            var guardian = gRoot.AddComponent<Guardian>();
            guardian.model = g.transform;
            guardian.heartLight = AddLight(gRoot.transform, "HeartLight", gRoot.transform.position + new Vector3(0, 3.7f, 1.1f), new Color(0.5f, 1f, 0.7f), 1, 10);
            AddLight(gRoot.transform, "LanternLight", gRoot.transform.position + new Vector3(-1.6f, 2.6f, 1.2f), new Color(0.75f, 1f, 0.85f), 2, 9);
            dir.guardian = guardian;
            dir.square = square;
            var exit = RiftAt(root, 0, 88, 180, "exit");
            exit.leadsTo = "Wrath";
            exit.delay = 6;
            exit.membrane.sharedMaterial = Mat("RiftFire", "Crab/Rift", m => { m.SetColor("_Color", new Color(1f, 0.45f, 0.15f)); m.SetColor("_Core", new Color(0.08f, 0.01f, 0)); });
            exit.glow.color = new Color(1f, 0.5f, 0.2f);
            dir.exit = exit;
            Ui<GameUI>();
            SaveScene(scene, "Limbo");
        }

        static Beacon BeaconAt(Transform parent, float x, float z, float yaw, string id)
        {
            var go = Place(Glb("World/beacon"), parent, x, z, yaw, 1);
            var b = go.AddComponent<Beacon>();
            b.id = id;
            b.glow = AddLight(go.transform, "Fire", go.transform.position + Vector3.up * 1.6f, new Color(0.5f, 1f, 0.7f), 0.4f, 8);
            return b;
        }

        static Rift RiftWithSpawns(Transform parent, float x, float z, float yaw, string id, float crawlers)
        {
            var r = RiftAt(parent, x, z, yaw, id);
            r.crawlerShare = crawlers;
            r.budget = 8;
            r.maxAlive = 3;
            r.spawnEvery = 6;
            return r;
        }

        // ------------------------------------------------------------------ Wrath
        const float LavaLevel = -0.3f;
        static readonly Vector3 LakeIsland = new Vector3(0, 0, 88);

        /// <summary>Where the lava river runs (its middle line, z for a given x).</summary>
        static float RiverZ(float x) => -12 + 4 * Mathf.Sin(x * 0.06f);

        static float WrathHeight(float x, float z)
        {
            // big dunes and ripples on them
            float h = (Mathf.PerlinNoise(x * 0.03f + 11, z * 0.03f + 5) - 0.5f) * 3.2f;
            h += (Mathf.PerlinNoise(x * 0.09f + 2, z * 0.09f) - 0.5f) * 0.8f;
            h += (Mathf.PerlinNoise(x * 0.3f, z * 0.3f) - 0.5f) * 0.1f;
            // the burnt town and the start lie on calmer ground
            float town = Mathf.Clamp01(1 - Mathf.Max(Mathf.Abs(x) - 26, Mathf.Abs(z - 22) - 22) / 8f);
            float start = Mathf.Clamp01(1 - (new Vector2(x, z + 44).magnitude - 6) / 6f);
            h = Mathf.Lerp(h, h * 0.25f + 0.4f, Mathf.Max(town, start));
            h = Mathf.Max(h, LavaLevel + 0.35f);   // dry land stays above the lava
            // the river, with a land bridge at x ≈ 5.5
            float dr = Mathf.Abs(z - RiverZ(x));
            float bridge = Mathf.Clamp01((Mathf.Abs(x - 5.5f) - 2.2f) / 2f);
            h = Mathf.Lerp(h, -1.0f, Mathf.Clamp01((4.6f - dr) / 2f) * bridge);
            // the lake around the island, crossed by a causeway from the south
            float dl = new Vector2(x - LakeIsland.x, z - LakeIsland.z).magnitude;
            float causeway = z > LakeIsland.z ? 1 : Mathf.Clamp01((Mathf.Abs(x) - 2.6f) / 1.5f);
            float lake = Mathf.Clamp01((dl - 17.5f) / 2.5f) * Mathf.Clamp01((30 - dl) / 4f) * causeway;
            h = Mathf.Lerp(h, -1.3f, lake);
            h = Mathf.Lerp(h, 0.3f + (Mathf.PerlinNoise(x * 0.2f, z * 0.2f) - 0.5f) * 0.15f, Mathf.Clamp01((17 - dl) / 2.5f));
            // the land ends in a sea of lava
            float ex = Mathf.Max(0, Mathf.Abs(x) - 80), ez = Mathf.Max(0, Mathf.Max(-56 - z, z - 128));
            h -= (ex + ez) * 0.35f;
            return h;
        }

        static void BuildWrath()
        {
            var scene = NewScene("Wrath");
            ground = WrathHeight;
            var (dc, dn) = TextureGen.Dunes();
            var dunes = Mat("Dunes", "Universal Render Pipeline/Lit", m =>
            {
                m.SetTexture("_BaseMap", dc);
                m.SetTexture("_BumpMap", dn);
                m.EnableKeyword("_NORMALMAP");
                m.SetFloat("_Smoothness", 0.12f);
            });
            var lava = Mat("Lava", "Crab/Lava");
            var root = Group("Wrath");
            Ground("WrathGround", new Rect(-95, -70, 190, 210), dunes, 0.2f).transform.SetParent(root, false);
            // one lava surface under everything: it shows wherever the ground dips below it
            var lavaGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            lavaGo.name = "Lava";
            UnityEngine.Object.DestroyImmediate(lavaGo.GetComponent<Collider>());
            lavaGo.transform.SetParent(root, false);
            lavaGo.transform.SetPositionAndRotation(new Vector3(0, LavaLevel, 30), Quaternion.Euler(90, 0, 0));
            lavaGo.transform.localScale = new Vector3(900, 900, 1);
            lavaGo.GetComponent<Renderer>().sharedMaterial = lava;
            lavaGo.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            lavaGo.AddComponent<LavaZone>().level = LavaLevel;
            Environment(new Color(1f, 0.5f, 0.28f), new Color(0.22f, 0.08f, 0.04f), 1.35f, new Color(0.78f, 0.52f, 0.42f), new Color(0.74f, 0.46f, 0.3f), new Color(0.36f, 0.18f, 0.1f), new Color(0.66f, 0.38f, 0.24f), 0.0055f, 0.07f, 1.6f);
            var wrathSun = Sun(new Color(1f, 0.72f, 0.5f), 1.8f, new Vector3(28, 150, 0));
            RenderSettings.skybox = Mat("Sky_Wrath_Gradient", "Crab/Sky", m =>
            {
                m.SetColor("_Top", new Color(0.32f, 0.1f, 0.07f));
                m.SetColor("_Horizon", new Color(0.95f, 0.5f, 0.27f));
                m.SetColor("_Bottom", new Color(0.3f, 0.1f, 0.05f));
                m.SetColor("_Sun", new Color(1f, 0.7f, 0.4f));
                m.SetVector("_SunDir", -wrathSun.transform.forward);
            });
            Post("Wrath", 0.25f, 8, new Color(1f, 0.95f, 0.88f), 1.2f, 0.3f, 12);
            // the lava's glow from below
            AddLight(root, "LakeGlow", LakeIsland + new Vector3(0, 1, 24), new Color(1f, 0.45f, 0.15f), 4, 40);

            // the horizon: volcanoes in the lava sea
            var volcano = Glb("World/volcano");
            foreach (var (x, z, s) in new[] { (-150f, 190f, 1.4f), (170f, 140f, 1.1f), (40f, 260f, 1.8f), (-190f, 20f, 1.0f), (200f, -40f, 0.9f) })
            {
                var v = Place(volcano, root, x, z, R(0, 360), s, collider: "none", sink: 0);
                v.transform.position = new Vector3(x, LavaLevel - 2, z);
                AddLight(v.transform, "Crater", new Vector3(x, LavaLevel + 40 * s, z), new Color(1f, 0.45f, 0.15f), 6, 90 * s);
                Smoke(v.transform, new Vector3(x, LavaLevel + 38 * s, z), s);
            }

            var props = Group("Scenery", root);
            var spires = Glb("World/spires");
            // places that stay open: the start, the beacons, the vents, the way north
            var clear = new[] { new Vector2(0, -46), new Vector2(3, -43), new Vector2(0, 10), new Vector2(3, 54), new Vector2(-40, 22), new Vector2(42, 30), new Vector2(-14, 48) };
            bool Clear(float x, float z) => clear.All(c => (c - new Vector2(x, z)).magnitude > 9) && Mathf.Abs(x - 4) > 5;
            for (int i = 0; i < 26; i++)
            {
                float x = R(-75, 75), z = R(-50, 70);
                if (Mathf.Abs(x) < 30 && z > -6 && z < 48) continue;   // not in the town
                if (Mathf.Abs(z - RiverZ(x)) < 6 || !Clear(x, z)) continue;
                Variant(spires, $"Spire{rnd.Next(4)}", props, x, z, R(0, 360), R(0.8f, 1.6f));
            }
            // black spires ring the lake
            for (int i = 0; i < 14; i++)
            {
                float a = i / 14f * Mathf.PI * 2 + R(-0.1f, 0.1f);
                if (Mathf.Abs(Mathf.Sin(a) + 1) < 0.3f) continue;   // the causeway comes from the south
                float r = R(32, 38);
                Variant(spires, $"Spire{rnd.Next(4)}", props, LakeIsland.x + Mathf.Cos(a) * r, LakeIsland.z + Mathf.Sin(a) * r, R(0, 360), R(1.2f, 2.2f));
            }
            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * Mathf.PI * 2 + 0.3f;
                if (Mathf.Abs(Mathf.Sin(a) + 1) < 0.4f) continue;
                Variant(spires, $"Rock{rnd.Next(3)}", props, LakeIsland.x + Mathf.Cos(a) * 15.5f, LakeIsland.z + Mathf.Sin(a) * 15.5f, R(0, 360), R(0.8f, 1.2f));
            }
            var btree = Glb("World/burnt_tree");
            for (int i = 0; i < 18; i++)
            {
                float x = R(-70, 70), z = R(-50, 65);
                if (Mathf.Abs(z - RiverZ(x)) < 6 || !Clear(x, z)) continue;
                Place(btree, props, x, z, R(0, 360), R(0.8f, 1.3f), collider: "box");
            }
            var totem = Glb("World/totem");
            foreach (var (x, z) in new[] { (-4f, -30f), (5f, -24f), (-3f, 50f), (4f, 55f), (-3.5f, 58f) })
                Place(totem, props, x, z, R(0, 360), R(0.9f, 1.1f), collider: "box");

            // the burnt town
            var town = Group("Town", root);
            var bh = Glb("World/burnt_house");
            var bw = Glb("World/burnt_wall");
            foreach (var (x, z, yaw) in new[] { (-12f, 4f, 90f), (13f, 8f, -90f), (-14f, 18f, 85f), (12f, 22f, -95f), (-13f, 33f, 95f), (14f, 36f, -80f), (-24f, 12f, 30f), (25f, 28f, -20f), (-22f, 40f, 160f) })
                Place(bh, town, x + R(-1, 1), z + R(-1, 1), yaw + R(-8, 8), 1, sink: 0.15f);
            foreach (var (x, z, yaw) in new[] { (-6f, -2f, 15f), (7f, 14f, -70f), (-28f, 26f, 90f), (28f, 14f, 80f), (-7f, 42f, 5f), (8f, 44f, -10f) })
                Place(bw, town, x, z, yaw, 1, sink: 0.15f);

            // start, beacons, director
            var start = Group("Start", root);
            start.position = new Vector3(0, H(0, -46), -46);
            var hero = BuildHero(start.position, 0, out _);
            Setup("Wrath", start);
            var dir = new GameObject("Wrath").AddComponent<WrathDirector>();
            dir.beacons.Add(BeaconAt(root, 3, -43, -30, "wrath_gate"));
            dir.beacons.Add(BeaconAt(root, 0, 10, 180, "wrath_town"));
            dir.beacons.Add(BeaconAt(root, 3, 54, 180, "wrath_lake"));
            foreach (var (x, z) in new[] { (-3f, -27f), (4f, -25f), (0f, -21f) })
            {
                var s = (GameObject)PrefabUtility.InstantiatePrefab(lib.imp);
                s.transform.position = new Vector3(x, H(x, z), z);
                s.transform.rotation = Quaternion.Euler(0, 180, 0);
                dir.firstImps.Add(s.GetComponent<Enemy>());
            }

            // matter: scrap in the town (teaches the machine gun), stone, bones (organic)
            void Source(GameObject model, float x, float z, Matter kind, int amount, float regrow, ItemId[] teaches, string thought, float scale = 1, float reach = 2.6f)
            {
                var go = Place(model, root, x, z, R(0, 360), scale, collider: "none", sink: 0.05f);
                var ms = go.AddComponent<MatterSource>();
                ms.kind = kind;
                ms.amount = amount;
                ms.regrow = regrow;
                ms.teaches = teaches.ToList();
                ms.firstThought = thought;
                ms.reach = reach;
            }
            const string scrapT = "Здесь металла — горы. Я вижу оружие, которое не умолкает. Пулемёт!";
            var scrap = Glb("World/scrap");
            var rubble = Glb("World/rubble");
            foreach (var (x, z) in new[] { (-5f, 6f), (6f, 3f), (-8f, 20f), (8f, 28f), (-4f, 36f), (20f, 18f), (-19f, 30f), (2f, -32f) })
                Source(scrap, x, z, Matter.Metal, 9, 40, new[] { ItemId.MachineGun }, scrapT);
            foreach (var (x, z) in new[] { (-7f, -36f), (9f, -6f), (-10f, 12f), (16f, 40f), (-30f, 50f), (34f, 44f) })
                Source(rubble, x, z, Matter.Mineral, 6, 40, new ItemId[0], null);
            foreach (var (x, z, yaw) in new[] { (-20f, -30f, 30f), (24f, -2f, -50f), (-36f, 36f, 80f) })
            {
                var ribs = Place(Glb("World/ribs"), root, x, z, yaw, 1, collider: "none", sink: 0.2f);
                var ms = ribs.AddComponent<MatterSource>();
                ms.kind = Matter.Organic;
                ms.amount = 10;
                ms.regrow = 45;
                ms.reach = 4;
            }
            // more for the boss fight, on the island
            foreach (var (x, z) in new[] { (-9f, 82f), (9f, 82f), (-6f, 96f), (7f, 95f) }) Source(scrap, x, z, Matter.Metal, 6, 20, new ItemId[0], null);
            foreach (var (x, z) in new[] { (0f, 75f), (-12f, 90f), (12f, 89f) }) Source(rubble, x, z, Matter.Mineral, 4, 22, new ItemId[0], null);

            // the fire vents
            dir.vents.Add(VentAt(root, -40, 22, "west", new[] { lib.imp, lib.imp, lib.burrower }));
            dir.vents.Add(VentAt(root, 42, 30, "east", new[] { lib.imp, lib.burrower, lib.brute }));
            dir.vents.Add(VentAt(root, -14, 48, "north", new[] { lib.imp, lib.brute, lib.burrower, lib.imp }));

            // the Colossus stands in the lava north of the island
            var c = (GameObject)PrefabUtility.InstantiatePrefab(lib.colossus);
            var cRoot = new GameObject("Colossus");
            cRoot.transform.position = new Vector3(LakeIsland.x, H(LakeIsland.x, LakeIsland.z + 24), LakeIsland.z + 24);
            cRoot.transform.rotation = Quaternion.Euler(0, 180, 0);
            c.transform.SetParent(cRoot.transform, false);
            var capsule = cRoot.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0, 7, 0);
            capsule.height = 10;
            capsule.radius = 1.5f;   // the chest core (0.75 m) stands out in front of it
            var colossus = cRoot.AddComponent<Colossus>();
            colossus.model = c.transform;
            var arena = Group("Arena", root);
            arena.position = new Vector3(LakeIsland.x, H(LakeIsland.x, LakeIsland.z), LakeIsland.z);
            colossus.arena = arena;
            colossus.glow = AddLight(cRoot.transform, "CoreGlow", cRoot.transform.position + new Vector3(0, 8, -3), new Color(1f, 0.55f, 0.2f), 3, 25);
            dir.colossus = colossus;
            var exit = RiftAt(root, 0, LakeIsland.z + 9, 180, "wrath_exit");
            exit.leadsTo = "Menu";
            exit.delay = 6;
            exit.membrane.sharedMaterial = Mat("RiftGold", "Crab/Rift", m => { m.SetColor("_Color", new Color(1f, 0.8f, 0.3f)); m.SetColor("_Core", new Color(0.1f, 0.05f, 0)); });
            exit.glow.color = new Color(1f, 0.8f, 0.35f);
            dir.exit = exit;
            Ui<GameUI>();
            SaveScene(scene, "Wrath");
        }

        /// <summary>A slow column of dark smoke over a volcano.</summary>
        static void Smoke(Transform parent, Vector3 at, float scale)
        {
            var go = new GameObject("Smoke");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(at, Quaternion.Euler(-90, 0, 0));
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = 40;
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f * scale, 5f * scale);
            main.startSize = new ParticleSystem.MinMaxCurve(14 * scale, 24 * scale);
            main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.16f, 0.1f, 0.08f, 0.7f), new Color(0.3f, 0.2f, 0.16f, 0.5f));
            main.maxParticles = 120;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission;
            em.rateOverTime = 2.5f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 8;
            shape.radius = 5 * scale;
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(2.5f, 4f);
            vel.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            vel.z = new ParticleSystem.MinMaxCurve(0.5f, 1f);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 0.6f, 1, 2.5f));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(0.9f, 0.5f, 0.3f), 0), new GradientColorKey(Color.white, 0.15f), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.1f), new GradientAlphaKey(0.6f, 0.6f), new GradientAlphaKey(0, 1) });
            col.color = g;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = lib.particle;
        }

        static Rift VentAt(Transform parent, float x, float z, string id, GameObject[] pool)
        {
            var go = Place(Glb("World/vent"), parent, x, z, R(0, 360), 0.85f);
            var v = go.AddComponent<Rift>();
            v.id = id;
            v.reach = 3.6f;
            v.pool = pool;
            v.sealWord = "Заглушить огненное жерло";
            v.tint = new Color(1f, 0.55f, 0.2f);
            v.spawnAhead = 0;
            v.pointHeight = 0.8f;
            v.budget = 9;
            v.maxAlive = 3;
            v.spawnEvery = 5.5f;
            v.wakeRange = 24;
            v.glow = AddLight(go.transform, "Glow", go.transform.position + Vector3.up * 1.5f, new Color(1f, 0.5f, 0.18f), 2.5f, 14);
            v.fire = Fire(go.transform);
            return v;
        }

        /// <summary>A column of fire and sparks going up from a vent.</summary>
        static ParticleSystem Fire(Transform parent)
        {
            var go = new GameObject("Fire");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0, 0.2f, 0);
            go.transform.rotation = Quaternion.Euler(-90, 0, 0);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 1.1f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.6f, 0.2f, 0.9f), new Color(1f, 0.3f, 0.08f, 0.7f));
            main.maxParticles = 400;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.15f;
            var em = ps.emission;
            em.rateOverTime = 45;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12;
            shape.radius = 0.7f;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0, 1, 1, 0.1f));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.85f, 0.4f), 0), new GradientColorKey(new Color(1f, 0.35f, 0.08f), 0.5f), new GradientColorKey(new Color(0.3f, 0.1f, 0.05f), 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.1f), new GradientAlphaKey(0.6f, 0.6f), new GradientAlphaKey(0, 1) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = lib.particleAdditive;
            return ps;
        }

        // ------------------------------------------------------------------ menu
        static void BuildMenu()
        {
            var scene = NewScene("Menu");
            ground = BeachHeight;
            var (sand, sea) = BeachMaterials();
            var root = Group("Beach");
            Ground("MenuGround", new Rect(-40, -30, 80, 70), sand, 0.25f).transform.SetParent(root, false);
            Sea(sea, -0.25f, 300).transform.SetParent(root, false);
            Environment(new Color(0.62f, 0.5f, 0.45f), new Color(0.5f, 0.42f, 0.35f), 1.1f, new Color(0.62f, 0.6f, 0.65f), new Color(0.7f, 0.58f, 0.5f), new Color(0.3f, 0.24f, 0.2f), new Color(0.8f, 0.68f, 0.6f), 0.006f, 0.06f);
            Sun(new Color(1f, 0.8f, 0.6f), 1.3f, new Vector3(16, -20, 0));
            Post("Menu", 0.1f, 5, Color.white, 0.9f, 0.3f);
            BeachScenery(root, false);
            var crab = (GameObject)PrefabUtility.InstantiatePrefab(lib.hero);
            crab.transform.position = new Vector3(0, H(0, 2), 2);
            crab.transform.rotation = Quaternion.Euler(0, 200, 0);
            crab.AddComponent<CrabRig>();
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
            camGo.transform.position = crab.transform.position + new Vector3(2, 1.2f, -4);
            camGo.transform.LookAt(crab.transform.position);
            var go = new GameObject("UI");
            go.AddComponent<UIDocument>().panelSettings = Panel();
            var menu = go.AddComponent<MainMenu>();
            menu.cam = cam;
            menu.orbitCenter = crab.transform;
            Setup("Menu", crab.transform);
            SaveScene(scene, "Menu");
        }
    }
}
