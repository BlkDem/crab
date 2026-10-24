// Fighting with what the crab has made: the item sits in its right claw; a strike checks for
// a hit halfway through the swing, a stone flies on an arc toward the aim point, the pistol
// fires along the centre of the screen. Aiming (right button) brings the camera over the
// shoulder; in the first-person boss scenes everything aims along the view.
using Crab.Core;
using UnityEngine;

namespace Crab.Game
{
    // after the rig has posed the claws, so the held gun can be turned onto the target
    [DefaultExecutionOrder(100)]
    public sealed class HeroCombat : MonoBehaviour
    {
        public Hero hero;
        [System.NonSerialized] public bool locked;   // story moments, menus
        [System.NonSerialized] public bool firstPerson;

        GameObject held;
        ItemId heldId = ItemId.None;
        Transform muzzle;
        float cooldown, pendingHit = -1, pendingThrow = -1;
        Blueprint pendingBp;

        GameState State => hero.State;
        public bool Aiming { get; private set; }

        void Start()
        {
            State.Items.Changed += Refresh;
            Refresh();
        }

        void OnDestroy()
        {
            if (GameSession.Instance) State.Items.Changed -= Refresh;
        }

        /// <summary>Show the equipped item in the claw.</summary>
        void Refresh()
        {
            var id = State.Items.Equipped;
            if (id == heldId && held) return;
            if (held) Destroy(held);
            heldId = id;
            held = null;
            muzzle = null;
            var lib = Library.Current;
            var model = lib ? lib.Item(id) : null;
            if (!model || !hero.Rig.gripRight) return;
            held = Instantiate(model, hero.Rig.gripRight, false);
            held.name = id.ToString();
            held.transform.localPosition = Vector3.zero;
            held.transform.localRotation = Quaternion.identity;
            // the grip lives inside the scaled body: undo the body's scale so items keep their size
            var s = hero.Rig.gripRight.lossyScale;
            held.transform.localScale = new Vector3(1 / s.x, 1 / s.y, 1 / s.z);
            foreach (var c in held.GetComponentsInChildren<Collider>()) Destroy(c);
            muzzle = CrabRig.Find(held.transform, "Muzzle");
        }

        void Update()
        {
            float dt = Time.deltaTime;
            cooldown -= dt;
            if (locked || !hero.Alive || Time.timeScale == 0)
            {
                SetAim(false);
                return;
            }
            var items = State.Items;
            for (int i = 0; i < Controls.Slot.Length; i++)
                if (Controls.Slot[i].WasPressedThisFrame())
                {
                    var list = new System.Collections.Generic.List<ItemId>(items.Weapons);
                    if (i < list.Count) { items.Equip(list[i]); Sfx.Play("click"); }
                }
            if (Controls.Next.WasPressedThisFrame()) { items.Cycle(1); Sfx.Play("click"); }
            if (Controls.Prev.WasPressedThisFrame()) { items.Cycle(-1); Sfx.Play("click"); }

            var bp = Blueprints.Get(items.Equipped);
            bool canAim = bp != null && (bp.kind == WeaponKind.Gun || bp.kind == WeaponKind.Throw);
            SetAim(canAim && Controls.Aim.IsPressed());

            if (pendingHit >= 0)
            {
                pendingHit -= dt;
                if (pendingHit < 0) MeleeHit(pendingBp);
            }
            if (pendingThrow >= 0)
            {
                pendingThrow -= dt;
                if (pendingThrow < 0) ThrowStone(pendingBp);
            }
            // automatic weapons fire while the button is held
            if (bp != null && bp.auto ? Controls.Attack.IsPressed() : Controls.Attack.WasPressedThisFrame()) Fire();
        }

        /// <summary>Strike, throw or shoot with what is in the claw (the attack button; tests call it too).</summary>
        public bool Fire()
        {
            var items = State.Items;
            var bp = Blueprints.Get(items.Equipped);
            if (bp == null || cooldown > 0 || locked || !hero.Alive) return false;
            switch (bp.kind)
            {
                case WeaponKind.Melee:
                    cooldown = bp.cooldown;
                    hero.Rig.Play(ClawPose.Strike, bp.cooldown * 0.9f);
                    hero.Controller.faceView = true;
                    pendingBp = bp;
                    pendingHit = bp.cooldown * 0.35f;
                    Sfx.Play("swing", transform.position, 0.7f, 1.2f - bp.cooldown * 0.3f);
                    break;
                case WeaponKind.Throw:
                    if (items.Count(ItemId.Stone) <= 0) { Story.Hint("Камней нет — создай из минерала (Tab)"); break; }
                    cooldown = bp.cooldown;
                    hero.Rig.Play(ClawPose.Throw, 0.5f);
                    hero.Controller.faceView = true;
                    pendingBp = bp;
                    pendingThrow = 0.25f;
                    break;
                case WeaponKind.Gun:
                    if (items.Count(bp.ammo) <= 0)
                    {
                        cooldown = 0.3f;
                        Sfx.Play("click", null, 1, 0.6f);
                        Story.Hint(bp.ammo == ItemId.EnergyCell ? "Нет энергоячеек — создай из энергии (Tab)" : "Нет патронов — создай из металла (Tab)");
                        break;
                    }
                    cooldown = bp.cooldown;
                    Shoot(bp);
                    break;
            }
            return true;
        }

        void LateUpdate()
        {
            // after a strike the crab may turn back to walking sideways
            if (!Aiming && cooldown < -0.2f && pendingHit < 0 && pendingThrow < 0 && !firstPerson) hero.Controller.faceView = false;
            // a gun in the claw points where the crab aims: the line from the grip to the muzzle
            // is turned onto the target (whatever way the model's nodes are rotated)
            if (held && muzzle && (Aiming || firstPerson))
            {
                // in the crab's eyes the gun is held low and to the right of the view, like in a shooter
                if (firstPerson && hero.Cam.cam)
                {
                    var c = hero.Cam.cam.transform;
                    held.transform.position = c.position + c.right * 0.17f - c.up * 0.16f + c.forward * 0.32f;
                }
                var dir = AimPoint(80, out _) - held.transform.position;
                var cur = muzzle.position - held.transform.position;
                if (dir.sqrMagnitude > 0.01f && cur.sqrMagnitude > 1e-6f)
                    held.transform.rotation = Quaternion.FromToRotation(cur, dir) * held.transform.rotation;
            }
        }

        void SetAim(bool on)
        {
            if (firstPerson)
            {
                Aiming = true;
                hero.Controller.faceView = true;
                return;
            }
            if (on == Aiming) return;
            Aiming = on;
            hero.Cam.SetMode(on ? CamMode.Aim : CamMode.Third);
            hero.Controller.faceView = on;
            if (on) hero.Rig.Hold(ClawPose.Aim); else hero.Rig.Release(ClawPose.Aim);
        }

        /// <summary>Where the crab is aiming: what the centre of the screen looks at (not the crab itself).</summary>
        public Vector3 AimPoint(float range, out Collider hitCol)
        {
            var ray = hero.Cam.AimRay;
            hitCol = null;
            var hits = Physics.RaycastAll(ray, range, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            Vector3 p = ray.origin + ray.direction * range;
            foreach (var h in hits)
            {
                if (h.transform.IsChildOf(hero.transform) || h.distance >= best) continue;
                best = h.distance;
                p = h.point;
                hitCol = h.collider;
            }
            return p;
        }

        void MeleeHit(Blueprint bp)
        {
            var center = transform.position + transform.forward * bp.range * 0.75f + Vector3.up * 0.35f;
            bool any = false;
            foreach (var c in Physics.OverlapSphere(center, bp.range * 0.65f, ~0, QueryTriggerInteraction.Collide))
            {
                if (c.transform.IsChildOf(transform)) continue;
                var h = Hits.Of(c);
                if (h == null || !h.Alive) continue;
                h.Hit(bp.damage, c.ClosestPoint(center), transform.forward, hero.gameObject);
                any = true;
                break;
            }
            if (any)
            {
                Sfx.Play("hit", center, 0.9f);
                hero.Cam.Shake(0.25f);
            }
        }

        void ThrowStone(Blueprint bp)
        {
            if (!State.Items.Take(ItemId.Stone)) return;
            var lib = Library.Current;
            var from = hero.Rig.gripRight ? hero.Rig.gripRight.position : transform.position + Vector3.up * 0.4f;
            var target = AimPoint(bp.range * 2, out _);
            var go = lib && lib.stone ? Instantiate(lib.stone) : GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "thrown stone";
            go.transform.position = from + transform.forward * 0.2f;
            foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
            var col = go.AddComponent<SphereCollider>();
            col.radius = 0.07f;
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.4f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            // a lob that lands on the aim point
            var to = target - go.transform.position;
            float flat = new Vector3(to.x, 0, to.z).magnitude;
            float t = Mathf.Clamp(flat / 14f, 0.25f, 1.1f);
            rb.linearVelocity = new Vector3(to.x / t, to.y / t + 0.5f * Physics.gravity.magnitude * t, to.z / t);
            rb.angularVelocity = Random.insideUnitSphere * 10;
            var p = go.AddComponent<Projectile>();
            p.damage = bp.damage;
            p.owner = hero.gameObject;
            Sfx.Play("swing", from, 0.6f, 1.5f);
        }

        void Shoot(Blueprint bp)
        {
            State.Items.Take(bp.ammo);
            var from = muzzle ? muzzle.position : (hero.Rig.gripRight ? hero.Rig.gripRight.position : transform.position + Vector3.up * 0.4f);
            var point = AimPoint(bp.range, out var col);
            var tint = bp.energy ? new Color(0.45f, 0.9f, 1f) : new Color(1f, 0.85f, 0.5f);
            Fx.Tracer(from, point, tint, bp.energy ? 0.06f : 0.03f, bp.energy ? 0.12f : 0.07f);
            Fx.Flash(from, tint, 4, 5);
            Fx.Burst(from, tint, 6, 3, 0.05f, 0.15f);
            Sfx.Play("shot", from, 0.9f, bp.energy ? 1.6f : 1);
            hero.Cam.Shake(0.2f);
            hero.Cam.Kick(3);
            hero.Rig.Play(ClawPose.Aim, 0.3f);
            hero.Controller.faceView = true;
            var h = Hits.Of(col);
            if (h != null && h.Alive)
            {
                Hits.Energy = bp.energy;
                h.Hit(bp.damage, point, (point - from).normalized, hero.gameObject);
                Hits.Energy = false;
            }
            Fx.Burst(point, h != null ? new Color(0.6f, 1f, 0.7f) : new Color(0.8f, 0.75f, 0.7f), 10, 2.5f, 0.05f, 0.35f, h != null, 0.8f);
        }
    }
}
