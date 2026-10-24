using UnityEngine;

namespace Crab.Game
{
    /// <summary>The Guardian's weak point: blows here go to its heart in full.</summary>
    public sealed class GuardianHeart : MonoBehaviour, IHittable
    {
        public Guardian guardian;
        public bool Alive => guardian && guardian.Alive;
        public void Hit(float damage, Vector3 point, Vector3 dir, GameObject by) => guardian.HitHeart(damage, point);
    }
}
