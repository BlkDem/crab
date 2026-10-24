using System;
using Crab.Core;
using UnityEngine;

namespace Crab.Game
{
    /// <summary>A Shade: drifts slowly, reaches out with both arms and drains life.</summary>
    public sealed class Shade : Enemy
    {
        Transform armL, armR, eyes, body;
        float moanT;

        protected override void Awake()
        {
            base.Awake();
            body = model ? CrabRig.BodyOf(model) : CrabRig.Find(transform, "Body");
            armL = CrabRig.Find(transform, "ArmL");
            armR = CrabRig.Find(transform, "ArmR");
            eyes = CrabRig.Find(transform, "GlowEyes");
            moanT = UnityEngine.Random.Range(2f, 8f);
        }

        protected override void Animate(float dt, float spd, float w)
        {
            float t = life;
            if (body) body.localPosition = new Vector3(0, 0.12f + Mathf.Sin(t * 1.7f) * 0.06f, 0);
            if (body) body.localRotation = Quaternion.Euler(spd * 6 + Mathf.Sin(t * 1.3f) * 2, 0, Mathf.Sin(t * 0.9f) * 3);
            float reach = w >= 0 ? Mathf.SmoothStep(0, 1, w) : hunting ? 0.25f : 0;
            var arm = Quaternion.Euler(-70 * reach + Mathf.Sin(t * 2.1f) * 6, 0, 0);
            if (armL) armL.localRotation = Quaternion.Slerp(armL.localRotation, arm * Quaternion.Euler(0, 0, Mathf.Sin(t * 2.7f) * 5), dt * 8);
            if (armR) armR.localRotation = Quaternion.Slerp(armR.localRotation, arm * Quaternion.Euler(0, 0, -Mathf.Sin(t * 2.3f) * 5), dt * 8);
            if (eyes) eyes.localScale = Vector3.one * (hurtT > 0 ? 0.3f : 1 + 0.15f * Mathf.Sin(t * 5));
            moanT -= dt;
            if (moanT <= 0 && hunting)
            {
                moanT = UnityEngine.Random.Range(4f, 9f);
                Sfx.Play("shade", transform.position + Vector3.up, 0.6f, UnityEngine.Random.Range(0.8f, 1.1f));
            }
        }

        protected override void Move(Vector3 horiz, float dt)
        {
            // drifting: no hurry to turn, no bounce
            base.Move(horiz, dt);
        }
    }
}
