using UnityEngine;

namespace Crab.Game
{
    /// <summary>One of the Colossus's fire cores: blows here go to that core.</summary>
    public sealed class ColossusCore : MonoBehaviour, IHittable
    {
        public Colossus colossus;
        public bool Alive => colossus && colossus.Alive;
        public void Hit(float damage, Vector3 point, Vector3 dir, GameObject by) => colossus.HitCore(transform, damage, point);
    }
}
