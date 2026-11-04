// A page torn out of Minos' book of judgement, adrift in the storm of the second circle: it hangs
// in the air over its island, bobbing, glowing red; the crab takes it by touching it.
using System;
using UnityEngine;

namespace Crab.Game
{
    public sealed class PagePickup : MonoBehaviour
    {
        public string id;
        public float reach = 1.8f;
        public event Action<PagePickup> Taken;
        Vector3 home;
        float seed;

        void Start()
        {
            home = transform.position;
            seed = UnityEngine.Random.value * 10;
        }

        void Update()
        {
            transform.position = home + Vector3.up * Mathf.Sin(Time.time * 1.3f + seed) * 0.15f;
            var hero = Hero.Instance;
            if (!hero || !hero.Alive) return;
            if ((hero.transform.position + Vector3.up * 0.5f - transform.position).sqrMagnitude < reach * reach) Take();
        }

        public void Take()
        {
            Fx.Burst(transform.position, new Color(1f, 0.35f, 0.3f), 40, 2.5f, 0.1f, 0.8f);
            Sfx.Play("spark", transform.position, 0.8f, 1.2f);
            gameObject.SetActive(false);
            Taken?.Invoke(this);
        }
    }
}
