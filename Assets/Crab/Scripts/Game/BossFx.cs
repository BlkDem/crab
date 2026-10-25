// What the big fights share: glowing rings on the ground where something is about to fall, and
// a wave rolling out in a ring that the crab must jump over.
using System.Collections;
using UnityEngine;

namespace Crab.Game
{
    public static class BossFx
    {
        /// <summary>A glowing ring on the ground where something is about to fall.</summary>
        /// <summary>Waits until no boss is fighting (or a while at most). A boss notices the crab's fall
        /// only between its attacks; the crab is brought back after that, so the fight starts over.</summary>
        public static IEnumerator Calm(float most = 12)
        {
            for (float t = 0; t < most; t += Time.unscaledDeltaTime)
            {
                bool any = false;
                foreach (var m in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                    if (m is IBoss b && b.Fighting) { any = true; break; }
                if (!any) yield break;
                yield return null;
            }
        }

        public static GameObject Marker(Vector3 at, float radius, Color color)
        {
            var go = new GameObject("marker");
            Object.Destroy(go, 10);   // its attack may be stopped before it removes it
            go.transform.position = at + Vector3.up * 0.12f;
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = Library.Current ? Library.Current.tracer : null;
            lr.loop = true;
            lr.useWorldSpace = false;
            lr.positionCount = 40;
            for (int i = 0; i < 40; i++)
            {
                float a = i / 40f * Mathf.PI * 2;
                lr.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius));
            }
            lr.startWidth = lr.endWidth = 0.12f;
            lr.startColor = lr.endColor = color;
            return go;
        }

        /// <summary>The marker throbs harder as the moment comes (k 0..1).</summary>
        public static void Pulse(GameObject mark, float k)
        {
            var lr = mark.GetComponent<LineRenderer>();
            float w = Mathf.Lerp(0.1f, 0.3f, k) * (1 + 0.3f * Mathf.Sin(Time.time * 20));
            lr.startWidth = lr.endWidth = w;
            mark.transform.localScale = Vector3.one * Mathf.Lerp(1.2f, 1f, k);
        }

        /// <summary>A ring rolling out from `at` to `reach`; a crab on the ground where it passes takes `damage`.</summary>
        public static IEnumerator Ring(Vector3 at, float speed, float reach, Color color, float damage)
        {
            var go = new GameObject("wave");
            Object.Destroy(go, reach / Mathf.Max(0.1f, speed) + 1);   // gone even if the attack is stopped
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = Library.Current ? Library.Current.tracer : null;
            lr.loop = true;
            lr.positionCount = 90;
            lr.startWidth = lr.endWidth = 0.45f;
            lr.startColor = lr.endColor = color;
            var hero = Hero.Instance;
            bool hit = false;
            for (float r = 0.5f; r < reach; r += Time.deltaTime * speed)
            {
                for (int i = 0; i < 90; i++)
                {
                    float a = i / 90f * Mathf.PI * 2;
                    lr.SetPosition(i, at + new Vector3(Mathf.Cos(a) * r, 0.2f, Mathf.Sin(a) * r));
                }
                if (Random.value < 0.5f)
                {
                    float a = Random.Range(0, Mathf.PI * 2);
                    Fx.Burst(at + new Vector3(Mathf.Cos(a) * r, 0.2f, Mathf.Sin(a) * r), color, 3, 1.5f, 0.12f, 0.4f, true, 1);
                }
                if (!hit && hero && hero.Alive)
                {
                    var hp = hero.transform.position;
                    float d = new Vector2(hp.x - at.x, hp.z - at.z).magnitude;
                    if (Mathf.Abs(d - r) < 0.6f && hero.Controller.Grounded)
                    {
                        hit = true;
                        hero.Hurt(damage, hp, hp - at);
                    }
                }
                yield return null;
            }
            Object.Destroy(go);
        }
    }
}
