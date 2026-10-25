using System.Collections.Generic;
using Crab.Core;
using UnityEngine;

namespace Crab.Game
{
    /// <summary>Something to eat: heals, and in the prologue it is what an ordinary crab's day is about.</summary>
    public sealed class Edible : Interactable
    {
        public float heal = 20;
        public string title = "Еда";
        public event System.Action Eaten;

        public override string Prompt => $"[E] Съесть — {title.ToLower()}";

        // a soft glint over it, so food can be told from pebbles and plain weed at a distance
        Transform glint;
        Material glintMat;
        float phase;

        void Start()
        {
            var lib = Library.Current;
            if (!lib || !lib.particleAdditive) return;
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "Glint";
            Destroy(q.GetComponent<Collider>());
            glint = q.transform;
            glint.SetParent(transform, false);
            glintMat = new Material(lib.particleAdditive);
            var r = q.GetComponent<Renderer>();
            r.sharedMaterial = glintMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            phase = Random.value * 6;
        }

        void LateUpdate()
        {
            if (!glint) return;
            var cam = Camera.main;
            if (!cam) return;
            float k = 0.5f + 0.5f * Mathf.Sin(Time.time * 3 + phase);
            glint.position = transform.position + Vector3.up * 0.35f;
            glint.rotation = cam.transform.rotation;
            float far = Mathf.Clamp(Vector3.Distance(cam.transform.position, glint.position) / 12, 1, 3);   // stays visible far off
            glint.localScale = Vector3.one * (0.35f + 0.15f * k) * far / Mathf.Max(0.01f, transform.lossyScale.x);
            glintMat.SetColor("_Tint", new Color(1f, 0.92f, 0.6f, 0.35f + 0.4f * k));
        }

        public override void Use(Hero hero)
        {
            GameSession.Instance.State.Health.Heal(heal);
            hero.Rig.Play(ClawPose.Eat, 1.0f);
            Sfx.Play("eat", transform.position);
            Fx.Burst(transform.position + Vector3.up * 0.1f, new Color(1, 0.9f, 0.7f), 12, 1.2f, 0.05f, 0.5f, false, 1);
            Eaten?.Invoke();
            gameObject.SetActive(false);
        }
    }
}
