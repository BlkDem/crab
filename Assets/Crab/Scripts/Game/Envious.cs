using UnityEngine;

namespace Crab.Game
{
    /// <summary>An Envious one: what a dweller becomes. Thin, green-grey, a face of mirror shards.
    /// It spits envy (green orbs that fly at the crab) from afar, claws up close, and when hurt it
    /// may flicker aside.</summary>
    public sealed class Envious : Enemy
    {
        Transform body, head, armL, armR, legL, legR;
        float phase, spitT, blinkCd;
        static readonly Color Green = new Color(0.55f, 1f, 0.35f);

        protected override void Awake()
        {
            base.Awake();
            body = model ? CrabRig.BodyOf(model) : transform;
            head = CrabRig.Find(transform, "Head");
            armL = CrabRig.Find(transform, "ArmL");
            armR = CrabRig.Find(transform, "ArmR");
            legL = CrabRig.Find(transform, "LegL");
            legR = CrabRig.Find(transform, "LegR");
            spitT = Random.Range(1f, 3f);
        }

        protected override Color HitColor => Green;

        protected override void Hunt(Hero hero, float dist, float dt)
        {
            spitT -= dt;
            blinkCd -= dt;
            if (spitT <= 0 && dist > 3.5f && dist < 14 && !Provoked)
            {
                spitT = Random.Range(2.2f, 3.4f);
                StartCoroutine(Spit(hero));
            }
        }

        System.Collections.IEnumerator Spit(Hero hero)
        {
            var from = transform.position + Vector3.up * 1.4f + transform.forward * 0.4f;
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "envy";
            Destroy(go.GetComponent<Collider>());
            go.transform.localScale = Vector3.one * 0.3f;
            var lib = Library.Current;
            if (lib && lib.particleAdditive) go.GetComponent<Renderer>().sharedMaterial = lib.particleAdditive;
            var l = go.AddComponent<Light>();
            l.color = Green;
            l.range = 3;
            l.intensity = 3;
            Sfx.Play("crawler", from, 0.5f, 1.6f);
            var aim = hero.transform.position + Vector3.up * 0.3f;
            var p = from;
            for (float t = 0; t < 3 && hero; t += Time.deltaTime)
            {
                var d = aim - p;
                if (d.magnitude < 0.3f) break;
                p += d.normalized * Mathf.Min(d.magnitude, 9 * Time.deltaTime);
                go.transform.position = p;
                if (Random.value < 0.5f) Fx.Burst(p, Green, 1, 0.3f, 0.12f, 0.3f);
                if (hero.Alive && Vector3.Distance(p, hero.transform.position + Vector3.up * 0.3f) < 0.6f)
                {
                    hero.Hurt(damage * 0.8f, p, d);
                    break;
                }
                yield return null;
            }
            Fx.Burst(p, Green, 14, 2, 0.08f, 0.4f);
            Destroy(go);
        }

        protected override void OnHurt()
        {
            if (blinkCd > 0 || !Alive || Random.value > 0.35f) return;
            blinkCd = 3;
            var side = transform.right * (Random.value < 0.5f ? -1 : 1) * 3.5f;
            Fx.Burst(transform.position + Vector3.up, Green, 20, 2, 0.1f, 0.5f);
            cc.enabled = false;
            transform.position += side;
            cc.enabled = true;
            Fx.Burst(transform.position + Vector3.up, Green, 20, 2, 0.1f, 0.5f);
        }

        protected override void Animate(float dt, float spd, float w)
        {
            phase += dt * (2 + spd * 3f);
            float s = Mathf.Sin(phase) * 35 * Mathf.Clamp01(spd / 2);
            if (legL) legL.localRotation = Quaternion.Euler(s, 0, 0);
            if (legR) legR.localRotation = Quaternion.Euler(-s, 0, 0);
            float claw = w >= 0 ? (w < 0.7f ? -120 * w / 0.7f : -120 + 200 * (w - 0.7f) / 0.3f) : -15 - s * 0.4f;
            if (armR) armR.localRotation = Quaternion.Euler(claw, 0, 0);
            if (armL) armL.localRotation = Quaternion.Euler(w >= 0 ? -60 : -15 + s * 0.4f, 0, 0);
            if (body) body.localRotation = Quaternion.Euler(12 + spd * 3, 0, Mathf.Sin(phase) * 4);
            if (head) head.localRotation = Quaternion.Euler(Mathf.Sin(life * 7) * 4, Mathf.Sin(life * 3.1f) * 15, Mathf.Sin(life * 5) * 6);
        }
    }
}
