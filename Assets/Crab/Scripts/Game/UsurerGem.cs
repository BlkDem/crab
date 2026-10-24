using UnityEngine;

namespace Crab.Game
{
    /// <summary>The gem on the Usurer's chest: his weak point, once the golden dome is down.</summary>
    public sealed class UsurerGem : MonoBehaviour, IHittable
    {
        public Usurer usurer;
        public bool Alive => usurer && usurer.Alive;
        public void Hit(float damage, Vector3 point, Vector3 dir, GameObject by) => usurer.HitGem(damage, point);
    }
}
