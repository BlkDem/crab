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
            ModelUnpack.All();
            ConfigureProject();
            lib = BuildLibrary();
            BuildPrologue();
            BuildLimbo();
            BuildWrath();
            BuildGreed();
            BuildEnvy();
            BuildGloom();
            BuildBetrayal();
            BuildViolence();
            BuildLies();
            BuildPower();
            BuildEpilogue();
            BuildMenu();
            EditorBuildSettings.scenes = new[] { "Menu", "Prologue", "Limbo", "Wrath", "Greed", "Envy", "Gloom", "Betrayal", "Violence", "Lies", "Power", "Epilogue" }.Select(n => new EditorBuildSettingsScene($"{SceneDir}/{n}.unity", true)).ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("Crab scenes built");
        }

        /// <summary>Only one scene, after its .blend was exported (Tools/blender/export_level.py).</summary>
        static void BuildOnly(string name, Action build)
        {
            Directory.CreateDirectory(SceneDir);
            ModelUnpack.All();
            lib = BuildLibrary();
            build();
            AssetDatabase.SaveAssets();
            Debug.Log(name + " built");
        }

        [MenuItem("Crab/Build Scene/Prologue")]
        public static void BuildPrologueOnly() => BuildOnly("Prologue", BuildPrologue);

        [MenuItem("Crab/Build Scene/Limbo")]
        public static void BuildLimboOnly() => BuildOnly("Limbo", BuildLimbo);

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
                // the SRP Batcher draws some models with other models' textures in the circle of Envy
                // (hundreds of flowers and trees, many baked materials) until they are re-enabled;
                // the plain path is right, and fast enough here
                Set("m_UseSRPBatcher", p => p.boolValue = false);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            // the scenes carry many textures: upload them faster after a load (the default slice leaves
            // garbage in the top mips for seconds in the bigger circles)
            var qso = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
            var levels = qso.FindProperty("m_QualitySettings");
            for (int i = 0; levels != null && i < levels.arraySize; i++)
            {
                var q = levels.GetArrayElementAtIndex(i);
                q.FindPropertyRelative("asyncUploadTimeSlice").intValue = 8;
                q.FindPropertyRelative("asyncUploadBufferSize").intValue = 64;
            }
            qso.ApplyModifiedPropertiesWithoutUndo();
            PlayerSettings.productName = "Краб против ада";
            PlayerSettings.companyName = "umolab";
        }

        /// <summary>A saved copy of a model's material with its colour raised (glTFast's baseColorFactor).</summary>
        static Material Brighter(Material src, string name, float k)
        {
            string path = $"{Gen}/{name}.mat";
            AssetDatabase.DeleteAsset(path);
            var m = new Material(src) { name = name };
            var c = src.GetColor("baseColorFactor");
            m.SetColor("baseColorFactor", new Color(c.r * k, c.g * k, c.b * k, c.a));
            AssetDatabase.CreateAsset(m, path);
            return m;
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

        // the unpacked models (ModelUnpack): their pictures are compressed textures
        static GameObject Glb(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(ModelUnpack.PathOf(path));

        static Library BuildLibrary()
        {
            string path = $"{Gen}/Library.asset";
            var l = AssetDatabase.LoadAssetAtPath<Library>(path);
            if (!l) { l = ScriptableObject.CreateInstance<Library>(); AssetDatabase.CreateAsset(l, path); }
            l.hero = Glb("Hero/crab");
            l.heroForms = new GameObject[7];
            for (int lv = 2; lv <= 6; lv++) l.heroForms[lv] = Glb($"Hero/form{lv}");
            l.items = new List<Library.Named>();
            foreach (var (id, file) in new[] { (ItemId.Stick, "stick"), (ItemId.Stone, "stone"), (ItemId.Crowbar, "crowbar"), (ItemId.Knife, "knife"), (ItemId.Pistol, "pistol"), (ItemId.MachineGun, "machinegun"), (ItemId.EnergyRifle, "energy_rifle"), (ItemId.EnergyBlade, "energy_blade"), (ItemId.GravityGun, "gravity_gun") })
                l.items.Add(new Library.Named { name = id.ToString(), model = Glb("Items/" + file) });
            l.stone = Glb("Items/stone");
            l.gull = Glb("World/gull");
            l.morsel = Glb("World/morsel");
            l.guardian = Glb("Enemies/guardian");
            l.colossus = Glb("Enemies/colossus");
            l.armor = Glb("Items/armor");
            l.drone = Glb("Items/drone");
            l.heavyArmor = Glb("Items/heavy_armor");
            l.king = Glb("Enemies/usurer");
            l.sparkCore = Glb("Items/spark_core");
            l.dweller = Glb("Enemies/dweller");
            l.devourer = Glb("Enemies/devourer");
            l.cyberFrame = Glb("Items/cyber_frame");
            l.hunter = Glb("Items/hunter");
            l.mageOrb = Glb("Items/mage_orb");
            l.turret = Glb("Items/turret");
            l.janus = Glb("Enemies/janus");
            l.guide = Glb("Enemies/guide");
            l.warlord = Glb("Enemies/warlord");
            l.liar = Glb("Enemies/liar");
            l.hellLord = Glb("Enemies/hell_lord");
            l.matterLord = Glb("Items/matter_lord");
            l.particle = Mat("ParticleSoft", "Crab/Soft");
            l.particleAdditive = Mat("ParticleGlow", "Crab/Glow", m => m.SetFloat("_Round", 1));
            l.tracer = Mat("Tracer", "Crab/Glow", m => m.SetFloat("_Round", 0));
            l.beam = Mat("Beam", "Crab/Glow", m => { m.SetFloat("_Round", 0); m.SetFloat("_Rim", 1); m.SetColor("_Tint", new Color(0.7f, 1f, 0.85f, 0.85f)); });
            l.rift = Mat("Rift", "Crab/Rift");
            l.water = Mat("Sea", "Crab/Water");
            l.gold = Mat("Gold", "Universal Render Pipeline/Lit", m => { m.SetColor("_BaseColor", new Color(1f, 0.75f, 0.25f)); m.SetFloat("_Metallic", 0.6f); m.SetFloat("_Smoothness", 0.7f); });
            l.shield = Mat("ShieldDome", "Crab/Glow", m => { m.SetFloat("_Round", 0); m.SetFloat("_Rim", 1); m.SetColor("_Tint", new Color(0.45f, 0.9f, 1f, 0.6f)); });
            l.mirror = Mat("MirrorGlass", "Universal Render Pipeline/Lit", m =>
            {
                m.SetColor("_BaseColor", new Color(0.5f, 0.48f, 0.62f));
                m.SetFloat("_Metallic", 0.9f);
                m.SetFloat("_Smoothness", 0.95f);
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", new Color(0.12f, 0.08f, 0.2f));
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                m.SetFloat("_Cull", 0);
            });
            l.fire = Mat("Fire", "Crab/Glow", m => { m.SetFloat("_Round", 0); m.SetFloat("_Rim", 1); m.SetColor("_Tint", new Color(1f, 0.55f, 0.15f, 1f)); });
            l.shade = EnemyPrefab<Shade>("Shade", Glb("Enemies/shade"), 0.4f, 2.4f, e => { e.maxHealth = 40; e.speed = 1.8f; e.attackRange = 1.7f; e.damage = 9; e.windup = 0.7f; e.attackCooldown = 1.5f; });
            l.crawler = EnemyPrefab<Crawler>("Crawler", Glb("Enemies/crawler"), 0.45f, 0.8f, e => { e.maxHealth = 28; e.speed = 3.6f; e.attackRange = 1.1f; e.damage = 8; e.windup = 0.35f; e.attackCooldown = 1.2f; });
            l.imp = EnemyPrefab<Imp>("Imp", Glb("Enemies/imp"), 0.35f, 1.3f, e => { e.maxHealth = 45; e.speed = 3.4f; e.attackRange = 1.4f; e.damage = 11; e.windup = 0.4f; e.attackCooldown = 1.1f; e.aggro = 16; });
            l.brute = EnemyPrefab<Brute>("Brute", Glb("Enemies/brute"), 0.8f, 3.0f, e => { e.maxHealth = 220; e.speed = 1.7f; e.attackRange = 2.6f; e.damage = 26; e.windup = 1.0f; e.attackCooldown = 2.4f; e.aggro = 14; });
            l.burrower = EnemyPrefab<Burrower>("Burrower", Glb("Enemies/burrower"), 0.5f, 0.9f, e => { e.maxHealth = 70; e.speed = 1.6f; e.attackRange = 1.6f; e.damage = 14; e.windup = 0.5f; e.attackCooldown = 1.6f; e.aggro = 15; });
            l.collector = EnemyPrefab<Collector>("Collector", Glb("Enemies/collector"), 0.35f, 1.2f, e => { e.maxHealth = 50; e.speed = 3.0f; e.aggro = 12; e.attackRange = 0; e.damage = 0; });
            l.goldGuard = EnemyPrefab<GoldGuard>("GoldGuard", Glb("Enemies/goldguard"), 0.5f, 2.2f, e => { e.maxHealth = 130; e.speed = 2.1f; e.attackRange = 2.2f; e.damage = 18; e.windup = 0.8f; e.attackCooldown = 1.8f; e.aggro = 13; });
            l.tank = TankPrefab(Glb("Items/tank"));
            l.legionary = EnemyPrefab<GoldGuard>("Legionary", Glb("Enemies/legionary"), 0.45f, 2.0f, e => { e.maxHealth = 90; e.speed = 2.4f; e.attackRange = 2.0f; e.damage = 13; e.windup = 0.6f; e.attackCooldown = 1.5f; e.aggro = 16; });
            l.engineer = CompanionPrefab<EngineerBot>("Engineer", Glb("Items/engineer"), 0.3f, 0.9f);
            l.mimic = EnemyPrefab<Mimic>("Mimic", Glb("Enemies/burrower"), 0.5f, 0.9f, e => { e.maxHealth = 90; e.speed = 3.2f; e.attackRange = 1.6f; e.damage = 16; e.windup = 0.4f; e.attackCooldown = 1.3f; e.aggro = 14; });
            l.battleBot = CompanionPrefab<BattleBot>("BattleBot", Glb("Items/battlebot"), 0.3f, 1.1f);
            l.wraith = EnemyPrefab<Wraith>("Wraith", Glb("Enemies/wraith"), 0.4f, 1.8f, e => { e.maxHealth = 60; e.speed = 2.2f; e.attackRange = 1.6f; e.damage = 6; e.windup = 0.6f; e.attackCooldown = 1.8f; e.aggro = 11; });
            l.envious = EnemyPrefab<Envious>("Envious", Glb("Enemies/envious"), 0.35f, 1.6f, e => { e.maxHealth = 80; e.speed = 3.0f; e.attackRange = 1.5f; e.damage = 14; e.windup = 0.45f; e.attackCooldown = 1.3f; e.aggro = 18; });
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

        static GameObject CompanionPrefab<T>(string name, GameObject model, float radius, float height) where T : MonoBehaviour
        {
            var go = new GameObject(name);
            var cc = go.AddComponent<CharacterController>();
            cc.radius = radius;
            cc.height = height;
            cc.center = new Vector3(0, height / 2 + 0.05f, 0);
            cc.stepOffset = 0.25f;
            var m = (GameObject)PrefabUtility.InstantiatePrefab(model, go.transform);
            m.name = "Model";
            go.AddComponent<T>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"Assets/Crab/Prefabs/{name}.prefab");
            UnityEngine.Object.DestroyImmediate(go);
            return prefab;
        }

        static GameObject TankPrefab(GameObject model)
        {
            var go = new GameObject("Tank");
            var cc = go.AddComponent<CharacterController>();
            cc.radius = 0.45f;
            cc.height = 1.3f;
            cc.center = new Vector3(0, 0.7f, 0);
            cc.stepOffset = 0.3f;
            var m = (GameObject)PrefabUtility.InstantiatePrefab(model, go.transform);
            m.name = "Model";
            go.AddComponent<TankBot>().model = m.transform;
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, "Assets/Crab/Prefabs/Tank.prefab");
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
            foreach (var c in go.transform.Cast<Transform>().ToArray())
                if (c.name != child) UnityEngine.Object.DestroyImmediate(c.gameObject);
            if (collider == "mesh")
                foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
                    if (!mf.name.StartsWith("Glow")) mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
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

        // ------------------------------------------------------------------ levels made in Blender
        /// <summary>One thing of a level's layout (Tools/blender/level.py): where it stands, what it shows,
        /// its role in the game and the role's properties.</summary>
        sealed class Spot
        {
            public Newtonsoft.Json.Linq.JObject data;
            public GameObject go;
            public string Role => (string)data["role"] ?? "";
            public string Str(string k, string d = "") => (string)data[k] ?? d;
            public float Num(string k, float d = 0) => data[k] != null ? (float)data[k] : d;
            public Vector3 Pos => new Vector3((float)data["pos"][0], (float)data["pos"][1], (float)data["pos"][2]);
            public float Yaw => (float)data["yaw"];
        }

        /// <summary>A level from its Blender layout (Levels/&lt;name&gt;.blend → Models/Levels/&lt;Name&gt;): the land
        /// (the height then is the land's own, by a ray), the props, the sun. Returns the game's places
        /// to be given their roles.</summary>
        static List<Spot> FromBlender(string level, Transform root, out Light sun)
        {
            var json = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText($"{Models}/Levels/{level}/layout.json"));
            var land = (GameObject)PrefabUtility.InstantiatePrefab(Glb((string)json["ground"]), root);
            land.name = "Ground";
            foreach (var mf in land.GetComponentsInChildren<MeshFilter>())
            {
                mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                mf.gameObject.isStatic = true;
                mf.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            Physics.SyncTransforms();
            ground = (x, z) => Physics.Raycast(new Vector3(x, 500, z), Vector3.down, out var hit, 1000) ? hit.point.y : 0;
            var s = json["sun"];
            sun = null;
            if (s != null && s.Type != Newtonsoft.Json.Linq.JTokenType.Null)
            {
                var d = new Vector3((float)s["dir"][0], (float)s["dir"][1], (float)s["dir"][2]);
                sun = Sun(new Color((float)s["color"][0], (float)s["color"][1], (float)s["color"][2]), (float)s["intensity"], Quaternion.LookRotation(d).eulerAngles);
            }
            var props = Group("Scenery", root);
            var spots = new List<Spot>();
            foreach (Newtonsoft.Json.Linq.JObject o in json["objects"])
            {
                var spot = new Spot { data = o };
                var asset = spot.Str("asset");
                if (asset.Length > 0)
                {
                    var parts = asset.Split('/');
                    var model = Glb(parts[0] + "/" + parts[1]);
                    var p = spot.Pos;
                    var col = spot.Str("collider", "mesh");
                    var parent = spot.Str("group") == "Props" ? props : root;
                    spot.go = parts.Length > 2
                        ? Variant(model, parts[2], parent, p.x, p.z, spot.Yaw, spot.Num("scale", 1), col)
                        : Place(model, parent, p.x, p.z, spot.Yaw, spot.Num("scale", 1), col, sink: spot.Role.Length > 0 ? 0 : 0.05f);
                    if (spot.Num("snap", 1) < 0.5f) spot.go.transform.position = p;
                    spot.go.name = spot.Str("name");
                }
                if (spot.Role.Length > 0) spots.Add(spot);
            }
            return spots;
        }

        // ------------------------------------------------------------------ prologue
        /// <summary>The beach is made in Blender (Levels/prologue.blend); the game's places get their roles here.</summary>
        static void BuildPrologue()
        {
            var scene = NewScene("Prologue");
            var (_, sea) = BeachMaterials();
            var root = Group("Beach");
            var spots = FromBlender("Prologue", root, out var sun);
            Environment(new Color(0.5f, 0.5f, 0.52f), new Color(0.6f, 0.55f, 0.45f), 1.25f, new Color(0.55f, 0.65f, 0.8f), new Color(0.7f, 0.65f, 0.55f), new Color(0.35f, 0.3f, 0.25f), new Color(0.7f, 0.8f, 0.9f), 0.004f);
            Post("Prologue", 0.1f, 12, Color.white, 0.6f, 0.2f);
            Spot One(string role) => spots.First(x => x.Role == role);

            var seaSpot = One("sea");
            Sea(sea, seaSpot.Pos.y, seaSpot.Num("size", 400)).transform.SetParent(root, false);
            var s0 = One("start");
            var start = Group("Start", root);
            start.position = new Vector3(s0.Pos.x, H(s0.Pos.x, s0.Pos.z), s0.Pos.z);
            start.rotation = Quaternion.Euler(0, s0.Yaw, 0);
            var hero = BuildHero(start.position, s0.Yaw, out var cam);
            Setup("Prologue", start);

            var dir = new GameObject("Prologue").AddComponent<PrologueDirector>();
            // food: the fish and all the weed
            foreach (var f in spots.Where(x => x.Role == "food"))
            {
                var e = f.go.AddComponent<Edible>();
                e.title = f.Str("title", "Еда");
                e.heal = f.Num("heal", 10);
                dir.morsels.Add(e);
            }
            // the shelter from the gull: a hollow under the slab
            var shelter = One("shelter").go;
            var zone = new GameObject("ShelterZone").AddComponent<BoxCollider>();
            zone.isTrigger = true;
            zone.transform.SetParent(shelter.transform, false);
            zone.center = new Vector3(0, 0.35f, 0);
            zone.size = new Vector3(2.6f, 0.75f, 2.0f);
            dir.shelter = zone;
            // the gull circles over its spot
            var gs = One("gull");
            var gull = (GameObject)PrefabUtility.InstantiatePrefab(lib.gull);
            gull.transform.position = new Vector3(gs.Pos.x, H(gs.Pos.x, gs.Pos.z) + gs.Num("height", 10), gs.Pos.z);
            gull.transform.localScale = Vector3.one * gs.Num("scale", 1.3f);
            var g = gull.AddComponent<Gull>();
            g.circleCenter = new Vector3(gs.Pos.x, H(gs.Pos.x, gs.Pos.z), gs.Pos.z);
            dir.gull = g;
            // the big wave: a long slab of water rolling up the beach from its spot
            var ws = One("wave");
            var waveGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            waveGo.name = "Wave";
            UnityEngine.Object.DestroyImmediate(waveGo.GetComponent<Collider>());
            waveGo.transform.localScale = new Vector3(ws.Num("width", 90), 0.9f, 4);
            waveGo.transform.position = ws.Pos;
            waveGo.GetComponent<Renderer>().sharedMaterial = sea;
            var wave = waveGo.AddComponent<Wave>();
            wave.startZ = ws.Pos.z;
            wave.reachZ = ws.Num("reach_z", 9);
            wave.speed = ws.Num("speed", 5.2f);
            waveGo.SetActive(false);
            dir.wave = wave;
            // the storm
            var storm = new GameObject("Storm").AddComponent<Storm>();
            storm.sun = sun;
            storm.rain = Rain(storm.transform);
            dir.storm = storm;
            // driftwood: the first matter the crab ever draws in
            foreach (var m in spots.Where(x => x.Role == "matter"))
            {
                var src = m.go.AddComponent<MatterSource>();
                src.kind = Matter.Organic;
                src.amount = (int)m.Num("amount", 5);
                if (m.Num("first") > 0.5f) dir.driftwood = src;
            }
            // the rift that opens at the end
            var rs = One("rift");
            var rift = MakeRift(rs.go, rs.Str("id"));
            rift.leadsTo = rs.Str("leads_to");
            rift.delay = rs.Num("delay", 0.5f);
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

        static Rift RiftAt(Transform parent, float x, float z, float yaw, string id) => MakeRift(Place(Glb("World/rift"), parent, x, z, yaw, 1.1f), id);

        /// <summary>A rift in a standing frame: the membrane, its light.</summary>
        static Rift MakeRift(GameObject frame, string id)
        {
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
        /// <summary>The first circle, made in Blender (Levels/limbo.blend): the grey valley of the souls before
        /// the Noble Castle. The fog is thick and low, the light cold and dim, the colour almost gone.</summary>
        static void BuildLimbo()
        {
            var scene = NewScene("Limbo");
            var sea = Mat("SeaLimbo", "Crab/Water", m =>
            {
                m.SetColor("_Shallow", new Color(0.22f, 0.25f, 0.25f));
                m.SetColor("_Deep", new Color(0.04f, 0.05f, 0.06f));
                m.SetColor("_Sky", new Color(0.4f, 0.43f, 0.44f));
                m.SetFloat("_WaveHeight", 0.18f);
            });
            var lake = Mat("LakeLimbo", "Crab/Water", m =>
            {
                m.SetColor("_Shallow", new Color(0.08f, 0.09f, 0.09f));
                m.SetColor("_Deep", new Color(0.01f, 0.012f, 0.015f));
                m.SetColor("_Sky", new Color(0.5f, 0.53f, 0.55f));
                m.SetFloat("_WaveHeight", 0.02f);
            });
            var root = Group("Limbo");
            var spots = FromBlender("Limbo", root, out var sun);
            if (sun) { sun.shadowStrength = 1; }
            Environment(new Color(0.3f, 0.32f, 0.33f), new Color(0.14f, 0.14f, 0.14f), 0.55f, new Color(0.34f, 0.37f, 0.39f), new Color(0.25f, 0.27f, 0.28f),
                new Color(0.1f, 0.1f, 0.1f), new Color(0.33f, 0.35f, 0.36f), 0.024f, 0.0f);
            // a world gone grey; the orange crab is the one warm thing in it
            Post("Limbo", 0.25f, -48, new Color(0.9f, 0.97f, 1f), 0.7f, 0.42f, 20);
            Mist(root, new Rect(-100, -80, 200, 240), new Color(0.62f, 0.65f, 0.66f, 0.16f), 420);
            Spot One(string role) => spots.First(x => x.Role == role);
            IEnumerable<Spot> All(string role) => spots.Where(x => x.Role == role);

            foreach (var w in All("sea").Concat(All("water")))
                Sea(w.Role == "sea" ? sea : lake, w.Pos.y, w.Num("size", 400)).transform.SetParent(root, false);
            var s0 = One("start");
            var start = Group("Start", root);
            start.position = new Vector3(s0.Pos.x, H(s0.Pos.x, s0.Pos.z), s0.Pos.z);
            start.rotation = Quaternion.Euler(0, s0.Yaw, 0);
            BuildHero(start.position, s0.Yaw, out _);
            Setup("Limbo", start);
            var dir = new GameObject("Limbo").AddComponent<LimboDirector>();
            foreach (var b in All("beacon")) dir.beacons.Add(MakeBeacon(b.go, b.Str("id")));
            foreach (var f in All("first_shade"))
            {
                var s = (GameObject)PrefabUtility.InstantiatePrefab(lib.shade);
                s.transform.SetPositionAndRotation(new Vector3(f.Pos.x, H(f.Pos.x, f.Pos.z), f.Pos.z), Quaternion.Euler(0, f.Yaw, 0));
                dir.firstShades.Add(s.GetComponent<Enemy>());
            }
            // matter: rubble teaches stones, scrap the crowbar and the knife; planks are organic
            const string rubbleT = "Камень. Из него можно сделать… просто камень. И метнуть.";
            const string scrapT = "Металл! Он тяжёлый и послушный. Лом. Нож. Я вижу, как их сложить.";
            foreach (var m in All("matter"))
            {
                var ms = m.go.AddComponent<MatterSource>();
                var kind = m.Str("kind");
                ms.kind = kind == "metal" ? Matter.Metal : kind == "mineral" ? Matter.Mineral : Matter.Organic;
                ms.amount = (int)m.Num("amount", 6);
                ms.regrow = m.Num("regrow");
                ms.reach = 2.6f;
                bool teach = ms.regrow <= 0;
                ms.teaches = !teach ? new List<ItemId>() : ms.kind == Matter.Mineral ? new List<ItemId> { ItemId.Stone } : ms.kind == Matter.Metal ? new List<ItemId> { ItemId.Crowbar, ItemId.Knife } : new List<ItemId>();
                ms.firstThought = !teach ? null : ms.kind == Matter.Mineral ? rubbleT : ms.kind == Matter.Metal ? scrapT : null;
            }
            foreach (var r in All("rift"))
            {
                var rift = MakeRift(r.go, r.Str("id"));
                rift.crawlerShare = r.Num("crawlers", 0.3f);
                rift.budget = (int)r.Num("budget", 8);
                rift.maxAlive = (int)r.Num("max_alive", 3);
                rift.spawnEvery = r.Num("every", 6);
                dir.rifts.Add(rift);
            }
            // the Guardian's ground before the gate
            var ar = One("arena");
            var square = Group("Square", root);
            square.position = new Vector3(ar.Pos.x, H(ar.Pos.x, ar.Pos.z), ar.Pos.z);
            var gs = One("guardian");
            var g = (GameObject)PrefabUtility.InstantiatePrefab(lib.guardian);
            var gRoot = new GameObject("Guardian");
            gRoot.transform.SetPositionAndRotation(new Vector3(gs.Pos.x, H(gs.Pos.x, gs.Pos.z), gs.Pos.z), Quaternion.Euler(0, gs.Yaw, 0));
            g.transform.SetParent(gRoot.transform, false);
            var capsule = gRoot.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0, 3, 0);
            capsule.height = 6.5f;
            capsule.radius = 0.8f;   // the heart (0.4 m) sticks out in front, so shots at it reach it
            var guardian = gRoot.AddComponent<Guardian>();
            guardian.model = g.transform;
            guardian.heartLight = AddLight(gRoot.transform, "HeartLight", gRoot.transform.position + gRoot.transform.rotation * new Vector3(0, 3.7f, 1.1f), new Color(0.5f, 1f, 0.7f), 1, 10);
            AddLight(gRoot.transform, "LanternLight", gRoot.transform.position + gRoot.transform.rotation * new Vector3(-1.6f, 2.6f, 1.2f), new Color(0.75f, 1f, 0.85f), 2, 9);
            dir.guardian = guardian;
            dir.square = square;
            var ex = One("exit");
            var exit = MakeRift(ex.go, ex.Str("id"));
            exit.leadsTo = ex.Str("leads_to");
            exit.delay = ex.Num("delay", 6);
            exit.membrane.sharedMaterial = Mat("RiftFire", "Crab/Rift", m => { m.SetColor("_Color", new Color(1f, 0.45f, 0.15f)); m.SetColor("_Core", new Color(0.08f, 0.01f, 0)); });
            exit.glow.color = new Color(1f, 0.5f, 0.2f);
            dir.exit = exit;
            // the processions: pale souls walking the paths, not seeing the crab — until it comes close
            var ghost = GhostMat();
            foreach (var path in All("procession").GroupBy(p => p.Str("path")))
            {
                var pts = path.OrderBy(p => p.Num("order")).Select(p => new Vector3(p.Pos.x, H(p.Pos.x, p.Pos.z), p.Pos.z)).ToArray();
                int n = (int)path.First().Num("count", 6);
                var line = Group("Procession" + path.Key, root);
                for (int i = 0; i < n; i++)
                {
                    var soul = (GameObject)PrefabUtility.InstantiatePrefab(Glb("Enemies/shade"), line);
                    soul.name = "Soul";
                    foreach (var rr in soul.GetComponentsInChildren<Renderer>())
                    {
                        rr.sharedMaterial = ghost;
                        rr.shadowCastingMode = ShadowCastingMode.Off;
                    }
                    soul.transform.localScale = Vector3.one * R(0.95f, 1.15f);
                    var pg = soul.AddComponent<Pilgrim>();
                    pg.path = pts;
                    pg.start = i * 2.2f;
                    pg.speed = R(0.45f, 0.6f);
                }
            }
            Ui<GameUI>();
            SaveScene(scene, "Limbo");
        }

        /// <summary>A pale, see-through stuff for the souls of Limbo.</summary>
        static Material GhostMat() => Mat("Ghost", "Universal Render Pipeline/Unlit", m =>
        {
            m.SetFloat("_Surface", 1);
            m.SetFloat("_Blend", 0);
            m.SetFloat("_ZWrite", 0);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetColor("_BaseColor", new Color(0.66f, 0.7f, 0.72f, 0.14f));
        });

        /// <summary>Mist lying low over the land: big soft motes drifting slowly.</summary>
        static void Mist(Transform parent, Rect area, Color color, int count)
        {
            var go = new GameObject("Mist");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(area.center.x, 1.2f, area.center.y);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = 40;
            main.startSpeed = 0.25f;
            main.startSize = new ParticleSystem.MinMaxCurve(9, 18);
            main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            main.startColor = color;
            main.maxParticles = count;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission;
            em.rateOverTime = count / 40f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(area.width, 1.5f, area.height);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var gr = new Gradient();
            gr.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.3f), new GradientAlphaKey(1, 0.7f), new GradientAlphaKey(0, 1) });
            col.color = gr;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = lib.particle;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.sortingFudge = 10;
        }

        static Beacon BeaconAt(Transform parent, float x, float z, float yaw, string id) => MakeBeacon(Place(Glb("World/beacon"), parent, x, z, yaw, 1), id);

        /// <summary>A beacon (a checkpoint) in its standing fire-bowl.</summary>
        static Beacon MakeBeacon(GameObject go, string id)
        {
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
            exit.leadsTo = "Greed";
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

        // ------------------------------------------------------------------ Greed
        // the golden city stands on a dark plain: walls around x -40..40, z -30..50, the gate in
        // the south, the vault door in the north wall, the Usurer's hall beyond it
        const float CityW = 40, CityS = -30, CityN = 50;

        static float GreedHeight(float x, float z)
        {
            float h = (Mathf.PerlinNoise(x * 0.025f + 31, z * 0.025f + 17) - 0.5f) * 4f;
            h += (Mathf.PerlinNoise(x * 0.1f + 3, z * 0.1f + 9) - 0.5f) * 0.5f;
            // inside the walls and the hall the ground is levelled
            float inside = Mathf.Clamp01(1 - Mathf.Max(Mathf.Abs(x) - CityW, Mathf.Max(CityS - z, z - 98)) / 12f);
            float road = Mathf.Clamp01(1 - (Mathf.Abs(x) - 4) / 6f) * Mathf.Clamp01((CityS + 2 - z) / 2f) * Mathf.Clamp01((z + 90) / 10f);
            return Mathf.Lerp(h, 0, Mathf.Max(inside, road * 0.8f));
        }

        static void BuildGreed()
        {
            var scene = NewScene("Greed");
            ground = GreedHeight;
            var (gc, gn) = TextureGen.Gloom();
            var gloom = Mat("Gloom", "Universal Render Pipeline/Lit", m =>
            {
                m.SetTexture("_BaseMap", gc);
                m.SetTexture("_BumpMap", gn);
                m.EnableKeyword("_NORMALMAP");
                m.SetFloat("_Smoothness", 0.2f);
            });
            var (pc, pn) = TextureGen.Paving();
            var paving = Mat("Paving", "Universal Render Pipeline/Lit", m =>
            {
                m.SetTexture("_BaseMap", pc);
                m.SetTexture("_BumpMap", pn);
                m.EnableKeyword("_NORMALMAP");
                m.SetFloat("_Smoothness", 0.45f);
                m.SetFloat("_Metallic", 0.15f);
            });
            var root = Group("Greed");
            Ground("GreedGround", new Rect(-120, -120, 240, 260), gloom, 0.15f).transform.SetParent(root, false);
            // the paved floors of the city and the hall, a hair above the plain
            var plain = ground;
            ground = (x, z) => plain(x, z) + 0.03f;
            Ground("CityFloor", new Rect(-CityW, CityS, CityW * 2, CityN - CityS + 1), paving, 0.125f).transform.SetParent(root, false);
            Ground("HallFloor", new Rect(-17, CityN, 34, 48), paving, 0.125f).transform.SetParent(root, false);
            Ground("Road", new Rect(-4, -95, 8, 66), paving, 0.125f).transform.SetParent(root, false);
            ground = plain;

            var sun = Sun(new Color(1f, 0.8f, 0.55f), 1.5f, new Vector3(24, 200, 0));
            Environment(new Color(0.5f, 0.5f, 0.5f), new Color(0.1f, 0.07f, 0.08f), 1, new Color(0.5f, 0.46f, 0.66f), new Color(0.55f, 0.44f, 0.38f), new Color(0.22f, 0.16f, 0.12f), new Color(0.3f, 0.23f, 0.3f), 0.0065f, 0.04f);
            RenderSettings.skybox = Mat("Sky_Greed_Gradient", "Crab/Sky", m =>
            {
                m.SetColor("_Top", new Color(0.05f, 0.04f, 0.13f));
                m.SetColor("_Horizon", new Color(0.6f, 0.36f, 0.32f));
                m.SetColor("_Bottom", new Color(0.1f, 0.07f, 0.09f));
                m.SetColor("_Sun", new Color(1f, 0.75f, 0.4f));
                m.SetVector("_SunDir", -sun.transform.forward);
                m.SetFloat("_Exponent", 0.45f);
            });
            Post("Greed", 0.3f, 10, new Color(1f, 0.96f, 0.9f), 1.0f, 0.3f, 12);

            // the walls, towers and two gates
            var city = Group("City", root);
            var gw = Glb("World/gold_wall");
            var gt = Glb("World/gold_tower");
            for (float x = -CityW + 5; x < CityW; x += 10)
            {
                if (Mathf.Abs(x) > 6) Place(gw, city, x, CityS, 0, 1, sink: 0.1f);
                if (Mathf.Abs(x) > 6) Place(gw, city, x, CityN, 180, 1, sink: 0.1f);
            }
            for (float z = CityS + 5; z < CityN; z += 10)
            {
                Place(gw, city, -CityW, z, 90, 1, sink: 0.1f);
                Place(gw, city, CityW, z, -90, 1, sink: 0.1f);
            }
            foreach (var x in new[] { -CityW, -20f, 20f, CityW })
                foreach (var z in new[] { CityS, CityN })
                    Place(gt, city, x, z, R(0, 360), 1, sink: 0.1f);
            Place(gt, city, -CityW, 10, 0, 1, sink: 0.1f);
            Place(gt, city, CityW, 10, 0, 1, sink: 0.1f);
            var gateGo = Place(Glb("World/gold_gate"), city, 0, CityS, 0, 1, sink: 0.05f);
            foreach (var (n, a) in new[] { ("DoorL", 100f), ("DoorR", -100f) })
            {
                var d = CrabRig.Find(gateGo.transform, n);
                if (d) d.localRotation = Quaternion.Euler(0, a, 0);
            }

            // the hall of the Usurer beyond the north wall
            var hall = Group("Hall", root);
            for (float z = CityN + 6; z < 98; z += 10)
            {
                Place(gw, hall, -17, z, 90, 1, sink: 0.1f);
                Place(gw, hall, 17, z, -90, 1, sink: 0.1f);
            }
            foreach (var x in new[] { -12f, -2f, 8f }) Place(gw, hall, x + 2, 99, 180, 1, sink: 0.1f);
            Place(gt, hall, -17, 99, 0, 1, sink: 0.1f);
            Place(gt, hall, 17, 99, 0, 1, sink: 0.1f);
            var gcol = Glb("World/gold_column");
            foreach (var x in new[] { -9f, 9f })
                foreach (var z in new[] { 58f, 66f, 74f, 82f })
                    Place(gcol, hall, x, z, 0, 1);
            Place(Glb("World/scales"), hall, -13, 92, 30, 1);
            Place(Glb("World/scales"), hall, 13, 92, -30, 1);
            var throne = Place(Glb("World/throne"), hall, 0, 92, 180, 1, sink: 0);

            // the houses of the rich, the monuments
            var gh = Glb("World/gold_house");
            foreach (var (x, z, yaw) in new[] { (-13f, -18f, 90f), (13f, -18f, -90f), (-26f, -18f, 90f), (26f, -18f, -90f), (-13f, 30f, 90f), (13f, 30f, -90f),
                         (-27f, 30f, 0f), (27f, 30f, 0f), (-31f, 0f, 90f), (31f, 0f, -90f), (14f, 0f, -90f), (-14f, 40f, 180f), (14f, 41f, 180f), (28f, 42f, 180f) })
                Place(gh, city, x, z, yaw + R(-3, 3), R(0.95f, 1.1f), sink: 0.1f);
            foreach (var (x, z) in new[] { (-6f, 13f), (6f, 13f), (0f, -40f) }) Place(Glb("World/obelisk"), city, x, z, 0, 1, collider: "box");

            // start, beacons, director
            var start = Group("Start", root);
            start.position = new Vector3(0, H(0, -80), -80);
            var hero = BuildHero(start.position, 0, out _);
            Setup("Greed", start);
            var dir = new GameObject("Greed").AddComponent<GreedDirector>();
            dir.beacons.Add(BeaconAt(root, 5, -77, -30, "greed_road"));
            dir.beacons.Add(BeaconAt(root, 5, -24, 180, "greed_gate"));
            dir.beacons.Add(BeaconAt(root, -4, 20, 180, "greed_plaza"));
            dir.beacons.Add(BeaconAt(root, 5, 44, 180, "greed_vault"));
            var gate = Group("GateIn", root);
            gate.position = new Vector3(0, 0, CityS + 2);
            dir.gate = gate;

            // matter: gold, crystals, energy — and the plain old metal and stone
            void Source(GameObject model, string variant, float x, float z, Matter kind, int amount, float regrow, float scale = 1, float reach = 2.6f)
            {
                var go = variant != null ? Variant(model, variant, root, x, z, R(0, 360), scale, collider: "none") : Place(model, root, x, z, R(0, 360), scale, collider: "none", sink: 0.02f);
                var ms = go.AddComponent<MatterSource>();
                ms.kind = kind;
                ms.amount = amount;
                ms.regrow = regrow;
                ms.reach = reach;
            }
            var pile = Glb("World/gold_pile");
            var crys = Glb("World/crystal");
            var pyl = Glb("World/pylon");
            foreach (var (x, z) in new[] { (-8f, -12f), (9f, -6f), (-20f, 18f), (22f, 12f), (-8f, 34f), (20f, 36f), (-34f, 40f) }) Source(pile, $"Pile{rnd.Next(2)}", x, z, Matter.Gold, 6, 70);
            foreach (var (x, z) in new[] { (-18f, -60f), (22f, -52f), (-34f, -10f), (30f, -24f), (-30f, 20f), (33f, 22f) }) Source(crys, $"Crystal{rnd.Next(3)}", x, z, Matter.Crystal, 5, 70, 1.2f);
            foreach (var (x, z) in new[] { (-8f, 4f), (8f, 22f), (-22f, -6f), (24f, -10f), (0f, 44f) })
            {
                Source(pyl, null, x, z, Matter.Energy, 5, 50);
                AddLight(root, "PylonLight", new Vector3(x, H(x, z) + 3, z), new Color(0.7f, 0.5f, 1f), 2, 9);
            }
            var scrap = Glb("World/scrap");
            var rubble = Glb("World/rubble");
            foreach (var (x, z) in new[] { (-7f, -70f), (8f, -58f), (-14f, -40f), (16f, -38f), (-18f, 6f), (-24f, 14f), (10f, 6f), (-4f, 26f) })
                Source(scrap, null, x, z, Matter.Metal, 8, 50);
            foreach (var (x, z) in new[] { (6f, -66f), (-10f, -50f), (-16f, 30f), (18f, -14f) }) Source(rubble, null, x, z, Matter.Mineral, 6, 50);

            // the workshop's ruin on the west square
            var wsGo = Place(Glb("World/workshop"), root, -22, 4, 90, 1, collider: "none", sink: 0);
            var ws = wsGo.AddComponent<Workshop>();
            ws.build = CrabRig.Find(wsGo.transform, "Build");
            if (ws.build)
                foreach (var mf in ws.build.GetComponentsInChildren<MeshFilter>())
                    if (!mf.name.StartsWith("Glow")) mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
            ws.reach = 4.5f;
            ws.forgeLight = AddLight(wsGo.transform, "Forge", wsGo.transform.position + Vector3.up * 1.2f + wsGo.transform.forward * 1.2f, new Color(1f, 0.55f, 0.2f), 3, 10);
            dir.workshop = ws;

            // the demons: collectors with sacks out on the plain and in the streets, guards of gold
            foreach (var (x, z) in new[] { (-6f, -55f), (6f, -50f), (12f, -44f) })
            {
                var c = (GameObject)PrefabUtility.InstantiatePrefab(lib.collector);
                c.transform.position = new Vector3(x, H(x, z), z);
                dir.firstCollectors.Add(c.GetComponent<Enemy>());
            }
            foreach (var (x, z) in new[] { (-20f, -14f), (18f, 26f), (-2f, 38f), (26f, 4f) })
            {
                var c = (GameObject)PrefabUtility.InstantiatePrefab(lib.collector);
                c.transform.position = new Vector3(x, H(x, z), z);
            }
            foreach (var (x, z, yaw) in new[] { (0f, 22f, 180f), (-18f, 32f, 120f), (18f, 32f, 240f), (-5f, 45f, 180f), (5f, 45f, 180f), (16f, -2f, 270f) })
            {
                var g = (GameObject)PrefabUtility.InstantiatePrefab(lib.goldGuard);
                g.transform.SetPositionAndRotation(new Vector3(x, H(x, z), z), Quaternion.Euler(0, yaw, 0));
            }

            // the vault door in the north wall
            var vaultGo = Place(Glb("World/gold_gate"), city, 0, CityN, 0, 1, sink: 0.05f);
            var vault = vaultGo.AddComponent<VaultDoor>();
            vault.doorL = CrabRig.Find(vaultGo.transform, "DoorL");
            vault.doorR = CrabRig.Find(vaultGo.transform, "DoorR");
            vault.price = 15;
            vault.reach = 5;
            dir.vault = vault;

            // the Usurer on his throne, the golden dome, his three hoards
            var u = (GameObject)PrefabUtility.InstantiatePrefab(lib.king);
            var uRoot = new GameObject("Usurer");
            uRoot.transform.SetPositionAndRotation(throne.transform.position + Vector3.up * 1.5f + Vector3.forward * 2f, Quaternion.Euler(0, 180, 0));
            u.transform.SetParent(uRoot.transform, false);
            var cap = uRoot.AddComponent<CapsuleCollider>();
            cap.center = new Vector3(0, 3.5f, 0);
            cap.height = 7;
            cap.radius = 1.5f;   // the gem stands out in front of it
            var usurer = uRoot.AddComponent<Usurer>();
            usurer.model = u.transform;
            var arena = Group("Arena", root);
            arena.position = new Vector3(0, H(0, 72), 72);
            usurer.arena = arena;
            var domeRoot = new GameObject("Dome").transform;
            domeRoot.SetParent(uRoot.transform, false);
            domeRoot.localPosition = new Vector3(0, 4, 1);
            var domeBall = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            domeBall.name = "DomeShell";
            domeBall.transform.SetParent(domeRoot, false);
            domeBall.transform.localScale = Vector3.one * 13;
            domeBall.GetComponent<Renderer>().sharedMaterial = Mat("GoldDome", "Crab/Glow", m => { m.SetFloat("_Round", 0); m.SetFloat("_Rim", 1); m.SetColor("_Tint", new Color(1f, 0.78f, 0.3f, 0.55f)); });
            domeBall.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            usurer.dome = domeRoot;
            usurer.gemLight = AddLight(uRoot.transform, "GemLight", uRoot.transform.position + new Vector3(0, 6, -2.5f), new Color(1f, 0.25f, 0.3f), 2, 12);
            AddLight(uRoot.transform, "HallLight", uRoot.transform.position + new Vector3(0, 9, -12), new Color(1f, 0.85f, 0.6f), 3, 40);
            foreach (var (x, z) in new[] { (-12f, 70f), (12f, 70f), (0f, 59f) })
            {
                var hg = Variant(pile, "Pile1", root, x, z, R(0, 360), 1.5f, collider: "none");
                var ms = hg.AddComponent<MatterSource>();
                ms.kind = Matter.Gold;
                ms.amount = 10;
                ms.regrow = 0;
                ms.reach = 3.2f;
                usurer.hoards.Add(ms);
                AddLight(hg.transform, "HoardGlow", hg.transform.position + Vector3.up * 1.5f, new Color(1f, 0.8f, 0.35f), 1.5f, 6);
            }
            dir.usurer = usurer;
            var exit = RiftAt(root, 0, 80, 180, "greed_exit");
            exit.leadsTo = "Envy";
            exit.delay = 6;
            exit.membrane.sharedMaterial = Mat("RiftGreen", "Crab/Rift", m => { m.SetColor("_Color", new Color(0.5f, 1f, 0.8f)); m.SetColor("_Core", new Color(0.02f, 0.08f, 0.06f)); });
            dir.exit = exit;
            Ui<GameUI>();
            SaveScene(scene, "Greed");
        }

        // ------------------------------------------------------------------ Envy
        // a paradise island in a turquoise sea: the shore in the south, the village of white
        // pavilions in the middle, the Spring on a hill in the west, the mirror plaza and the lake
        // in the north
        static readonly Vector3 SpringHill = new Vector3(-30, 0, 36), MirrorPlaza = new Vector3(0, 0, 70);

        static float EnvyHeight(float x, float z)
        {
            float h = 0.8f + (Mathf.PerlinNoise(x * 0.03f + 51, z * 0.03f + 7) - 0.5f) * 3f;
            h += (Mathf.PerlinNoise(x * 0.12f + 5, z * 0.12f) - 0.5f) * 0.4f;
            // the Spring's hill
            float hill = Mathf.Clamp01(1 - new Vector2(x - SpringHill.x, z - SpringHill.z).magnitude / 22f);
            h += hill * hill * (3 - 2 * hill) * 5;
            float top = Mathf.Clamp01(1 - (new Vector2(x - SpringHill.x, z - SpringHill.z).magnitude - 4) / 3f);
            h = Mathf.Lerp(h, 6.2f, top);
            // the village and the plaza lie flat
            float village = Mathf.Clamp01(1 - (new Vector2(x, z - 4).magnitude - 18) / 8f);
            float plaza = Mathf.Clamp01(1 - (new Vector2(x - MirrorPlaza.x, z - MirrorPlaza.z).magnitude - 15) / 5f);
            h = Mathf.Lerp(h, 0.8f, Mathf.Max(village, plaza));
            // the lake behind the mirror, and the shore going down into the sea all round
            float lake = Mathf.Clamp01(1 - (new Vector2(x, z - 102).magnitude - 14) / 6f);
            h = Mathf.Lerp(h, -2f, lake);
            float r = new Vector2(x / 70f, (z - 25) / 95f).magnitude;
            h = Mathf.Lerp(h, -3f, Mathf.Clamp01((r - 0.82f) / 0.18f));
            return h;
        }

        static void BuildEnvy()
        {
            var scene = NewScene("Envy");
            ground = EnvyHeight;
            var (mc, mn) = TextureGen.Meadow();
            var meadow = Mat("Meadow", "Universal Render Pipeline/Lit", m =>
            {
                m.SetTexture("_BaseMap", mc);
                m.SetTexture("_BumpMap", mn);
                m.EnableKeyword("_NORMALMAP");
                m.SetFloat("_Smoothness", 0.15f);
            });
            var root = Group("Envy");
            Ground("EnvyGround", new Rect(-80, -80, 160, 210), meadow, 0.25f).transform.SetParent(root, false);
            var sea = Mat("SeaEnvy", "Crab/Water", m =>
            {
                m.SetColor("_Shallow", new Color(0.35f, 0.9f, 0.85f));
                m.SetColor("_Deep", new Color(0.05f, 0.4f, 0.6f));
                m.SetColor("_Sky", new Color(0.8f, 0.9f, 1f));
            });
            Sea(sea, -0.3f, 500).transform.SetParent(root, false);
            var sun = Sun(new Color(1f, 0.95f, 0.85f), 1.7f, new Vector3(45, 30, 0));
            Environment(new Color(0.5f, 0.5f, 0.5f), new Color(0.3f, 0.35f, 0.3f), 1, new Color(0.7f, 0.78f, 0.95f), new Color(0.8f, 0.75f, 0.72f), new Color(0.35f, 0.4f, 0.3f), new Color(0.85f, 0.82f, 0.92f), 0.0045f, 0.05f);
            RenderSettings.skybox = Mat("Sky_Envy_Gradient", "Crab/Sky", m =>
            {
                m.SetColor("_Top", new Color(0.3f, 0.55f, 0.95f));
                m.SetColor("_Horizon", new Color(0.98f, 0.88f, 0.92f));
                m.SetColor("_Bottom", new Color(0.6f, 0.75f, 0.8f));
                m.SetColor("_Sun", new Color(1f, 0.95f, 0.8f));
                m.SetVector("_SunDir", -sun.transform.forward);
                m.SetFloat("_Exponent", 0.6f);
            });
            Post("Envy", 0.3f, 18, new Color(1f, 0.97f, 0.98f), 1.0f, 0.18f, 6);

            // paradise: blossoming trees, flowers everywhere, white pavilions and arches
            var seeming = Group("Seeming", root);
            var tree = Glb("World/blossom_tree");
            var pavs = new[] { new Vector2(-13, -6), new Vector2(13, -2), new Vector2(0, 14), new Vector2(-15, 14), new Vector2(14, 16) };
            var flowers = Glb("World/flowers");
            bool Clear(float x, float z) => new Vector2(x, z - 4).magnitude > 20 && new Vector2(x - MirrorPlaza.x, z - MirrorPlaza.z).magnitude > 19
                                            && new Vector2(x - SpringHill.x, z - SpringHill.z).magnitude > 7 && Mathf.Abs(x) > 4 && EnvyHeight(x, z) > 0.2f;
            for (int i = 0; i < 60; i++)
            {
                float x = R(-60, 60), z = R(-60, 110);
                if (!Clear(x, z)) continue;
                Place(tree, seeming, x, z, R(0, 360), R(0.8f, 1.4f), collider: "box");
            }
            for (int i = 0; i < 420; i++)
            {
                float x = R(-62, 62), z = R(-62, 112);
                if (EnvyHeight(x, z) < 0.3f || new Vector2(x - MirrorPlaza.x, z - MirrorPlaza.z).magnitude < 15 || pavs.Any(p => (p - new Vector2(x, z)).magnitude < 4.5f)) continue;
                Variant(flowers, $"Flowers{rnd.Next(4)}", seeming, x, z, R(0, 360), R(2f, 3.5f), collider: "none");
            }
            var pav = Glb("World/pavilion");
            foreach (var p in pavs) Place(pav, root, p.x, p.y, R(0, 360), 1, sink: 0.1f);
            var arch = Glb("World/marble_arch");
            foreach (var (z, yaw) in new[] { (-30f, 0f), (32f, 0f) }) Place(arch, root, 0, z, yaw, 1, sink: 0.1f);
            Place(arch, root, -20, 30, 50, 1, sink: 0.1f);

            // what is really there: bones, black spires, burnt trees — shown only when the illusion slips
            var truth = Group("Truth", root);
            var ribs = Glb("World/ribs");
            var spires = Glb("World/spires");
            var burnt = Glb("World/burnt_tree");
            var totem = Glb("World/totem");
            for (int i = 0; i < 40; i++)
            {
                float x = R(-55, 55), z = R(-55, 100);
                if (EnvyHeight(x, z) < 0.2f) continue;
                int k = rnd.Next(4);
                if (k == 0) Place(ribs, truth, x, z, R(0, 360), R(0.6f, 1.2f), collider: "none");
                else if (k == 1) Variant(spires, $"Spire{rnd.Next(4)}", truth, x, z, R(0, 360), R(0.8f, 1.5f), collider: "none");
                else if (k == 2) Place(burnt, truth, x, z, R(0, 360), R(0.8f, 1.3f), collider: "none");
                else Place(totem, truth, x, z, R(0, 360), 1, collider: "none");
            }
            var glitch = new GameObject("Illusion").AddComponent<IllusionGlitch>();
            glitch.truth.Add(truth.gameObject);
            glitch.seeming.Add(seeming.gameObject);
            glitch.trueSky = Mat("Sky_Wrath_Gradient", "Crab/Sky");
            truth.gameObject.SetActive(false);

            // butterflies over the meadow
            var bf = new GameObject("Butterflies");
            bf.transform.SetParent(root, false);
            bf.transform.position = new Vector3(0, 2, 10);
            var ps = bf.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = 12;
            main.startSpeed = 0.4f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.14f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.6f, 0.85f), new Color(1f, 0.95f, 0.5f));
            main.maxParticles = 200;
            var em = ps.emission;
            em.rateOverTime = 14;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(80, 3, 100);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 1.2f;
            noise.frequency = 0.4f;
            bf.GetComponent<ParticleSystemRenderer>().sharedMaterial = lib.particleAdditive;
            bf.transform.SetParent(seeming, true);

            // start, beacons, director
            var start = Group("Start", root);
            start.position = new Vector3(0, H(0, -52), -52);
            var hero = BuildHero(start.position, 0, out _);
            Setup("Envy", start);
            var dir = new GameObject("Envy").AddComponent<EnvyDirector>();
            dir.glitch = glitch;
            dir.beacons.Add(BeaconAt(root, 5, -48, -30, "envy_shore"));
            dir.beacons.Add(BeaconAt(root, 5, 2, 180, "envy_village"));
            dir.beacons.Add(BeaconAt(root, SpringHill.x + 5, SpringHill.z - 4, 120, "envy_hill"));
            dir.beacons.Add(BeaconAt(root, 6, 50, 180, "envy_lake"));

            // the dwellers of the village
            var names = new[] { "Светлая", "Тихий", "Певунья", "Старейшина" };
            var lines = new[]
            {
                new[] { "Добро пожаловать, маленький! Здесь у всех всё есть.", "Останься. Тут никто никогда не плачет.", "А что это светится у тебя внутри?.." },
                new[] { "Тише… Слышишь? Это цветы растут.", "Мы ничего не делаем. Нам и так хорошо.", "Твоя Искра… Красивая. Очень." },
                new[] { "Ля-ля! Ты пришёл петь с нами?", "Наверху, на холме, Источник. Он даёт всё, чего пожелаешь!", "Почему у тебя есть то, чего нет у меня?" },
                new[] { "Мы ждали тебя, носитель. Очень долго ждали.", "Источник на холме откроет тебе, кто ты.", "Иди. Мы будем смотреть на тебя. Все." },
            };
            var dpos = new[] { (-9f, -3f), (10f, 1f), (2f, 10f), (-12f, 11f) };
            for (int i = 0; i < 4; i++)
            {
                var (x, z) = dpos[i];
                var m = (GameObject)PrefabUtility.InstantiatePrefab(lib.dweller);
                var dw = new GameObject("Dweller " + names[i]);
                dw.transform.SetPositionAndRotation(new Vector3(x, H(x, z), z), Quaternion.Euler(0, R(0, 360), 0));
                m.transform.SetParent(dw.transform, false);
                var col = dw.AddComponent<CapsuleCollider>();
                col.center = new Vector3(0, 0.9f, 0);
                col.height = 1.8f;
                col.radius = 0.35f;
                var d = dw.AddComponent<Dweller>();
                d.model = m.transform;
                d.title = names[i];
                d.lines = lines[i];
                d.reach = 3;
                dir.dwellers.Add(d);
                AddLight(dw.transform, "Halo", dw.transform.position + Vector3.up * 2, new Color(1f, 0.9f, 0.6f), 1, 4);
            }

            // matter in paradise: crystals in the rocks, a little scrap of an older world, flowers (organic)
            void Source(GameObject model, string variant, float x, float z, Matter kind, int amount, float regrow, float scale = 1)
            {
                var go = variant != null ? Variant(model, variant, root, x, z, R(0, 360), scale, collider: "none") : Place(model, root, x, z, R(0, 360), scale, collider: "none", sink: 0.02f);
                var ms = go.AddComponent<MatterSource>();
                ms.kind = kind;
                ms.amount = amount;
                ms.regrow = regrow;
                ms.reach = 2.6f;
            }
            var crys = Glb("World/crystal");
            foreach (var (x, z) in new[] { (-20f, -20f), (22f, -26f), (-34f, 10f), (30f, 30f), (-22f, 50f), (24f, 56f), (-36f, 40f) }) Source(crys, $"Crystal{rnd.Next(3)}", x, z, Matter.Crystal, 5, 60);
            foreach (var (x, z) in new[] { (-6f, -36f), (14f, -14f), (-26f, 24f), (18f, 44f) }) Source(Glb("World/scrap"), null, x, z, Matter.Metal, 8, 50);
            foreach (var (x, z) in new[] { (8f, -40f), (-18f, 0f), (26f, 8f), (-10f, 44f) }) Source(Glb("World/rubble"), null, x, z, Matter.Mineral, 6, 50);
            foreach (var (x, z) in new[] { (-4f, -20f), (6f, 24f), (-30f, -10f) }) Source(Glb("World/pylon"), null, x, z, Matter.Energy, 5, 45);

            // the Spring on the hill
            var springGo = Place(Glb("World/spring"), root, SpringHill.x, SpringHill.z, 0, 1, sink: 0.05f);
            var spring = springGo.AddComponent<Spring>();
            spring.reach = 3.6f;
            spring.glow = AddLight(springGo.transform, "Glow", springGo.transform.position + Vector3.up * 2, new Color(0.8f, 0.6f, 1f), 4, 14);
            dir.spring = spring;

            // the mirror plaza: a disc of white marble, the great mirror at its north edge, the lake beyond
            var marble = Mat("Marble", "Universal Render Pipeline/Lit", m => { m.SetColor("_BaseColor", new Color(0.82f, 0.8f, 0.88f)); m.SetFloat("_Smoothness", 0.6f); });
            var plaza = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            plaza.name = "Plaza";
            plaza.transform.SetParent(root, false);
            plaza.transform.position = new Vector3(MirrorPlaza.x, H(MirrorPlaza.x, MirrorPlaza.z) - 0.1f, MirrorPlaza.z);
            plaza.transform.localScale = new Vector3(32, 0.2f, 32);
            plaza.GetComponent<Renderer>().sharedMaterial = marble;
            UnityEngine.Object.DestroyImmediate(plaza.GetComponent<Collider>());
            var mirrorGo = Place(Glb("World/giant_mirror"), root, 0, MirrorPlaza.z + 16, 180, 1.3f, sink: 0);
            var glass = CrabRig.Find(mirrorGo.transform, "Glass");
            if (glass) glass.GetComponent<Renderer>().sharedMaterial = lib.mirror;
            foreach (var x in new[] { -13f, 13f })
                Place(Glb("World/column"), root, x, MirrorPlaza.z + 6, 0, 1.2f);
            var arena = Group("Arena", root);
            arena.position = new Vector3(MirrorPlaza.x, H(MirrorPlaza.x, MirrorPlaza.z), MirrorPlaza.z);

            // the Reflection: the crab itself, giant, of dark mirror-glass
            var rRoot = new GameObject("Reflection");
            rRoot.transform.SetPositionAndRotation(arena.position + Vector3.forward * 7, Quaternion.Euler(0, 180, 0));
            var model = (GameObject)PrefabUtility.InstantiatePrefab(lib.hero, rRoot.transform);
            model.name = "Model";
            model.transform.localScale *= 3.5f;   // the hero model carries its own scale
            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = lib.mirror;
                r.sharedMaterials = mats;
            }
            var rig = rRoot.AddComponent<CrabRig>();
            rig.body = CrabRig.BodyOf(model.transform);
            var box = rRoot.AddComponent<BoxCollider>();
            box.center = new Vector3(0, 1.1f, 0);
            box.size = new Vector3(2.8f, 1.8f, 2.2f);
            var mc2 = rRoot.AddComponent<MirrorCrab>();
            mc2.rig = rig;
            mc2.arena = arena;
            var core = new GameObject("Core").transform;
            core.SetParent(rRoot.transform, false);
            core.localPosition = new Vector3(0, 1.2f, 0.4f);
            mc2.core = core;
            mc2.glow = AddLight(rRoot.transform, "Glare", rRoot.transform.position + Vector3.up * 3, new Color(0.85f, 0.85f, 1f), 1.5f, 14);
            dir.reflection = mc2;
            var exit = RiftAt(root, 0, MirrorPlaza.z + 9, 180, "envy_exit");
            exit.leadsTo = "Gloom";
            exit.delay = 6;
            exit.membrane.sharedMaterial = Mat("RiftViolet", "Crab/Rift", m => { m.SetColor("_Color", new Color(0.7f, 0.55f, 1f)); m.SetColor("_Core", new Color(0.05f, 0.02f, 0.1f)); });
            exit.glow.color = new Color(0.7f, 0.55f, 1f);
            dir.exit = exit;
            Ui<GameUI>();
            SaveScene(scene, "Envy");
        }

        // ------------------------------------------------------------------ Despondency
        // a vast cave under a stone dome: the way in from the south, the shards of memory in the
        // dark to the west, east and north-west, the ancient machine in the north, the hall of the
        // Devourer beyond it
        static readonly Vector3 GloomArena = new Vector3(0, 0, 72);

        static float GloomHeight(float x, float z)
        {
            float h = (Mathf.PerlinNoise(x * 0.04f + 71, z * 0.04f + 13) - 0.5f) * 4f;
            h += (Mathf.PerlinNoise(x * 0.15f + 3, z * 0.15f) - 0.5f) * 0.8f;
            // the hall and the path stay walkable
            float hall = Mathf.Clamp01(1 - (new Vector2(x - GloomArena.x, z - GloomArena.z).magnitude - 17) / 6f);
            h = Mathf.Lerp(h, 0, hall);
            // a black lake to the east
            float lake = Mathf.Clamp01(1 - (new Vector2(x - 34, z - 40).magnitude - 10) / 6f);
            h = Mathf.Lerp(h, -2.5f, lake);
            // the walls of the cave rise at the edge
            float r = new Vector2(x / 75f, (z - 15) / 85f).magnitude;
            h += Mathf.Clamp01((r - 0.85f) / 0.15f) * 25;
            return h;
        }

        static Volume DespairVolume()
        {
            string path = $"{Gen}/Volume_Despair.asset";
            AssetDatabase.DeleteAsset(path);
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(p, path);
            var ca = p.Add<ColorAdjustments>(true); ca.saturation.value = -95; ca.postExposure.value = -0.5f; ca.contrast.value = 20;
            var v = p.Add<Vignette>(true); v.intensity.value = 0.55f; v.smoothness.value = 0.6f; v.color.value = new Color(0.02f, 0.02f, 0.05f);
            foreach (var c in p.components) AssetDatabase.AddObjectToAsset(c, p);
            EditorUtility.SetDirty(p);
            var go = new GameObject("DespairPost");
            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 5;
            vol.weight = 0;
            vol.sharedProfile = p;
            return vol;
        }

        static void BuildGloom()
        {
            var scene = NewScene("Gloom");
            ground = GloomHeight;
            var (cc, cn) = TextureGen.Cave();
            var floor = Mat("CaveFloor", "Universal Render Pipeline/Lit", m =>
            {
                m.SetTexture("_BaseMap", cc);
                m.SetTexture("_BumpMap", cn);
                m.EnableKeyword("_NORMALMAP");
                m.SetFloat("_Smoothness", 0.45f);
            });
            var root = Group("Gloom");
            Ground("GloomGround", new Rect(-90, -85, 180, 200), floor, 0.2f).transform.SetParent(root, false);
            var water = Mat("BlackWater", "Crab/Water", m =>
            {
                m.SetColor("_Shallow", new Color(0.04f, 0.06f, 0.09f));
                m.SetColor("_Deep", new Color(0.0f, 0.0f, 0.02f));
                m.SetColor("_Sky", new Color(0.1f, 0.12f, 0.2f));
                m.SetFloat("_WaveHeight", 0.03f);
                m.SetColor("_Foam", new Color(0.12f, 0.14f, 0.2f));
                m.SetFloat("_Alpha", 0.97f);
            });
            Sea(water, -0.9f, 300).transform.SetParent(root, false);
            var dome = Place(Glb("World/cave_dome"), root, 0, 15, 0, 1, collider: "none", sink: 0);
            dome.transform.position = new Vector3(0, -3, 15);
            var sun = Sun(new Color(0.45f, 0.5f, 0.75f), 0.12f, new Vector3(70, 20, 0));
            sun.shadows = LightShadows.None;
            Environment(new Color(0, 0, 0), Color.black, 0, new Color(0.05f, 0.055f, 0.08f), new Color(0.035f, 0.04f, 0.055f), new Color(0.02f, 0.02f, 0.03f), new Color(0.015f, 0.018f, 0.03f), 0.03f, 0);
            RenderSettings.skybox = Mat("Sky_Gloom_Gradient", "Crab/Sky", m =>
            {
                m.SetFloat("_SunSize", 6);
                m.SetColor("_Top", Color.black);
                m.SetColor("_Horizon", new Color(0.02f, 0.02f, 0.035f));
                m.SetColor("_Bottom", Color.black);
                m.SetColor("_Sun", Color.black);
            });
            Post("Gloom", 0.6f, -10, new Color(0.9f, 0.95f, 1f), 1.4f, 0.35f, 14);
            var despair = new GameObject("Despair");

            // stone teeth, glowing mushrooms (some give energy), stalagmites
            var props = Group("Scenery", root);
            var stal = Glb("World/stalagmites");
            var mush = Glb("World/glow_mushrooms");
            bool Clear(float x, float z) => new Vector2(x - GloomArena.x, z - GloomArena.z).magnitude > 20 && Mathf.Abs(x) > 3 && GloomHeight(x, z) < 3 && GloomHeight(x, z) > -0.5f;
            for (int i = 0; i < 70; i++)
            {
                float x = R(-65, 65), z = R(-65, 95);
                if (!Clear(x, z)) continue;
                Variant(stal, $"Stalag{rnd.Next(4)}", props, x, z, R(0, 360), R(0.7f, 1.8f));
            }
            int lights = 0;
            for (int i = 0; i < 46; i++)
            {
                float x = R(-60, 60), z = R(-60, 92);
                if (!Clear(x, z)) continue;
                var m = Variant(mush, $"Mush{rnd.Next(3)}", props, x, z, R(0, 360), R(1f, 2f), collider: "none");
                var zone = m.AddComponent<LightZone>();
                zone.radius = 3.2f;
                if (lights++ < 14) AddLight(m.transform, "Glow", m.transform.position + Vector3.up * 0.8f, new Color(0.4f, 0.9f, 1f), 1.2f, 6);
                if (i % 4 == 0)
                {
                    var ms = m.AddComponent<MatterSource>();
                    ms.kind = Matter.Energy;
                    ms.amount = 4;
                    ms.regrow = 60;
                    ms.reach = 2.4f;
                }
            }

            // start, beacons, the director
            var start = Group("Start", root);
            start.position = new Vector3(0, H(0, -56), -56);
            var hero = BuildHero(start.position, 0, out _);
            Setup("Gloom", start);
            var dir = new GameObject("Gloom").AddComponent<GloomDirector>();
            var dsp = despair.AddComponent<Despair>();
            dsp.grey = DespairVolume();
            dir.despair = dsp;
            dir.beacons.Add(BeaconAt(root, 5, -52, -30, "gloom_entry"));
            dir.beacons.Add(BeaconAt(root, 4, 14, 180, "gloom_middle"));
            dir.beacons.Add(BeaconAt(root, 6, 50, 180, "gloom_deep"));

            // the lamps of memory along the way
            MemoryLamp Lamp(float x, float z, float hold)
            {
                var go = Place(Glb("World/memory_lamp"), root, x, z, R(0, 360), 1);
                var l = go.AddComponent<MemoryLamp>();
                l.name = $"Lamp {x:0}_{z:0}";
                l.flame = CrabRig.Find(go.transform, "GlowFlame");
                l.glow = AddLight(go.transform, "Flame", go.transform.position + Vector3.up * 1.8f, new Color(1f, 0.75f, 0.45f), 4, 14);
                l.zone = go.AddComponent<LightZone>();
                l.zone.radius = 9;
                l.lightTime = hold;
                l.reach = 2.6f;
                return l;
            }
            foreach (var (x, z) in new[] { (-6f, -40f), (12f, -24f), (-14f, -6f), (9f, 8f), (-20f, 24f), (22f, 28f), (2f, 44f), (-34f, -18f), (34f, -2f), (-28f, 40f) })
                dir.lamps.Add(Lamp(x, z, 0));

            // the shards of memory, each before its fresco
            var fresco = Glb("World/fresco");
            var shardModel = Glb("World/memory_shard");
            var visions = new[]
            {
                ("knight", -38f, -24f, 60f, new[] { "Видение… Рыцарь в сияющих доспехах. В его груди — Искра. Моя Искра.", "Он шёл через ад с мечом. Он дошёл до третьего круга…", "…и погас. Искра выпала из него, как уголёк, и покатилась дальше." }),
                ("bird", 40f, -6f, -80f, new[] { "Птица. Огромная, огненная. Искра горела у неё под крылом.", "Она летела над кругами ада, и демоны прятались от её света.", "Она дошла до шестого круга… и упала. Искра снова осталась одна." }),
                ("machine", -32f, 46f, 120f, new[] { "Машина. Гигант из металла, а в нём — Искра и чей-то голос.", "Она прошла дальше всех. Восьмой круг. Девятый…", "…и замерла. Здесь, в темноте. Искра поднялась наверх — и упала на берег. Ко мне." }),
            };
            for (int i = 0; i < visions.Length; i++)
            {
                var (id, x, z, yaw, lines) = visions[i];
                var sgo = Place(shardModel, root, x, z, yaw, 1);
                var s = sgo.AddComponent<MemoryShard>();
                s.id = id;
                s.lines = lines;
                s.reach = 2.6f;
                AddLight(sgo.transform, "ShardGlow", sgo.transform.position + Vector3.up * 1.6f, new Color(1f, 0.92f, 0.75f), 2, 7);
                var back = sgo.transform.position - sgo.transform.forward * 4.5f;
                var f = Variant(fresco, $"Fresco{i}", root, back.x, back.z, yaw, 1.1f);
                s.fresco = f.transform;
                s.frescoLight = AddLight(f.transform, "FrescoLight", f.transform.position + f.transform.forward * 4 + Vector3.up * 4, new Color(1f, 0.88f, 0.7f), 6, 12);
                s.frescoLight.enabled = false;
                dir.shards.Add(s);
            }

            // the ancient machine: the last bearer's body, its AI asleep
            var robot = Place(Glb("World/ancient_robot"), root, 14, 54, 200, 1.2f, sink: 0.2f);
            var am = robot.AddComponent<AncientMachine>();
            am.reach = 5;
            am.eye = AddLight(robot.transform, "Eye", robot.transform.position + Vector3.up * 6.5f + robot.transform.forward * 1.2f, new Color(0.35f, 0.85f, 1f), 0, 10);
            dir.machine = am;

            // matter: old machine scrap, crystals in the walls, stone
            void Source(GameObject model, string variant, float x, float z, Matter kind, int amount, float regrow)
            {
                var go = variant != null ? Variant(model, variant, root, x, z, R(0, 360), 1.2f, collider: "none") : Place(model, root, x, z, R(0, 360), 1, collider: "none", sink: 0.02f);
                var ms = go.AddComponent<MatterSource>();
                ms.kind = kind;
                ms.amount = amount;
                ms.regrow = regrow;
                ms.reach = 2.6f;
            }
            var scrap = Glb("World/scrap");
            foreach (var (x, z) in new[] { (-4f, -30f), (16f, -14f), (-18f, 6f), (8f, 30f), (18f, 46f), (6f, 58f), (22f, 58f) }) Source(scrap, null, x, z, Matter.Metal, 9, 45);
            foreach (var (x, z) in new[] { (-26f, -36f), (28f, -30f), (-40f, 10f), (40f, 20f), (-12f, 52f) }) Source(Glb("World/crystal"), $"Crystal{rnd.Next(3)}", x, z, Matter.Crystal, 6, 50);
            foreach (var (x, z) in new[] { (10f, -46f), (-22f, 14f), (26f, 12f) }) Source(Glb("World/rubble"), null, x, z, Matter.Mineral, 6, 45);
            foreach (var (x, z) in new[] { (-8f, 36f), (14f, -36f) }) Source(Glb("World/gold_pile"), "Pile2", x, z, Matter.Gold, 5, 60);

            // the wraiths in the dark
            foreach (var (x, z) in new[] { (-4f, -24f), (14f, -8f), (-24f, -10f), (-12f, 16f), (26f, 18f), (-30f, 30f), (4f, 36f), (30f, -16f), (-6f, 60f), (24f, 64f) })
            {
                var w = (GameObject)PrefabUtility.InstantiatePrefab(lib.wraith);
                w.transform.position = new Vector3(x, H(x, z), z);
            }

            // the hall of darkness: three lamps that take holding E, the Devourer at the north end
            var arena = Group("Arena", root);
            arena.position = new Vector3(GloomArena.x, H(GloomArena.x, GloomArena.z), GloomArena.z);
            var d = (GameObject)PrefabUtility.InstantiatePrefab(lib.devourer);
            var dRoot = new GameObject("Devourer");
            dRoot.transform.SetPositionAndRotation(arena.position + Vector3.forward * 19, Quaternion.Euler(0, 180, 0));
            d.transform.SetParent(dRoot.transform, false);
            var cap = dRoot.AddComponent<CapsuleCollider>();
            cap.center = new Vector3(0, 6, 0);
            cap.height = 12;
            cap.radius = 3.4f;
            var dev = dRoot.AddComponent<Devourer>();
            dev.model = d.transform;
            dev.arena = arena;
            dev.heartLight = AddLight(dRoot.transform, "HeartLight", dRoot.transform.position + new Vector3(0, 6.8f, -2.5f), new Color(0.7f, 0.45f, 1f), 1, 18);
            foreach (var a in new[] { -40f, 90f, 220f })
            {
                float rad = a * Mathf.Deg2Rad;
                var l = Lamp(arena.position.x + Mathf.Cos(rad) * 10, arena.position.z + Mathf.Sin(rad) * 10, 1.5f);
                dev.lamps.Add(l);
            }
            dir.devourer = dev;
            var exit = RiftAt(root, 0, GloomArena.z + 8, 180, "gloom_exit");
            exit.leadsTo = "Betrayal";
            exit.delay = 6;
            exit.membrane.sharedMaterial = Mat("RiftPale", "Crab/Rift", m => { m.SetColor("_Color", new Color(1f, 0.92f, 0.75f)); m.SetColor("_Core", new Color(0.08f, 0.06f, 0.04f)); });
            exit.glow.color = new Color(1f, 0.9f, 0.7f);
            dir.exit = exit;
            Ui<GameUI>();
            SaveScene(scene, "Gloom");
        }

        // ------------------------------------------------------------------ Betrayal
        // a labyrinth of 7x7 cells of 10 m (x -35..35, z 0..70), the way in from the south, the
        // heart of the labyrinth (a round hall) to the north
        const int MazeN = 7;
        const float MazeCell = 10;
        static readonly Vector3 Heart = new Vector3(0, 0, 94);

        static float BetrayalHeight(float x, float z)
        {
            bool inside = Mathf.Abs(x) < 40 && z > -5 && z < 118;
            float h = (Mathf.PerlinNoise(x * 0.05f + 91, z * 0.05f + 3) - 0.5f) * 2.5f;
            return inside ? 0 : Mathf.Lerp(0, h, Mathf.Clamp01((Mathf.Max(Mathf.Abs(x) - 40, Mathf.Max(-5 - z, z - 118))) / 8f));
        }

        static Vector3 CellCenter(int cx, int cz) => new Vector3(-35 + (cx + 0.5f) * MazeCell, 0, (cz + 0.5f) * MazeCell);

        static void BuildBetrayal()
        {
            var scene = NewScene("Betrayal");
            ground = BetrayalHeight;
            var (fc, fn) = TextureGen.Cave();
            var floor = Mat("IronFloor", "Universal Render Pipeline/Lit", m =>
            {
                m.SetTexture("_BaseMap", fc);
                m.SetTexture("_BumpMap", fn);
                m.EnableKeyword("_NORMALMAP");
                m.SetColor("_BaseColor", new Color(1.6f, 1.25f, 1.05f));
                m.SetFloat("_Smoothness", 0.35f);
                m.SetFloat("_Metallic", 0.3f);
            });
            var root = Group("Betrayal");
            Ground("BetrayalGround", new Rect(-80, -60, 160, 200), floor, 0.25f).transform.SetParent(root, false);
            var sun = Sun(new Color(1f, 0.6f, 0.4f), 1.1f, new Vector3(35, 210, 0));
            Environment(new Color(0.5f, 0.5f, 0.5f), Color.black, 1, new Color(0.5f, 0.4f, 0.42f), new Color(0.46f, 0.34f, 0.28f), new Color(0.18f, 0.13f, 0.11f), new Color(0.26f, 0.17f, 0.14f), 0.01f, 0.03f);
            RenderSettings.skybox = Mat("Sky_Betrayal_Gradient", "Crab/Sky", m =>
            {
                m.SetFloat("_SunSize", 6);
                m.SetColor("_Top", new Color(0.08f, 0.05f, 0.06f));
                m.SetColor("_Horizon", new Color(0.45f, 0.2f, 0.12f));
                m.SetColor("_Bottom", new Color(0.1f, 0.06f, 0.05f));
                m.SetColor("_Sun", new Color(1f, 0.5f, 0.3f));
                m.SetVector("_SunDir", -sun.transform.forward);
            });
            Post("Betrayal", 0.9f, -5, new Color(1f, 0.9f, 0.82f), 1.1f, 0.35f, 16);

            // the maze: a depth-first walk over the cells knocks down walls
            var open = new bool[MazeN, MazeN, 4];   // 0 north (+z), 1 east (+x), 2 south, 3 west
            var seen = new bool[MazeN, MazeN];
            int[] dx = { 0, 1, 0, -1 }, dz = { 1, 0, -1, 0 };
            var stack = new Stack<(int, int)>();
            stack.Push((3, 0));
            seen[3, 0] = true;
            while (stack.Count > 0)
            {
                var (cx, cz) = stack.Peek();
                var dirs = new List<int>();
                for (int d = 0; d < 4; d++)
                {
                    int nx = cx + dx[d], nz = cz + dz[d];
                    if (nx >= 0 && nz >= 0 && nx < MazeN && nz < MazeN && !seen[nx, nz]) dirs.Add(d);
                }
                if (dirs.Count == 0) { stack.Pop(); continue; }
                int dd = dirs[rnd.Next(dirs.Count)];
                int ax = cx + dx[dd], az = cz + dz[dd];
                open[cx, cz, dd] = true;
                open[ax, az, (dd + 2) % 4] = true;
                seen[ax, az] = true;
                stack.Push((ax, az));
            }
            // a few extra openings so it is not a single thread
            for (int k = 0; k < 6; k++)
            {
                int cx = rnd.Next(MazeN - 1), cz = rnd.Next(MazeN);
                open[cx, cz, 1] = true;
                open[cx + 1, cz, 3] = true;
            }
            open[3, 0, 2] = true;            // the way in
            open[3, MazeN - 1, 0] = true;    // the way to the heart
            // the solution: breadth-first from the entrance to the exit cell
            var prev = new Dictionary<(int, int), (int, int)>();
            var q = new Queue<(int, int)>();
            q.Enqueue((3, 0));
            prev[(3, 0)] = (-1, -1);
            while (q.Count > 0)
            {
                var (cx, cz) = q.Dequeue();
                if (cx == 3 && cz == MazeN - 1) break;
                for (int d = 0; d < 4; d++)
                {
                    if (!open[cx, cz, d]) continue;
                    int nx = cx + dx[d], nz = cz + dz[d];
                    if (nx < 0 || nz < 0 || nx >= MazeN || nz >= MazeN || prev.ContainsKey((nx, nz))) continue;
                    prev[(nx, nz)] = (cx, cz);
                    q.Enqueue((nx, nz));
                }
            }
            var cells = new List<(int, int)>();
            for (var c = (3, MazeN - 1); c.Item1 >= 0; c = prev[c]) cells.Add(c);
            cells.Reverse();

            var maze = Group("Maze", root);
            var wall = Glb("World/mech_wall");
            for (int cx = 0; cx < MazeN; cx++)
                for (int cz = 0; cz < MazeN; cz++)
                {
                    var c = CellCenter(cx, cz);
                    if (!open[cx, cz, 0]) Place(wall, maze, c.x, c.z + MazeCell / 2, 0, 1, sink: 0);
                    if (!open[cx, cz, 1]) Place(wall, maze, c.x + MazeCell / 2, c.z, 90, 1, sink: 0);
                    if (cz == 0 && !open[cx, cz, 2]) Place(wall, maze, c.x, c.z - MazeCell / 2, 0, 1, sink: 0);
                    if (cx == 0 && !open[cx, cz, 3]) Place(wall, maze, c.x - MazeCell / 2, c.z, 90, 1, sink: 0);
                }
            // work lamps of the legion in every other cell: warm pools of light along the iron
            var lamp = Glb("World/lamp");
            for (int cx = 0; cx < MazeN; cx++)
                for (int cz = 0; cz < MazeN; cz++)
                {
                    if ((cx + cz) % 2 != 0) continue;
                    var c = CellCenter(cx, cz);
                    float sx = rnd.Next(2) == 0 ? -1 : 1, sz = rnd.Next(2) == 0 ? -1 : 1;
                    var p = c + new Vector3(sx, 0, sz) * (MazeCell / 2 - 1.3f);
                    var lg = Place(lamp, maze, p.x, p.z, Mathf.Atan2(-sx, -sz) * Mathf.Rad2Deg, 1, collider: "box");
                    var li = AddLight(lg.transform, "WorkLight", lg.transform.position + Vector3.up * 3.2f, new Color(1f, 0.55f, 0.28f), 12, 15);
                    li.shadows = LightShadows.None;
                }
            // gears turning on the walls, machine towers round about
            var gear = Glb("World/gear");
            for (int i = 0; i < 14; i++)
            {
                var c = CellCenter(rnd.Next(MazeN), rnd.Next(MazeN));
                var g = Place(gear, maze, c.x + MazeCell / 2 - 0.7f, c.z + R(-2, 2), 90, R(0.8f, 1.3f), collider: "none", sink: 0);
                g.transform.position += Vector3.up * R(3, 5);
                var gp = CrabRig.Find(g.transform, "Gear");
                if (gp) gp.gameObject.AddComponent<Spinner>().speed = R(-30, 30);
            }
            foreach (var (x, z) in new[] { (-50f, 10f), (50f, 20f), (-48f, 60f), (52f, 70f), (-30f, -30f), (34f, -28f) })
                Place(Glb("World/machine_tower"), root, x, z, R(0, 360), R(0.9f, 1.3f));

            // start, beacons, director
            var start = Group("Start", root);
            start.position = new Vector3(0, H(0, -22), -22);
            var hero = BuildHero(start.position, 0, out _);
            Setup("Betrayal", start);
            var dir = new GameObject("Betrayal").AddComponent<BetrayalDirector>();
            dir.beacons.Add(BeaconAt(root, 5, -18, -30, "betrayal_gate"));

            // the Guide and its way: the entrance, the cells of the solution, into the heart
            var g0 = (GameObject)PrefabUtility.InstantiatePrefab(lib.guide);
            var guideGo = new GameObject("Guide");
            guideGo.transform.position = new Vector3(0, 0, -12);
            g0.transform.SetParent(guideGo.transform, false);
            var guide = guideGo.AddComponent<Guide>();
            guide.model = g0.transform;
            AddLight(guideGo.transform, "Lantern", guideGo.transform.position + new Vector3(0.3f, 0.5f, 0.2f), new Color(1f, 0.85f, 0.55f), 2.5f, 8);
            guide.path.Add(new Vector3(0, 0, -12));
            guide.path.Add(new Vector3(0, 0, -3));
            foreach (var (cx, cz) in cells) guide.path.Add(CellCenter(cx, cz));
            guide.path.Add(new Vector3(0, 0, MazeN * MazeCell + 3));
            guide.path.Add(Heart + new Vector3(0, 0, -14));
            dir.guide = guide;
            int Wp(int cellIndex) => cellIndex + 2;   // a path cell's index among the waypoints

            // two gates on the way, each with its lever in the cell before it
            var gateM = Glb("World/mech_gate");
            var leverM = Glb("World/lever");
            foreach (int ci in new[] { 2, Mathf.Min(cells.Count - 3, 8) })
            {
                var a = CellCenter(cells[ci].Item1, cells[ci].Item2);
                var b = CellCenter(cells[ci + 1].Item1, cells[ci + 1].Item2);
                var mid = (a + b) / 2;
                float yaw = Mathf.Abs(a.x - b.x) > 0.1f ? 90 : 0;
                var gg = Place(gateM, maze, mid.x, mid.z, yaw, 1, sink: 0);
                var mg = gg.AddComponent<MechGate>();
                mg.door = CrabRig.Find(gg.transform, "Door");
                guide.gates.Add(mg);
                var side = Quaternion.Euler(0, yaw + 90, 0) * Vector3.forward;
                var lp = a + side * 3.5f - (b - a).normalized * 1.5f;
                var lv = Place(leverM, maze, lp.x, lp.z, Mathf.Atan2(-side.x, -side.z) * Mathf.Rad2Deg, 1, collider: "box", sink: 0);
                var lever = lv.AddComponent<Lever>();
                lever.handle = CrabRig.Find(lv.transform, "Handle");
                lever.gates.Add(mg);
                lever.reach = 2.4f;
                AddLight(lv.transform, "LeverLamp", lv.transform.position + Vector3.up * 2, new Color(1f, 0.4f, 0.3f), 1.5f, 5);
            }
            // crushers in two corridors
            foreach (int ci in new[] { 4, Mathf.Min(cells.Count - 2, 10) })
            {
                var c = CellCenter(cells[ci].Item1, cells[ci].Item2);
                var cr = Place(Glb("World/crusher"), maze, c.x, c.z, R(0, 2) < 1 ? 0 : 90, 1, collider: "none", sink: 0);
                var crusher = cr.AddComponent<Crusher>();
                crusher.head = CrabRig.Find(cr.transform, "Head");
                crusher.offset = R(0, 3);
                foreach (var post in cr.GetComponentsInChildren<MeshFilter>())
                    if (post.name == "Frame") post.gameObject.AddComponent<MeshCollider>().sharedMesh = post.sharedMesh;
            }
            // the legion's workshop beside the way (the crab becomes the architect here)
            int wsCell = Mathf.Min(6, cells.Count - 2);
            dir.workshopStop = Wp(wsCell);
            var wc = CellCenter(cells[wsCell].Item1, cells[wsCell].Item2);
            var wsGo = Place(Glb("World/workshop"), root, wc.x + 2.5f, wc.z + 2.5f, 225, 0.8f, collider: "none", sink: 0);
            var ws = wsGo.AddComponent<Workshop>();
            ws.build = CrabRig.Find(wsGo.transform, "Build");
            ws.reach = 4;
            ws.forgeLight = AddLight(wsGo.transform, "Forge", wsGo.transform.position + Vector3.up * 1.2f, new Color(1f, 0.55f, 0.2f), 3, 10);
            dir.workshop = ws;
            dir.beacons.Add(BeaconAt(root, wc.x - 3, wc.z - 3, 45, "betrayal_workshop"));

            // the legion: bands along the way
            foreach (int ci in new[] { 3, 7, Mathf.Min(cells.Count - 2, 11) })
            {
                if (ci >= cells.Count) continue;
                var c = CellCenter(cells[ci].Item1, cells[ci].Item2);
                var band = new GameObject("Legion").AddComponent<LegionBand>();
                band.transform.SetPositionAndRotation(c, Quaternion.LookRotation(CellCenter(cells[ci - 1].Item1, cells[ci - 1].Item2) - c));
                band.ranks = 2;
                band.files = 2;
                band.trigger = 9;
                dir.bands.Add(band);
            }

            // matter in the labyrinth
            void Source(GameObject model, string variant, Vector3 p, Matter kind, int amount, float regrow)
            {
                var go = variant != null ? Variant(model, variant, root, p.x, p.z, R(0, 360), 1, collider: "none") : Place(model, root, p.x, p.z, R(0, 360), 1, collider: "none", sink: 0.02f);
                var ms = go.AddComponent<MatterSource>();
                ms.kind = kind;
                ms.amount = amount;
                ms.regrow = regrow;
                ms.reach = 2.6f;
            }
            var scrap = Glb("World/scrap");
            for (int i = 0; i < cells.Count; i += 2) Source(scrap, null, CellCenter(cells[i].Item1, cells[i].Item2) + new Vector3(3, 0, -3), Matter.Metal, 9, 45);
            Source(scrap, null, new Vector3(-6, 0, -16), Matter.Metal, 9, 45);
            Source(Glb("World/rubble"), null, new Vector3(6, 0, -10), Matter.Mineral, 8, 45);
            Source(Glb("World/crystal"), "Crystal1", wc + new Vector3(-3, 0, 3), Matter.Crystal, 8, 40);
            Source(Glb("World/pylon"), null, wc + new Vector3(3, 0, -3), Matter.Energy, 8, 40);
            Source(Glb("World/gold_pile"), "Pile0", wc + new Vector3(-3.5f, 0, -2), Matter.Gold, 6, 40);
            Source(Glb("World/rubble"), null, wc + new Vector3(0, 0, 3.5f), Matter.Mineral, 8, 40);
            Source(Glb("World/glow_mushrooms"), "Mush2", new Vector3(-4, 0, -8), Matter.Energy, 5, 50);

            // the heart: a round hall ringed with walls, the gate behind the crab, the idol in the middle
            var heart = Group("Heart", root);
            for (int i = 0; i < 14; i++)
            {
                float a = i / 14f * Mathf.PI * 2;
                if (Mathf.Abs(Mathf.Sin(a) + 1) < 0.1f) continue;   // the way in from the south
                Place(wall, heart, Heart.x + Mathf.Cos(a) * 22, Heart.z + Mathf.Sin(a) * 22, -a * Mathf.Rad2Deg + 90, 1, sink: 0);
            }
            var hgGo = Place(gateM, heart, Heart.x, Heart.z - 22, 0, 1, sink: 0);
            var hg = hgGo.AddComponent<MechGate>();
            hg.door = CrabRig.Find(hgGo.transform, "Door");
            hg.open = true;
            dir.heartGate = hg;
            foreach (var (x, z) in new[] { (-14f, 84f), (14f, 84f) })
            {
                var trap = new GameObject("Trap").AddComponent<LegionBand>();
                trap.transform.SetPositionAndRotation(new Vector3(x, 0, z), Quaternion.LookRotation(new Vector3(-x, 0, Heart.z - 14 - z)));
                trap.auto = false;
                trap.ranks = 2;
                trap.files = 2;
                dir.trap.Add(trap);
            }
            foreach (var (x, z) in new[] { (-10f, 88f), (10f, 100f), (-8f, 104f), (12f, 86f) }) Source(scrap, null, new Vector3(x, 0, z), Matter.Metal, 6, 25);
            var arena = Group("Arena", root);
            arena.position = Heart;
            var j = (GameObject)PrefabUtility.InstantiatePrefab(lib.janus);
            var jRoot = new GameObject("Janus");
            jRoot.transform.SetPositionAndRotation(Heart + Vector3.forward * 4, Quaternion.Euler(0, 180, 0));
            j.transform.SetParent(jRoot.transform, false);
            var cap = jRoot.AddComponent<CapsuleCollider>();
            cap.center = new Vector3(0, 3.5f, 0);
            cap.height = 7;
            cap.radius = 2.4f;
            var janus = jRoot.AddComponent<Janus>();
            janus.model = j.transform;
            janus.arena = arena;
            janus.faceLight = AddLight(jRoot.transform, "FaceLight", jRoot.transform.position + new Vector3(0, 8, -2.5f), new Color(0.5f, 0.8f, 1f), 3, 12);
            janus.backLight = AddLight(jRoot.transform, "BackLight", jRoot.transform.position + new Vector3(0, 8, 2.5f), new Color(1f, 0.3f, 0.2f), 3, 12);
            dir.janus = janus;
            var exit = RiftAt(root, 0, Heart.z + 14, 180, "betrayal_exit");
            exit.leadsTo = "Violence";
            exit.delay = 6;
            exit.membrane.sharedMaterial = Mat("RiftFire", "Crab/Rift");
            exit.glow.color = new Color(1f, 0.55f, 0.3f);
            dir.exit = exit;
            Ui<GameUI>();
            // the iron is baked near black: a lighter copy of its material, so the lamps have something to light
            var lighter = new Dictionary<Material, Material>();
            var ironMats = new HashSet<Material>(wall.GetComponentsInChildren<Renderer>().SelectMany(r => r.sharedMaterials));
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (!mats[i] || !ironMats.Contains(mats[i])) continue;
                    if (!lighter.TryGetValue(mats[i], out var m)) lighter[mats[i]] = m = Brighter(mats[i], "MechWall" + lighter.Count, 2.4f);
                    mats[i] = m;
                }
                r.sharedMaterials = mats;
            }
            SaveScene(scene, "Betrayal");
        }

        // ------------------------------------------------------------------ Violence
        // a battlefield: the crab comes from the south, the legion's forge to the west, the siege
        // engines in the middle and the east, the Warlord's field to the north
        static readonly Vector3 WarField = new Vector3(0, 0, 84);
        static readonly Vector2[] Craters = { new Vector2(12, -40), new Vector2(-20, -10), new Vector2(30, -18), new Vector2(-8, 22), new Vector2(26, 48), new Vector2(-34, 58), new Vector2(48, -50), new Vector2(-52, -46) };

        static float ViolenceHeight(float x, float z)
        {
            float h = (Mathf.PerlinNoise(x * 0.02f + 41, z * 0.02f + 9) - 0.5f) * 5f;
            h += (Mathf.PerlinNoise(x * 0.1f + 7, z * 0.1f) - 0.5f) * 0.6f;
            foreach (var c in Craters)
            {
                float d = new Vector2(x - c.x, z - c.y).magnitude;
                h -= Mathf.Clamp01(1 - d / 6f) * 2.2f;            // the pit
                h += Mathf.Clamp01(1 - Mathf.Abs(d - 6.5f) / 1.5f) * 0.6f;   // the rim
            }
            // a trench across the field
            float tr = Mathf.Abs(z - (5 + Mathf.Sin(x * 0.08f) * 4));
            if (Mathf.Abs(x) > 6) h -= Mathf.Clamp01(1 - tr / 1.6f) * 1.4f;
            float field = Mathf.Clamp01(1 - (new Vector2(x - WarField.x, z - WarField.z).magnitude - 22) / 6f);
            return Mathf.Lerp(h, 0, field);
        }

        static void BuildViolence()
        {
            var scene = NewScene("Violence");
            ground = ViolenceHeight;
            var (ac, an) = TextureGen.Ash();
            var mud = Mat("BattleMud", "Universal Render Pipeline/Lit", m =>
            {
                m.SetTexture("_BaseMap", ac);
                m.SetTexture("_BumpMap", an);
                m.EnableKeyword("_NORMALMAP");
                m.SetColor("_BaseColor", new Color(1.05f, 0.8f, 0.66f));
                m.SetFloat("_Smoothness", 0.2f);
            });
            var root = Group("Violence");
            Ground("BattleGround", new Rect(-110, -110, 220, 230), mud, 0.2f).transform.SetParent(root, false);
            var sun = Sun(new Color(1f, 0.66f, 0.42f), 1.8f, new Vector3(18, 160, 0));
            Environment(new Color(0.5f, 0.5f, 0.5f), Color.black, 1, new Color(0.45f, 0.38f, 0.38f), new Color(0.42f, 0.32f, 0.28f), new Color(0.16f, 0.12f, 0.1f), new Color(0.42f, 0.28f, 0.22f), 0.0065f, 0.04f);
            RenderSettings.skybox = Mat("Sky_Violence_Gradient", "Crab/Sky", m =>
            {
                m.SetFloat("_SunSize", 6);
                m.SetColor("_Top", new Color(0.18f, 0.1f, 0.1f));
                m.SetColor("_Horizon", new Color(0.62f, 0.32f, 0.2f));
                m.SetColor("_Bottom", new Color(0.2f, 0.1f, 0.08f));
                m.SetColor("_Sun", new Color(1f, 0.55f, 0.3f));
                m.SetVector("_SunDir", -sun.transform.forward);
            });
            Post("Violence", 0.55f, -2, new Color(1f, 0.9f, 0.82f), 1.1f, 0.35f, 16);

            // the debris of war
            var props = Group("Scenery", root);
            var barricade = Glb("World/barricade");
            var wreck = Glb("World/wreck");
            var banner = Glb("World/war_banner");
            for (int i = 0; i < 26; i++)
            {
                float x = R(-90, 90), z = R(-90, 100);
                if (new Vector2(x - WarField.x, z - WarField.z).magnitude < 26 || new Vector2(x, z + 80).magnitude < 10) continue;
                Place(barricade, props, x, z, R(0, 360), R(0.9f, 1.2f), collider: "box");
            }
            for (int i = 0; i < 16; i++)
            {
                float x = R(-85, 85), z = R(-85, 100);
                if (new Vector2(x - WarField.x, z - WarField.z).magnitude < 24) continue;
                var w = Place(wreck, props, x, z, R(0, 360), R(0.9f, 1.4f), collider: "box");
                var ms = w.AddComponent<MatterSource>();
                ms.kind = Matter.Metal;
                ms.amount = 8;
                ms.regrow = 60;
                ms.reach = 3.2f;
            }
            for (int i = 0; i < 18; i++)
            {
                float x = R(-90, 90), z = R(-60, 110);
                Place(banner, props, x, z, R(0, 360), R(0.9f, 1.3f), collider: "box");
            }
            foreach (var (x, z) in new[] { (-75f, 30f), (80f, 60f), (-70f, -70f), (85f, -40f), (0f, 120f) }) Place(Glb("World/machine_tower"), props, x, z, R(0, 360), R(1, 1.4f));

            // start, beacons, director
            var start = Group("Start", root);
            start.position = new Vector3(0, H(0, -82), -82);
            var hero = BuildHero(start.position, 0, out _);
            Setup("Violence", start);
            var dir = new GameObject("Violence").AddComponent<ViolenceDirector>();
            dir.beacons.Add(BeaconAt(root, 5, -78, -30, "violence_camp"));
            dir.beacons.Add(BeaconAt(root, -24, -36, 60, "violence_forge"));
            dir.beacons.Add(BeaconAt(root, 6, 56, 180, "violence_front"));

            // the legion's forge and, beside it, the place for the crab's factory
            var forgeGo = Place(Glb("World/workshop"), root, -32, -30, 90, 1.3f, collider: "none", sink: 0);
            var forge = forgeGo.AddComponent<Workshop>();
            forge.title = "свою кузницу";
            forge.cost = new Cost(0, 6, 12, 0);
            forge.build = CrabRig.Find(forgeGo.transform, "Build");
            forge.reach = 5;
            forge.area = 9;
            forge.forgeLight = AddLight(forgeGo.transform, "Forge", forgeGo.transform.position + Vector3.up * 1.4f, new Color(1f, 0.55f, 0.2f), 3, 10);
            dir.forge = forge;
            Place(Glb("World/machine_tower"), root, -42, -24, 0, 0.8f);
            var facGo = Place(Glb("World/factory"), root, -32, -46, 0, 1, sink: 0.05f);
            var fac = facGo.AddComponent<RobotFactory>();
            fac.door = CrabRig.Find(facGo.transform, "Door");
            dir.factory = fac;
            var guard = new GameObject("ForgeGuard").AddComponent<LegionBand>();
            guard.transform.SetPositionAndRotation(new Vector3(-24, 0, -32), Quaternion.Euler(0, 120, 0));
            guard.ranks = 2;
            guard.files = 2;
            guard.trigger = 18;
            dir.bands.Add(guard);

            // the siege engines, each with a band of the legion
            SiegeEngine Engine(string model, SiegeEngine.Kind kind, float x, float z, float yaw)
            {
                var go = Place(Glb("World/" + model), root, x, z, yaw, 1, collider: "none", sink: 0);
                var col = go.AddComponent<BoxCollider>();
                var b = Bounds(go);
                col.center = go.transform.InverseTransformPoint(b.center);
                col.size = b.size;
                var e = go.AddComponent<SiegeEngine>();
                e.name = model + $"_{x:0}_{z:0}";
                e.kind = kind;
                e.arm = CrabRig.Find(go.transform, "Arm");
                e.maxHealth = kind == SiegeEngine.Kind.Tower ? 600 : 400;
                var band = new GameObject("EngineGuard").AddComponent<LegionBand>();
                band.transform.SetPositionAndRotation(go.transform.position + go.transform.forward * 5, go.transform.rotation);
                band.ranks = 1;
                band.files = 3;
                band.trigger = 22;
                dir.bands.Add(band);
                return e;
            }
            dir.engines.Add(Engine("catapult", SiegeEngine.Kind.Catapult, 40, 12, 230));
            dir.engines.Add(Engine("catapult", SiegeEngine.Kind.Catapult, -46, 34, 150));
            var tower = Engine("siege_tower", SiegeEngine.Kind.Tower, 12, 46, 200);
            tower.rollTo = new Vector3(0, 0, 10);
            dir.engines.Add(tower);

            // the field's other demons
            foreach (var (x, z) in new[] { (-12f, -50f), (14f, -30f), (-6f, -6f), (20f, 26f), (-24f, 14f) })
            {
                var imp = (GameObject)PrefabUtility.InstantiatePrefab(lib.imp);
                imp.transform.position = new Vector3(x, H(x, z), z);
            }
            foreach (var (x, z) in new[] { (30f, -4f), (-30f, 50f) })
            {
                var br = (GameObject)PrefabUtility.InstantiatePrefab(lib.brute);
                br.transform.position = new Vector3(x, H(x, z), z);
            }

            // matter
            void Source(GameObject model, string variant, float x, float z, Matter kind, int amount, float regrow)
            {
                var go = variant != null ? Variant(model, variant, root, x, z, R(0, 360), 1, collider: "none") : Place(model, root, x, z, R(0, 360), 1, collider: "none", sink: 0.02f);
                var ms = go.AddComponent<MatterSource>();
                ms.kind = kind;
                ms.amount = amount;
                ms.regrow = regrow;
                ms.reach = 2.6f;
            }
            foreach (var (x, z) in new[] { (-6f, -74f), (8f, -66f), (-38f, -38f), (-26f, -22f), (-40f, -18f) }) Source(Glb("World/scrap"), null, x, z, Matter.Metal, 9, 45);
            foreach (var (x, z) in new[] { (6f, -76f), (-22f, -40f), (-36f, -44f), (10f, 0f) }) Source(Glb("World/rubble"), null, x, z, Matter.Mineral, 8, 45);
            foreach (var (x, z) in new[] { (-44f, -30f), (-20f, -48f), (20f, 60f) }) Source(Glb("World/crystal"), "Crystal2", x, z, Matter.Crystal, 8, 45);
            foreach (var (x, z) in new[] { (-28f, -54f), (-44f, -40f), (24f, -60f) }) Source(Glb("World/pylon"), null, x, z, Matter.Energy, 8, 40);
            foreach (var (x, z) in new[] { (-18f, -30f), (-40f, -50f) }) Source(Glb("World/gold_pile"), "Pile1", x, z, Matter.Gold, 8, 45);

            // the sky: two flying fortresses; the low one bombs when called
            var ff = Place(Glb("World/flying_fortress"), root, 0, 0, 0, 1.2f, collider: "none", sink: 0);
            var fort = ff.AddComponent<FlyingFortress>();
            fort.center = new Vector3(0, 0, 40);
            fort.radius = 70;
            fort.height = 45;
            fort.speed = 4;
            fort.every = 18;
            fort.bombing = false;
            ff.transform.position = fort.center + new Vector3(fort.radius, fort.height, 0);
            AddLight(ff.transform, "Belly", ff.transform.position + Vector3.down * 14, new Color(1f, 0.45f, 0.15f), 6, 40);
            dir.fortress = fort;
            var ff2 = Place(Glb("World/flying_fortress"), root, 0, 0, 0, 1.8f, collider: "none", sink: 0);
            var fort2 = ff2.AddComponent<FlyingFortress>();
            fort2.center = new Vector3(20, 0, 60);
            fort2.radius = 110;
            fort2.height = 70;
            fort2.speed = -3;
            ff2.transform.position = fort2.center + new Vector3(0, fort2.height, fort2.radius);

            // the Warlord's field
            var arena = Group("Arena", root);
            arena.position = new Vector3(WarField.x, H(WarField.x, WarField.z), WarField.z);
            for (int i = 0; i < 12; i++)
            {
                float a = i / 12f * Mathf.PI * 2;
                if (Mathf.Abs(Mathf.Sin(a) + 1) < 0.15f) continue;
                Place(banner, root, WarField.x + Mathf.Cos(a) * 24, WarField.z + Mathf.Sin(a) * 24, R(0, 360), 1.4f, collider: "box");
            }
            var w0 = (GameObject)PrefabUtility.InstantiatePrefab(lib.warlord);
            var wRoot = new GameObject("Warlord");
            wRoot.transform.SetPositionAndRotation(arena.position + Vector3.forward * 10, Quaternion.Euler(0, 180, 0));
            w0.transform.SetParent(wRoot.transform, false);
            var cap = wRoot.AddComponent<CapsuleCollider>();
            cap.center = new Vector3(0, 3.2f, 0);
            cap.height = 6.4f;
            cap.radius = 1.6f;
            var wl = wRoot.AddComponent<Warlord>();
            wl.model = w0.transform;
            wl.arena = arena;
            wl.fortress = fort;
            var auraGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            auraGo.name = "Aura";
            UnityEngine.Object.DestroyImmediate(auraGo.GetComponent<Collider>());
            auraGo.transform.SetParent(wRoot.transform, false);
            auraGo.transform.localPosition = Vector3.up * 3;
            auraGo.GetComponent<Renderer>().sharedMaterial = Mat("GuardAura", "Crab/Glow", m => { m.SetFloat("_Round", 0); m.SetFloat("_Rim", 1); m.SetColor("_Tint", new Color(1f, 0.25f, 0.15f, 0.5f)); });
            wl.aura = auraGo.transform;
            wl.auraLight = AddLight(wRoot.transform, "AuraLight", wRoot.transform.position + Vector3.up * 3, new Color(1f, 0.3f, 0.2f), 0, 14);
            dir.warlord = wl;
            var exit = RiftAt(root, 0, WarField.z + 16, 180, "violence_exit");
            exit.leadsTo = "Lies";
            exit.delay = 6;
            exit.membrane.sharedMaterial = Mat("RiftPale", "Crab/Rift");
            exit.glow.color = new Color(0.8f, 0.85f, 1f);
            dir.exit = exit;
            Ui<GameUI>();
            SaveScene(scene, "Violence");
        }

        // ------------------------------------------------------------------ Lies
        // islands of rock in a void: (center x, z, radius); strips of land join some of them, lying or
        // hidden bridges others, and one gap has no bridge at all
        static readonly Vector3[] LieIslands =
        {
            new Vector3(0, -40, 12), new Vector3(0, -12, 7), new Vector3(16, 8, 7), new Vector3(2, 30, 8),
            new Vector3(-14, 52, 7), new Vector3(-4, 74, 9), new Vector3(0, 106, 20),
        };
        static readonly (int, int)[] LieStrips = { (0, 1), (2, 3), (5, 6) };

        static float SegDist(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (p - (a + ab * t)).magnitude;
        }

        static float LiesHeight(float x, float z)
        {
            var p = new Vector2(x, z);
            float best = -60;
            foreach (var i in LieIslands)
            {
                float d = (p - new Vector2(i.x, i.y)).magnitude;
                float top = (Mathf.PerlinNoise(x * 0.15f + 5, z * 0.15f) - 0.5f) * 0.3f;
                if (d < i.z) best = Mathf.Max(best, top);
                else if (d < i.z + 3) best = Mathf.Max(best, Mathf.Lerp(top, -60, (d - i.z) / 3));
            }
            foreach (var (a, b) in LieStrips)
            {
                float d = SegDist(p, new Vector2(LieIslands[a].x, LieIslands[a].y), new Vector2(LieIslands[b].x, LieIslands[b].y));
                if (d < 2.2f) best = Mathf.Max(best, 0);
                else if (d < 4) best = Mathf.Max(best, Mathf.Lerp(0, -60, (d - 2.2f) / 1.8f));
            }
            return best;
        }

        static void BuildLies()
        {
            var scene = NewScene("Lies");
            ground = LiesHeight;
            var (pc, pn) = TextureGen.Paving();
            var floor = Mat("LieFloor", "Universal Render Pipeline/Lit", m =>
            {
                m.SetTexture("_BaseMap", pc);
                m.SetTexture("_BumpMap", pn);
                m.EnableKeyword("_NORMALMAP");
                m.SetColor("_BaseColor", new Color(0.85f, 0.78f, 1.15f));
                m.SetFloat("_Smoothness", 0.55f);
            });
            var root = Group("Lies");
            Ground("LieGround", new Rect(-40, -60, 80, 192), floor, 0.25f).transform.SetParent(root, false);
            var sun = Sun(new Color(0.9f, 0.85f, 1f), 1.4f, new Vector3(50, 60, 0));
            Environment(new Color(0.5f, 0.5f, 0.5f), Color.black, 1, new Color(0.55f, 0.5f, 0.75f), new Color(0.5f, 0.42f, 0.6f), new Color(0.15f, 0.12f, 0.25f), new Color(0.35f, 0.28f, 0.5f), 0.011f, 0.04f);
            RenderSettings.skybox = Mat("Sky_Lies_Gradient", "Crab/Sky", m =>
            {
                m.SetFloat("_SunSize", 6);
                m.SetColor("_Top", new Color(0.1f, 0.3f, 0.35f));
                m.SetColor("_Horizon", new Color(0.7f, 0.35f, 0.65f));
                m.SetColor("_Bottom", new Color(0.05f, 0.03f, 0.1f));
                m.SetColor("_Sun", new Color(1f, 0.9f, 0.7f));
                m.SetVector("_SunDir", -sun.transform.forward);
                m.SetFloat("_Exponent", 0.4f);
            });
            Post("Lies", 0.3f, 12, new Color(1f, 0.95f, 1.05f), 1.2f, 0.32f, 10);

            // the bridges: lying ones you fall through, hidden ones that hold
            var bridgeMat = Mat("LieBridge", "Universal Render Pipeline/Lit", m =>
            {
                m.SetColor("_BaseColor", new Color(0.8f, 0.78f, 0.9f));
                m.SetFloat("_Smoothness", 0.6f);
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Color.black);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            });
            lib.bridge = bridgeMat;
            EditorUtility.SetDirty(lib);
            void Bridge(Vector3 a, Vector3 b, Illusion.Kind? kind)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = kind == null ? "Bridge" : kind == Illusion.Kind.False ? "FalseBridge" : "HiddenBridge";
                go.transform.SetParent(root, false);
                var mid = (a + b) / 2;
                go.transform.SetPositionAndRotation(new Vector3(mid.x, -0.15f, mid.z), Quaternion.LookRotation(b - a));
                go.transform.localScale = new Vector3(2.4f, 0.3f, (b - a).magnitude);
                go.GetComponent<Renderer>().sharedMaterial = bridgeMat;
                if (kind != null) go.AddComponent<Illusion>().kind = kind.Value;
            }
            Vector3 Edge(int i, int j, float side = 0)
            {
                var a = new Vector3(LieIslands[i].x, 0, LieIslands[i].y);
                var b = new Vector3(LieIslands[j].x, 0, LieIslands[j].y);
                var d = (b - a).normalized;
                return a + d * (LieIslands[i].z - 0.5f) + Vector3.Cross(Vector3.up, d) * side;
            }
            // 1 -> 2: the straight bridge lies; the true one is hidden beside it
            Bridge(Edge(1, 2), Edge(2, 1), Illusion.Kind.False);
            Bridge(Edge(1, 2, -4), Edge(2, 1, 4), Illusion.Kind.Hidden);
            // 4 -> 5: the same, the other way round
            Bridge(Edge(4, 5, 3.5f), Edge(5, 4, -3.5f), Illusion.Kind.False);
            Bridge(Edge(4, 5), Edge(5, 4), Illusion.Kind.Hidden);
            // 3 -> 4: nothing at all — the crab builds it

            // bits of the past circles adrift in the void, trading places
            var shift = new GameObject("WorldShift").AddComponent<WorldShift>();
            var rocks = Glb("World/beach_rocks");
            var pieces = new[] { "palm", "house0", "gold_house", "pavilion", "burnt_house", "blossom_tree", "mech_wall", "deadtree", "obelisk", "column", "spring", "burnt_tree" };
            for (int i = 0; i < pieces.Length * 2; i++)
            {
                float a = i / (pieces.Length * 2f) * Mathf.PI * 2 + R(-0.1f, 0.1f);
                float rr = R(34, 60);
                var p = new Vector3(Mathf.Cos(a) * rr, R(-14, 10), 30 + Mathf.Sin(a) * rr * 1.4f);
                var holder = new GameObject("Fragment").transform;
                holder.SetParent(root, false);
                holder.position = p;
                holder.rotation = Quaternion.Euler(R(-8, 8), R(0, 360), R(-8, 8));
                var rock = (GameObject)PrefabUtility.InstantiatePrefab(rocks, holder);
                foreach (Transform c in rock.transform.Cast<Transform>().ToArray()) if (c.name != "Rock1") UnityEngine.Object.DestroyImmediate(c.gameObject);
                rock.transform.localScale = new Vector3(5, 2.5f, 5);
                rock.transform.localRotation = Quaternion.Euler(180, 0, 0);
                var thing = (GameObject)PrefabUtility.InstantiatePrefab(Glb("World/" + pieces[i % pieces.Length]), holder);
                thing.transform.localPosition = Vector3.zero;
                if (pieces[i % pieces.Length] == "palm" || pieces[i % pieces.Length] == "obelisk") thing.transform.localScale = Vector3.one * 0.8f;
                shift.pieces.Add(holder);
            }

            // start, beacons, director
            var start = Group("Start", root);
            start.position = new Vector3(0, H(0, -46), -46);
            var hero = BuildHero(start.position, 0, out _);
            Setup("Lies", start);
            var dir = new GameObject("Lies").AddComponent<LiesDirector>();
            dir.beacons.Add(BeaconAt(root, 5, -42, -30, "lies_start"));
            dir.beacons.Add(BeaconAt(root, 6, 30, 200, "lies_middle"));
            dir.beacons.Add(BeaconAt(root, 2, 70, 180, "lies_archive"));
            var gap = Group("Gap", root);
            gap.position = new Vector3(-2, 0, 36);
            dir.gap = gap;
            var doubt = Group("Doubt", root);
            doubt.position = new Vector3(LieIslands[4].x, 0, LieIslands[4].y);
            dir.doubt = doubt;
            // a little of each island from a past circle
            Place(Glb("World/palm"), root, -6, -44, 30, 1, collider: "box");
            Place(Glb("World/house1"), root, 7, -36, -40, 0.8f, sink: 0.1f);
            Place(Glb("World/gold_column"), root, 18, 11, 0, 0.8f);
            Place(Glb("World/pavilion"), root, -16, 54, 0, 0.7f, sink: 0.05f);

            // matter — and the heaps that are not what they seem
            void Source(GameObject model, string variant, float x, float z, Matter kind, int amount, float regrow)
            {
                var go = variant != null ? Variant(model, variant, root, x, z, R(0, 360), 1, collider: "none") : Place(model, root, x, z, R(0, 360), 1, collider: "none", sink: 0.02f);
                var ms = go.AddComponent<MatterSource>();
                ms.kind = kind;
                ms.amount = amount;
                ms.regrow = regrow;
                ms.reach = 2.6f;
            }
            var scrapM = Glb("World/scrap");
            foreach (var (x, z) in new[] { (-5f, -36f), (5f, -48f), (4f, 26f), (-2f, 76f) }) Source(scrapM, null, x, z, Matter.Metal, 9, 40);
            foreach (var (x, z) in new[] { (3f, -42f), (-4f, 34f), (-12f, 48f) }) Source(Glb("World/rubble"), null, x, z, Matter.Mineral, 9, 40);
            foreach (var (x, z) in new[] { (-6f, -46f), (14f, 4f), (-6f, 70f) }) Source(Glb("World/crystal"), "Crystal0", x, z, Matter.Crystal, 8, 40);
            foreach (var (x, z) in new[] { (6f, -38f), (18f, 12f), (0f, 34f) }) Source(Glb("World/pylon"), null, x, z, Matter.Energy, 8, 40);
            foreach (var (x, z) in new[] { (-2f, 24f), (7f, 32f), (-17f, 55f) })
            {
                var mm = (GameObject)PrefabUtility.InstantiatePrefab(lib.mimic);
                mm.transform.position = new Vector3(x, H(x, z), z);
                var dis = (GameObject)PrefabUtility.InstantiatePrefab(scrapM, mm.transform);
                dis.name = "Disguise";
                mm.GetComponent<Mimic>().disguise = dis.transform;
            }
            foreach (var (x, z) in new[] { (14f, 12f), (-12f, 50f) })
            {
                var w = (GameObject)PrefabUtility.InstantiatePrefab(lib.envious);
                w.transform.position = new Vector3(x, H(x, z), z);
            }

            // the archive: a shard of memory that remembers the mission's writing
            var arch = Place(Glb("World/memory_shard"), root, LieIslands[5].x, LieIslands[5].y, 180, 1.3f);
            var shard = arch.AddComponent<MemoryShard>();
            shard.id = "archive";
            shard.reach = 3;
            shard.lines = new[]
            {
                "Архив. Записи о носителях Искры: рыцарь, птица, машина… краб.",
                "«Миссия: дойти до трона Ада». Подпись… стёрта. Это писал не Бог.",
                "«Носители — расходный материал. Цель — открыть трон». Открыть — для кого?",
            };
            var fr = Variant(Glb("World/fresco"), "Fresco2", root, LieIslands[5].x, LieIslands[5].y + 5, 180, 1.1f);
            shard.fresco = fr.transform;
            shard.frescoLight = AddLight(fr.transform, "FrescoLight", fr.transform.position + fr.transform.forward * 4 + Vector3.up * 4, new Color(1f, 0.88f, 0.7f), 6, 12);
            shard.frescoLight.enabled = false;
            AddLight(arch.transform, "ArchiveGlow", arch.transform.position + Vector3.up * 2, new Color(1f, 0.9f, 0.7f), 2, 8);
            dir.archive = shard;

            // the Father of Lies on the last island
            var arena = Group("Arena", root);
            arena.position = new Vector3(LieIslands[6].x, H(LieIslands[6].x, LieIslands[6].y), LieIslands[6].y);
            var l0 = (GameObject)PrefabUtility.InstantiatePrefab(lib.liar);
            var lRoot = new GameObject("FatherOfLies");
            lRoot.transform.SetPositionAndRotation(arena.position + Vector3.forward * 8, Quaternion.Euler(0, 180, 0));
            l0.transform.SetParent(lRoot.transform, false);
            var cap = lRoot.AddComponent<CapsuleCollider>();
            cap.center = new Vector3(0, 2.2f, 0);
            cap.height = 4.4f;
            cap.radius = 0.8f;
            var father = lRoot.AddComponent<FatherOfLies>();
            father.model = l0.transform;
            father.arena = arena;
            father.truthLight = AddLight(lRoot.transform, "Truth", lRoot.transform.position + Vector3.up * 3, new Color(0.4f, 0.9f, 1f), 0, 8);
            dir.father = father;
            var exit = RiftAt(root, 0, LieIslands[6].y + 14, 180, "lies_exit");
            exit.leadsTo = "Power";
            exit.delay = 6;
            exit.membrane.sharedMaterial = Mat("RiftGold", "Crab/Rift");
            exit.glow.color = new Color(1f, 0.85f, 0.4f);
            dir.exit = exit;
            Ui<GameUI>();
            SaveScene(scene, "Lies");
        }

        // ------------------------------------------------------------------ Power
        // the last circle: a platform in the south, the great bridge over the abyss, the gate of the
        // citadel, the throne hall; the underside of the fortress of hell for a sky
        static readonly Vector3 ThroneHall = new Vector3(0, 0, 86);

        static float PowerHeight(float x, float z)
        {
            float h = -50;
            if (new Vector2(x, z + 88).magnitude < 18) h = 0;                       // the platform
            if (Mathf.Abs(x) < 7 && z > -75 && z < 62) h = 0;                       // the bridge
            if (new Vector2(x - ThroneHall.x, z - ThroneHall.z).magnitude < 26) h = 0; // the hall
            if (Mathf.Abs(x) < 26 && z > 98 && z < 112) h = 0;                       // the throne's dais
            return h;
        }

        static void BuildPower()
        {
            var scene = NewScene("Power");
            ground = PowerHeight;
            var (cc, cn) = TextureGen.Cave();
            var stone = Mat("HellStone", "Universal Render Pipeline/Lit", m =>
            {
                m.SetTexture("_BaseMap", cc);
                m.SetTexture("_BumpMap", cn);
                m.EnableKeyword("_NORMALMAP");
                m.SetColor("_BaseColor", new Color(1.6f, 0.9f, 0.85f));
                m.SetFloat("_Smoothness", 0.4f);
            });
            var root = Group("Power");
            Ground("PowerGround", new Rect(-40, -110, 80, 230), stone, 0.25f).transform.SetParent(root, false);
            var lavaGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            lavaGo.name = "Abyss";
            UnityEngine.Object.DestroyImmediate(lavaGo.GetComponent<Collider>());
            lavaGo.transform.SetParent(root, false);
            lavaGo.transform.SetPositionAndRotation(new Vector3(0, -40, 0), Quaternion.Euler(90, 0, 0));
            lavaGo.transform.localScale = new Vector3(800, 800, 1);
            lavaGo.GetComponent<Renderer>().sharedMaterial = Mat("Lava", "Crab/Lava");
            var sun = Sun(new Color(1f, 0.55f, 0.4f), 1.3f, new Vector3(55, 200, 0));
            Environment(new Color(0.5f, 0.5f, 0.5f), Color.black, 1, new Color(0.7f, 0.38f, 0.32f), new Color(0.6f, 0.32f, 0.25f), new Color(0.35f, 0.15f, 0.1f), new Color(0.3f, 0.1f, 0.06f), 0.009f, 0.03f);
            RenderSettings.skybox = Mat("Sky_Power_Gradient", "Crab/Sky", m =>
            {
                m.SetFloat("_SunSize", 6);
                m.SetColor("_Top", new Color(0.04f, 0.01f, 0.01f));
                m.SetColor("_Horizon", new Color(0.35f, 0.08f, 0.04f));
                m.SetColor("_Bottom", new Color(0.3f, 0.08f, 0.02f));
                m.SetColor("_Sun", Color.black);
            });
            Post("Power", 0.6f, 5, new Color(1f, 0.85f, 0.8f), 1.3f, 0.4f, 16);
            var ceiling = Place(Glb("World/hell_ceiling"), root, 0, 10, 0, 1.3f, collider: "none", sink: 0);
            ceiling.transform.position = new Vector3(0, 60, 10);
            foreach (var (x, z, s) in new[] { (-40f, -40f, 1.2f), (42f, -20f, 1f), (-46f, 30f, 1.4f), (48f, 50f, 1.1f), (-30f, 110f, 1.3f), (34f, 120f, 1.2f), (0f, 140f, 1.6f), (-60f, 80f, 1f), (60f, 90f, 1f) })
            {
                var sp = Place(Glb("World/hell_spire"), root, x, z, R(0, 360), s, collider: "none", sink: 0);
                sp.transform.position = new Vector3(x, -45, z);
            }

            // start, beacons, director
            var start = Group("Start", root);
            start.position = new Vector3(0, 0, -92);
            var hero = BuildHero(start.position, 0, out _);
            Setup("Power", start);
            var dir = new GameObject("Power").AddComponent<PowerDirector>();
            dir.beacons.Add(BeaconAt(root, 6, -88, -30, "power_start"));
            dir.beacons.Add(BeaconAt(root, 4, 30, 180, "power_bridge"));
            var maker = new GameObject("ArmyMaker").AddComponent<RobotFactory>();
            maker.transform.SetPositionAndRotation(new Vector3(0, 0, -98), Quaternion.identity);
            maker.company = 12;
            dir.armyMaker = maker;
            dir.ceiling = ceiling.transform;

            // matter for the last forging, on the platform
            void Source(GameObject model, string variant, float x, float z, Matter kind, int amount, float regrow)
            {
                var go = variant != null ? Variant(model, variant, root, x, z, R(0, 360), 1, collider: "none") : Place(model, root, x, z, R(0, 360), 1, collider: "none", sink: 0.02f);
                var ms = go.AddComponent<MatterSource>();
                ms.kind = kind;
                ms.amount = amount;
                ms.regrow = regrow;
                ms.reach = 2.6f;
            }
            foreach (var (x, z) in new[] { (-10f, -84f), (10f, -84f), (-12f, -94f) }) Source(Glb("World/planks"), null, x, z, Matter.Organic, 12, 30);
            foreach (var (x, z) in new[] { (-6f, -80f), (12f, -92f), (-14f, -88f) }) Source(Glb("World/rubble"), null, x, z, Matter.Mineral, 14, 30);
            foreach (var (x, z) in new[] { (6f, -80f), (14f, -88f), (-8f, -100f), (8f, -100f) }) Source(Glb("World/scrap"), null, x, z, Matter.Metal, 14, 30);
            foreach (var (x, z) in new[] { (-4f, -102f), (4f, -76f) }) Source(Glb("World/gold_pile"), "Pile1", x, z, Matter.Gold, 14, 30);
            foreach (var (x, z) in new[] { (-14f, -80f), (14f, -98f) }) Source(Glb("World/crystal"), "Crystal2", x, z, Matter.Crystal, 14, 30);
            foreach (var (x, z) in new[] { (-10f, -98f), (10f, -78f), (0f, -104f) }) Source(Glb("World/pylon"), null, x, z, Matter.Energy, 14, 30);

            // the last legions on the bridge
            foreach (var z in new[] { -50f, -20f, 10f, 34f })
            {
                var band = new GameObject("Legion").AddComponent<LegionBand>();
                band.transform.SetPositionAndRotation(new Vector3(0, 0, z), Quaternion.Euler(0, 180, 0));
                band.ranks = 2;
                band.files = 3;
                band.trigger = 20;
                dir.bands.Add(band);
            }
            foreach (var (x, z) in new[] { (-3f, -36f), (3f, -4f), (-3f, 22f) })
            {
                var br = (GameObject)PrefabUtility.InstantiatePrefab(lib.brute);
                br.transform.position = new Vector3(x, 0, z);
            }
            foreach (var z in new[] { -60f, -30f, 0f, 30f })
                foreach (var sx in new[] { -1f, 1f })
                    Place(Glb("World/war_banner"), root, sx * 6.2f, z, 0, 1.2f, collider: "box");

            // the gate: the army stays here
            var gateGo = Place(Glb("World/citadel_gate"), root, 0, 56, 180, 0.9f, collider: "none", sink: 0);
            foreach (var mf in gateGo.GetComponentsInChildren<MeshFilter>())
                if (!mf.name.StartsWith("Glow")) mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
            dir.gate = gateGo.transform;
            AddLight(gateGo.transform, "Barrier", gateGo.transform.position + Vector3.up * 5, new Color(1f, 0.45f, 0.25f), 4, 20);

            // the throne hall: four pillars of the circles, the throne, the Lord
            var arena = Group("Arena", root);
            arena.position = ThroneHall;
            var hl = (GameObject)PrefabUtility.InstantiatePrefab(lib.hellLord);
            var throne = Place(Glb("World/hell_throne"), root, 0, 108, 180, 1, sink: 0);
            var lRoot = new GameObject("HellLord");
            lRoot.transform.SetPositionAndRotation(new Vector3(0, 1.6f, 100), Quaternion.Euler(0, 180, 0));
            hl.transform.SetParent(lRoot.transform, false);
            var cap = lRoot.AddComponent<CapsuleCollider>();
            cap.center = new Vector3(0, 7, 0);
            cap.height = 14;
            cap.radius = 3;
            var lord = lRoot.AddComponent<HellLord>();
            lord.model = hl.transform;
            lord.arena = arena;
            lord.crownLight = AddLight(lRoot.transform, "Crown", lRoot.transform.position + Vector3.up * 14, new Color(1f, 0.6f, 0.25f), 5, 30);
            var sh = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sh.name = "LordShield";
            UnityEngine.Object.DestroyImmediate(sh.GetComponent<Collider>());
            sh.transform.SetParent(lRoot.transform, false);
            sh.transform.localPosition = Vector3.up * 7;
            sh.GetComponent<Renderer>().sharedMaterial = Mat("LordShield", "Crab/Glow", m => { m.SetFloat("_Round", 0); m.SetFloat("_Rim", 1); m.SetColor("_Tint", new Color(1f, 0.6f, 0.25f, 0.45f)); });
            lord.shield = sh.transform;
            var colors = new[] { new Color(0.5f, 1f, 0.7f), new Color(1f, 0.5f, 0.15f), new Color(1f, 0.82f, 0.3f), new Color(0.75f, 0.5f, 1f) };
            for (int i = 0; i < 4; i++)
            {
                float a = (45 + i * 90) * Mathf.Deg2Rad;
                var p = ThroneHall + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 15;
                var pg = Place(Glb("World/circle_pillar"), root, p.x, p.z, R(0, 360), 1);
                var pil = pg.AddComponent<CirclePillar>();
                pil.color = colors[i];
                var col = pg.AddComponent<CapsuleCollider>();
                col.center = new Vector3(0, 5, 0);
                col.height = 10;
                col.radius = 1.2f;
                pil.glow = AddLight(pg.transform, "PillarGlow", pg.transform.position + Vector3.up * 6, colors[i], 12, 14);
                foreach (var r in pg.GetComponentsInChildren<Renderer>())
                    if (r.name.StartsWith("Glow")) r.sharedMaterial = Mat($"PillarBand{i}", "Crab/Glow", m => { m.SetFloat("_Round", 0); m.SetFloat("_Rim", 0); m.SetColor("_Tint", colors[i]); });
                lord.pillars.Add(pil);
            }
            dir.lord = lord;
            var portal = RiftAt(root, 0, ThroneHall.z + 6, 180, "power_portal");
            portal.leadsTo = "Epilogue";
            portal.delay = 2;
            portal.membrane.sharedMaterial = Mat("RiftSpace", "Crab/Rift", m => { m.SetColor("_Color", new Color(0.6f, 0.8f, 1f)); m.SetColor("_Core", new Color(0.0f, 0.02f, 0.08f)); });
            portal.glow.color = new Color(0.6f, 0.8f, 1f);
            dir.portal = portal;
            Ui<GameUI>();
            SaveScene(scene, "Power");
        }

        // ------------------------------------------------------------------ the end
        static void BuildEpilogue()
        {
            var scene = NewScene("Epilogue");
            ground = (x, z) => -200;
            var root = Group("Space");
            Environment(new Color(0.5f, 0.5f, 0.5f), Color.black, 1, new Color(0.25f, 0.28f, 0.4f), new Color(0.2f, 0.2f, 0.28f), new Color(0.08f, 0.08f, 0.1f), Color.black, 0.0f);
            RenderSettings.fog = false;
            Sun(new Color(1f, 0.97f, 0.9f), 2f, new Vector3(15, 35, 0));
            RenderSettings.skybox = Mat("Sky_Space_Gradient", "Crab/Sky", m =>
            {
                m.SetFloat("_SunSize", 6);
                m.SetColor("_Top", new Color(0.0f, 0.0f, 0.02f));
                m.SetColor("_Horizon", new Color(0.02f, 0.03f, 0.08f));
                m.SetColor("_Bottom", new Color(0.0f, 0.0f, 0.02f));
                m.SetColor("_Sun", Color.black);
            });
            Post("Epilogue", 0.2f, 10, Color.white, 1.4f, 0.35f, 10);
            // stars
            var stars = new GameObject("Stars");
            stars.transform.SetParent(root, false);
            var ps = stars.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = 1000;
            main.startSpeed = 0;
            main.startSize = new ParticleSystem.MinMaxCurve(4f, 12f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.8f, 0.85f, 1f), new Color(1f, 0.9f, 0.75f));
            main.maxParticles = 4000;
            var em = ps.emission;
            em.rateOverTime = 0;
            em.SetBursts(new[] { new ParticleSystem.Burst(0, 4000) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 6000;
            shape.radiusThickness = 0.1f;
            stars.GetComponent<ParticleSystemRenderer>().sharedMaterial = lib.particleAdditive;
            // the stone the crab stands on, the broken portal of hell behind it
            var stoneGo = Variant(Glb("World/beach_rocks"), "Rock2", root, 0, 0, 0, 1, collider: "none");
            stoneGo.transform.position = new Vector3(0, -1.2f, 0);
            stoneGo.transform.localScale = new Vector3(6, 2, 6);
            var plat = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            plat.name = "Footing";
            plat.transform.SetParent(root, false);
            plat.transform.position = new Vector3(0, -0.5f, 0);
            plat.transform.localScale = new Vector3(6, 0.5f, 6);
            plat.GetComponent<Renderer>().enabled = false;
            var rift = RiftAt(root, 0, -8, 0, "epilogue_portal");
            rift.transform.position = new Vector3(0, 0, -8);
            rift.transform.localScale = Vector3.one * 2;
            rift.membrane.sharedMaterial = Mat("RiftFire", "Crab/Rift");
            rift.open = false;
            rift.leadsTo = "";
            // the Earth before it, a moon, and thousands of worlds beyond
            var earth = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            earth.name = "Earth";
            UnityEngine.Object.DestroyImmediate(earth.GetComponent<Collider>());
            earth.transform.SetParent(root, false);
            earth.transform.position = new Vector3(0, 8, 120);
            earth.transform.localScale = Vector3.one * 70;
            earth.transform.rotation = Quaternion.Euler(-20, 0, 15);
            var planetTex = TextureGen.Planet();
            earth.GetComponent<Renderer>().sharedMaterial = Mat("Earth", "Universal Render Pipeline/Lit", m => { m.SetTexture("_BaseMap", planetTex); m.SetFloat("_Smoothness", 0.35f); });
            var moon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            moon.name = "Moon";
            UnityEngine.Object.DestroyImmediate(moon.GetComponent<Collider>());
            moon.transform.SetParent(root, false);
            moon.transform.position = new Vector3(-70, 40, 170);
            moon.transform.localScale = Vector3.one * 16;
            moon.GetComponent<Renderer>().sharedMaterial = Mat("Moon", "Universal Render Pipeline/Lit", m => m.SetColor("_BaseColor", new Color(0.65f, 0.65f, 0.7f)));
            var worlds = Group("Worlds", root);
            var palette = new[] { new Color(0.8f, 0.45f, 0.25f), new Color(0.35f, 0.6f, 0.9f), new Color(0.5f, 0.8f, 0.45f), new Color(0.85f, 0.75f, 0.45f), new Color(0.6f, 0.4f, 0.8f), new Color(0.9f, 0.9f, 0.95f), new Color(0.3f, 0.75f, 0.7f) };
            var mats = new Material[palette.Length];
            for (int i = 0; i < palette.Length; i++)
            {
                var c = palette[i];
                mats[i] = Mat($"World{i}", "Universal Render Pipeline/Lit", m =>
                {
                    m.SetColor("_BaseColor", c);
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", c * 0.25f);
                    m.SetFloat("_Smoothness", 0.3f);
                });
            }
            for (int i = 0; i < 900; i++)
            {
                var w = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                w.name = "world";
                UnityEngine.Object.DestroyImmediate(w.GetComponent<Collider>());
                w.transform.SetParent(worlds, false);
                var dirv = UnityEngine.Random.onUnitSphere;
                if (dirv.z < -0.3f) dirv.z = -dirv.z;   // mostly beyond the Earth, ahead and around
                w.transform.position = dirv * R(1500, 4000);
                w.transform.localScale = Vector3.one * R(15, 90);
                w.GetComponent<Renderer>().sharedMaterial = mats[rnd.Next(mats.Length)];
                w.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            var start = Group("Start", root);
            start.position = new Vector3(0, 0, 2);
            var hero = BuildHero(start.position, 0, out var camRig);
            camRig.cam.farClipPlane = 9000;
            Setup("Epilogue", start);
            var dir = new GameObject("Epilogue").AddComponent<EpilogueDirector>();
            dir.earth = earth.transform;
            dir.worlds = worlds;
            Ui<GameUI>();
            SaveScene(scene, "Epilogue");
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
