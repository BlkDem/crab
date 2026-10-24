// The crab as a whole: its state lives in the session (health, matter, items), this ties it
// to the body — taking blows (a moment of invulnerability, knockback, a flinch), falling and
// coming back at the last beacon, and the glow of the Spark once it has struck.
using System;
using Crab.Core;
using UnityEngine;

namespace Crab.Game
{
    public sealed class Hero : MonoBehaviour, IHittable
    {
        public static Hero Instance { get; private set; }
        public CrabController Controller;
        public CrabRig Rig;
        public CameraRig Cam;
        public HeroCombat Combat;
        public MatterPower Power;
        [Tooltip("Where streams of matter flow into (inside the shell)")]
        public Transform Core;
        public Light sparkLight;

        public GameState State => GameSession.Instance.State;
        public bool Alive => !State.Health.Dead;
        public event Action Died, Respawned;
        public bool Invulnerable { get; set; }
        float iframes;

        void Awake()
        {
            Instance = this;
            GameSession.Ensure();
        }

        void Start()
        {
            ShowSpark(State.Flag("spark"));
            State.Items.Changed += RefreshGear;
            RefreshGear();
        }

        void OnDestroy()
        {
            if (GameSession.Instance) State.Items.Changed -= RefreshGear;
        }

        GameObject armor;
        ItemId armorId;
        public Drone Drone { get; private set; }
        public TankBot Tank { get; private set; }
        public EnergyShield Shield { get; private set; }
        public PortalStep Portal { get; private set; }
        GameObject core;
        float regen;

        /// <summary>What the crab wears and who goes with it: armour on the shell (heavy over scrap),
        /// the shield, the drone, the TANK.</summary>
        void RefreshGear()
        {
            var lib = Library.Current;
            var wear = State.Items.Has(ItemId.HeavyArmor) ? ItemId.HeavyArmor : State.Items.Has(ItemId.ScrapArmor) ? ItemId.ScrapArmor : ItemId.None;
            if (armor && armorId != wear) Destroy(armor);
            var model = !lib ? null : wear == ItemId.HeavyArmor ? lib.heavyArmor : wear == ItemId.ScrapArmor ? lib.armor : null;
            if (wear != ItemId.None && (!armor || armorId != wear) && model && Rig.body)
            {
                armor = Instantiate(model, Rig.body, false);
                armor.name = "Armor";
                armorId = wear;
                foreach (var c in armor.GetComponentsInChildren<Collider>()) Destroy(c);
            }
            if (State.Items.Has(ItemId.EnergyShield) && !Shield)
            {
                Shield = gameObject.AddComponent<EnergyShield>();
                Shield.hero = this;
            }
            if (State.Items.Has(ItemId.Portal) && !Portal)
            {
                Portal = gameObject.AddComponent<PortalStep>();
                Portal.hero = this;
            }
            // the Spark's core: a crystal crown on the shell, violet light
            if (State.Mage && !core && lib && lib.sparkCore && Rig.body)
            {
                core = Instantiate(lib.sparkCore, Rig.body, false);
                core.name = "SparkCore";
                foreach (var c in core.GetComponentsInChildren<Collider>()) Destroy(c);
                if (sparkLight) sparkLight.color = new Color(0.8f, 0.6f, 1f);
            }
            if (State.Items.Has(ItemId.Tank) && !Tank && lib && lib.tank)
            {
                var go = Instantiate(lib.tank, transform.position - transform.forward * 2 + transform.right, transform.rotation);
                go.name = "TANK";
                Tank = go.GetComponent<TankBot>();
                if (Tank) Tank.hero = this;
            }
            bool fly = State.Items.Has(ItemId.ScoutDrone);
            if (fly && !Drone && lib && lib.drone)
            {
                var go = Instantiate(lib.drone, transform.position + Vector3.up * 1.2f + transform.right * 0.6f, Quaternion.identity);
                go.name = "SCOUT";
                foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
                Drone = go.AddComponent<Drone>();
                Drone.hero = this;
            }
        }

        /// <summary>The armour takes a share of every blow.</summary>
        public float Armor => State.Items.Has(ItemId.HeavyArmor) ? 0.45f : State.Items.Has(ItemId.ScrapArmor) ? 0.65f : 1f;

        public void ShowSpark(bool on)
        {
            if (sparkLight) sparkLight.enabled = on;
        }

        void Update()
        {
            iframes -= Time.deltaTime;
            // in the crab's own eyes the armour would sit over the camera
            if (armor && armor.activeSelf == Combat.firstPerson) armor.SetActive(!Combat.firstPerson);
            if (core && core.activeSelf == Combat.firstPerson) core.SetActive(!Combat.firstPerson);
            // the mage draws energy out of the air, slowly
            if (State.Mage && Alive)
            {
                regen += Time.deltaTime;
                if (regen > 5) { regen = 0; State.Matter.Add(Matter.Energy, 1); }
            }
            if (sparkLight && sparkLight.enabled) sparkLight.intensity = 0.6f + 0.25f * Mathf.Sin(Time.time * 3);
            if (transform.position.y < -30 && Alive) Hurt(1000, transform.position, Vector3.zero);
        }

        public void Hit(float damage, Vector3 point, Vector3 dir, GameObject by)
        {
            if (by == gameObject) return;
            Hurt(damage, point, dir);
        }

        public void Hurt(float damage, Vector3 point, Vector3 dir)
        {
            if (!Alive || iframes > 0 || Invulnerable) return;
            if (Shield && Shield.Absorb(point)) return;
            iframes = 0.45f;
            bool died = State.Health.Hurt(damage * Armor);
            dir.y = 0;
            Controller.Knock(dir.normalized * (died ? 2 : 4) + Vector3.up * 0.5f);
            Rig.Play(ClawPose.Hurt, 0.4f);
            Cam.Shake(0.6f);
            Sfx.Play("hurt", transform.position, 0.8f);
            Fx.Burst(point, new Color(1, 0.6f, 0.3f), 10, 2, 0.06f, 0.4f, false, 1);
            if (died) Die();
        }

        void Die()
        {
            Controller.frozen = true;
            Rig.Hold(ClawPose.Hurt);
            Sfx.Play("die", transform.position);
            Died?.Invoke();
        }

        /// <summary>Back at the last beacon (or the scene's start), whole again; matter and things are kept.</summary>
        public void Respawn()
        {
            var setup = FindAnyObjectByType<SceneSetup>();
            Transform at = setup ? setup.start : null;
            foreach (var b in FindObjectsByType<Beacon>())
                if (b.id == State.Checkpoint) at = b.transform;
            var pos = at ? at.position + at.forward * 1.6f : Vector3.zero;
            Controller.Teleport(pos, at ? at.eulerAngles.y : 0);
            // a boss fight may have left the view in the crab's eyes
            Combat.firstPerson = false;
            Controller.faceView = false;
            Cam.SetMode(CamMode.Third);
            Cam.Face(at ? at.eulerAngles.y : 0);
            State.Health.Reset(100);
            Controller.frozen = false;
            Rig.Release(ClawPose.Hurt);
            iframes = 2;
            Respawned?.Invoke();
        }
    }
}
