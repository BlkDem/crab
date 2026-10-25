// A flying fortress of the demon army: a rock with towers drifting in a wide circle over the
// battlefield. Now and then it bombards — dark bombs fall where the crab is (marked on the ground).
using System.Collections;
using UnityEngine;

namespace Crab.Game
{
    public sealed class FlyingFortress : MonoBehaviour
    {
        public Vector3 center;
        public float radius = 70, height = 45, speed = 4, every = 14;
        [Tooltip("Bombs only while true (the director turns it on)")]
        public bool bombing;
        float a, t = 8;

        void Start()
        {
            if (center == Vector3.zero) center = transform.position - Vector3.up * height;
            a = Random.Range(0, Mathf.PI * 2);
        }

        void Update()
        {
            a += speed / radius * Time.deltaTime;
            var p = center + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius + Vector3.up * (height + Mathf.Sin(Time.time * 0.3f) * 2);
            transform.position = p;
            transform.rotation = Quaternion.Euler(Mathf.Sin(Time.time * 0.4f) * 2, -a * Mathf.Rad2Deg, Mathf.Cos(Time.time * 0.3f) * 2);
            if (!bombing) return;
            t -= Time.deltaTime;
            if (t > 0) return;
            t = every * Random.Range(0.8f, 1.2f);
            StartCoroutine(Bombard());
        }

        public IEnumerator Bombard(int n = 5)
        {
            var hero = Hero.Instance;
            if (!hero) yield break;
            Story.Hint("Крепость в небе бомбит!");
            Sfx.Play("roar", transform.position, 0.5f, 0.5f);
            for (int i = 0; i < n && hero && hero.Alive; i++)
            {
                var at = hero.transform.position + (i == 0 ? Vector3.zero : new Vector3(Random.Range(-5f, 5f), 0, Random.Range(-5f, 5f)));
                if (Physics.Raycast(at + Vector3.up * 10, Vector3.down, out var hit, 30, ~0, QueryTriggerInteraction.Ignore)) at = hit.point;
                StartCoroutine(Bomb(at));
                yield return new WaitForSeconds(0.35f);
            }
        }

        IEnumerator Bomb(Vector3 at)
        {
            const float r = 2.0f, fall = 1.6f;
            var mark = BossFx.Marker(at, r, new Color(1f, 0.3f, 0.2f, 0.6f));
            var bomb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(bomb.GetComponent<Collider>());
            bomb.transform.localScale = Vector3.one * 0.6f;
            var from = at + Vector3.up * 30;
            for (float k = 0; k < fall; k += Time.deltaTime)
            {
                float f = k / fall;
                bomb.transform.position = Vector3.Lerp(from, at, f * f);
                BossFx.Pulse(mark, f);
                yield return null;
            }
            Destroy(mark);
            Destroy(bomb);
            Sfx.Play("slam", at, 0.9f, 0.9f);
            Fx.Burst(at + Vector3.up * 0.3f, new Color(1f, 0.5f, 0.2f), 40, 6, 0.15f, 0.8f, true, 1);
            Fx.Burst(at + Vector3.up * 0.3f, new Color(0.2f, 0.18f, 0.16f), 30, 3, 0.4f, 1.6f, false, -0.2f);
            var hero = Hero.Instance;
            if (hero && hero.Alive && Vector3.Distance(hero.transform.position, at) < r) hero.Hurt(16, hero.transform.position, hero.transform.position - at);
            else if (hero) hero.Cam.Shake(0.4f);
        }
    }
}
