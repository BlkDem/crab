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
        public Drone Drone { get; private set; }

        /// <summary>What the crab wears and who flies with it: the scrap armour on the shell, the drone.</summary>
        void RefreshGear()
        {
            var lib = Library.Current;
            bool wear = State.Items.Has(ItemId.ScrapArmor);
            if (wear && !armor && lib && lib.armor && Rig.body)
            {
                armor = Instantiate(lib.armor, Rig.body, false);
                armor.name = "Armor";
                foreach (var c in armor.GetComponentsInChildren<Collider>()) Destroy(c);
            }
            if (!wear && armor) Destroy(armor);
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
        public float Armor => State.Items.Has(ItemId.ScrapArmor) ? 0.65f : 1f;

        public void ShowSpark(bool on)
        {
            if (sparkLight) sparkLight.enabled = on;
        }

        void Update()
        {
            iframes -= Time.deltaTime;
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
