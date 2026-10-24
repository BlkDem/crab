using System.Collections;
using System.Collections.Generic;
using Crab.Core;
using UnityEngine;

namespace Crab.Game
{
    /// <summary>The big wave: rolls up the beach to a line and back, dragging the crab if it is caught.</summary>
    public sealed class Wave : MonoBehaviour
    {
        public float startZ = -18, reachZ = 4, speed = 5;
        public bool rolling;
        public event System.Action<bool> Done;   // true: the crab was caught
        bool caught;

        public void Roll()
        {
            rolling = true;
            caught = false;
            var p = transform.position;
            transform.position = new Vector3(p.x, p.y, startZ);
            gameObject.SetActive(true);
            Sfx.Play("surf", transform.position, 1, 0.7f);
        }

        void Update()
        {
            if (!rolling) return;
            var p = transform.position;
            p.z += speed * Time.deltaTime;
            transform.position = p;
            var hero = Hero.Instance;
            if (!caught && hero && hero.transform.position.z < p.z + 0.5f && hero.transform.position.z > p.z - 6)
            {
                caught = true;
                hero.Hurt(8, hero.transform.position, Vector3.back);
                hero.Controller.Knock(Vector3.back * 9);
                Fx.Burst(hero.transform.position + Vector3.up * 0.3f, new Color(0.85f, 0.95f, 1f), 40, 4, 0.12f, 0.8f, false, 1.2f);
            }
            if (p.z >= reachZ || caught)
            {
                rolling = false;
                gameObject.SetActive(false);
                Done?.Invoke(caught);
            }
        }
    }
}
