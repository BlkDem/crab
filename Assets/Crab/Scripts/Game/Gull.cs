using System.Collections;
using System.Collections.Generic;
using Crab.Core;
using UnityEngine;

namespace Crab.Game
{
    /// <summary>A gull circling over the beach; it dives at the crab unless the crab is in shelter.</summary>
    public sealed class Gull : MonoBehaviour
    {
        public Vector3 circleCenter;
        public float circleRadius = 9, height = 9;
        Transform wingL, wingR, body;
        float t, flap, diveT = -1;
        Vector3 diveFrom, diveTo;
        public bool Diving => diveT >= 0;
        public System.Func<bool> heroSheltered;
        public event System.Action<bool> DiveDone;   // true: the crab was hit

        void Awake()
        {
            wingL = CrabRig.Find(transform, "WingL");
            wingR = CrabRig.Find(transform, "WingR");
            // the model's root is the gull's body; it already turns with the flight
            var b = CrabRig.BodyOf(transform);
            body = b == transform ? null : b;
        }

        public void Dive()
        {
            if (Diving || !Hero.Instance) return;
            diveT = 0;
            diveFrom = transform.position;
            diveTo = Hero.Instance.transform.position + Vector3.up * 0.3f;
            Sfx.Play("gull", transform.position, 1);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            t += dt;
            Vector3 pos;
            Vector3 fwd;
            if (diveT >= 0)
            {
                diveT += dt / 2.2f;
                // down to the crab, then up and away
                float k = diveT;
                var low = diveTo + Vector3.up * (heroSheltered != null && heroSheltered() ? 1.8f : 0.2f);
                var up = diveTo + (diveTo - diveFrom).normalized * 8 + Vector3.up * height;
                pos = k < 0.5f ? Vector3.Lerp(diveFrom, low, Mathf.SmoothStep(0, 1, k * 2)) : Vector3.Lerp(low, up, Mathf.SmoothStep(0, 1, (k - 0.5f) * 2));
                fwd = (pos - transform.position).normalized;
                if (k >= 0.5f && k - dt / 2.2f < 0.5f)
                {
                    var hero = Hero.Instance;
                    bool safe = heroSheltered != null && heroSheltered();
                    bool hit = !safe && hero && Vector3.Distance(hero.transform.position, diveTo) < 2.2f;
                    if (hit) hero.Hurt(15, hero.transform.position, hero.transform.position - diveFrom);
                    Sfx.Play("gull", pos, 1, 1.2f);
                    DiveDone?.Invoke(hit);
                }
                if (diveT >= 1)
                {
                    diveT = -1;
                    t = Mathf.Atan2(pos.z - circleCenter.z, pos.x - circleCenter.x) / 0.35f;
                }
                flap += dt * (k < 0.5f ? 2 : 14);
            }
            else
            {
                float a = t * 0.35f;
                pos = circleCenter + new Vector3(Mathf.Cos(a) * circleRadius, height + Mathf.Sin(t * 0.7f), Mathf.Sin(a) * circleRadius);
                fwd = new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a));
                pos = Vector3.Lerp(transform.position, pos, dt * 2);
                flap += dt * 7 * (Mathf.Sin(t * 0.5f) > 0.3f ? 1 : 0.2f);
            }
            transform.position = pos;
            if (fwd.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(fwd), dt * 5);
            float w = Mathf.Sin(flap) * 40;
            if (wingL) wingL.localRotation = Quaternion.Euler(0, 0, -w);
            if (wingR) wingR.localRotation = Quaternion.Euler(0, 0, w);
            if (body) body.localRotation = Quaternion.Euler(Diving ? 25 : 0, 0, 0);
        }
    }
}
