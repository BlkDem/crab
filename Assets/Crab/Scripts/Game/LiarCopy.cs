using UnityEngine;

namespace Crab.Game
{
    /// <summary>One of the Father of Lies' copies: a blow to it shatters it — and feeds him.</summary>
    public sealed class LiarCopy : MonoBehaviour, IHittable
    {
        public FatherOfLies father;
        public bool Alive => gameObject.activeInHierarchy && father && father.Alive;
        public void Hit(float damage, Vector3 point, Vector3 dir, GameObject by) => father.HitCopy(this, point);
    }
}
