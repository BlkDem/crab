using UnityEngine;

namespace Crab.Game
{
    /// <summary>The Devourer's heart: blows here go in as far as the light has opened it.</summary>
    public sealed class DevourerHeart : MonoBehaviour, IHittable
    {
        public Devourer devourer;
        public bool Alive => devourer && devourer.Alive;
        public void Hit(float damage, Vector3 point, Vector3 dir, GameObject by) => devourer.HitHeart(damage, point);
    }
}
