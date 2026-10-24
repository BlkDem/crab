// A shard of memory (circle of Despondency): a crystal hanging over an old pedestal. Touched, it
// shows the crab what was before — the Spark in other hearts — on the fresco beside it.
using System;
using System.Collections;
using UnityEngine;

namespace Crab.Game
{
    public sealed class MemoryShard : Interactable
    {
        public string id;
        [Tooltip("What the camera looks at during the vision")]
        public Transform fresco;
        [TextArea] public string[] lines;
        public bool seen;
        [Tooltip("Lights the fresco while the vision lasts")]
        public Light frescoLight;
        public event Action<MemoryShard> Seen;
        Transform crystal;

        public override string Prompt => "[E] Коснуться осколка памяти";
        public override bool Usable => !seen;
        public override Vector3 Point => transform.position + Vector3.up * 1.6f;

        void Awake() => crystal = CrabRig.Find(transform, "GlowShard");

        public override void Use(Hero hero)
        {
            if (seen) return;
            seen = true;
            StartCoroutine(Vision(hero));
        }

        IEnumerator Vision(Hero hero)
        {
            hero.Controller.frozen = true;
            hero.Combat.locked = true;
            Sfx.Play("spark", Point, 0.8f, 0.6f);
            Story.Flash(new Color(1f, 0.9f, 0.7f, 0.9f), 1.4f);
            // a step aside from the shard, a little up: the whole fresco in view
            if (fresco) hero.Cam.Shot(fresco.position + fresco.forward * 8 + fresco.right * 2.2f + Vector3.up * 3.2f, fresco.position + Vector3.up * 3.2f);
            if (frescoLight) frescoLight.enabled = true;
            yield return new WaitForSeconds(1);
            foreach (var l in lines)
            {
                Story.Think(l, 4.2f);
                for (float t = 0; t < 4.5f; t += Time.deltaTime) yield return null;
            }
            hero.Cam.SetMode(CamMode.Third);
            hero.Controller.frozen = false;
            hero.Combat.locked = false;
            if (crystal) crystal.gameObject.SetActive(false);
            if (frescoLight) frescoLight.intensity = 1.5f;
            Fx.Burst(Point, new Color(1f, 0.9f, 0.7f), 50, 3, 0.08f, 1);
            Seen?.Invoke(this);
        }

        /// <summary>Already seen (a loaded game).</summary>
        public void MarkSeen()
        {
            seen = true;
            if (crystal) crystal.gameObject.SetActive(false);
        }

        void Update()
        {
            if (crystal && !seen)
            {
                crystal.localRotation = Quaternion.Euler(0, Time.time * 30, 0);
                crystal.localPosition = new Vector3(crystal.localPosition.x, crystal.localPosition.y, crystal.localPosition.z);
            }
        }
    }
}
