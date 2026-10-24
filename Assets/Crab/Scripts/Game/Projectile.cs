// A thrown stone: flies on an arc, strikes the first thing it meets.
using UnityEngine;

namespace Crab.Game
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class Projectile : MonoBehaviour
    {
        public float damage = 18;
        public GameObject owner;
        bool spent;

        void Start() => Destroy(gameObject, 6);

        void OnCollisionEnter(Collision c)
        {
            if (spent || (owner && c.transform.IsChildOf(owner.transform))) return;
            var h = Hits.Of(c.collider);
            var p = c.contactCount > 0 ? c.GetContact(0).point : transform.position;
            if (h != null && h.Alive)
            {
                h.Hit(damage, p, GetComponent<Rigidbody>().linearVelocity.normalized, owner);
                Sfx.Play("hit", p, 0.7f, 1.3f);
                spent = true;
            }
            Fx.Burst(p, new Color(0.8f, 0.8f, 0.75f), 8, 1.5f, 0.05f, 0.4f, false, 1);
            if (spent) Destroy(gameObject, 0.05f);
        }
    }
}
