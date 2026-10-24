using UnityEngine;

namespace Crab.Game
{
    /// <summary>The golden dome over the Usurer: it turns every shot while his hoards hold gold.</summary>
    public sealed class UsurerDome : MonoBehaviour, IHittable
    {
        public Usurer usurer;
        public bool Alive => usurer && usurer.Alive;
        public void Hit(float damage, Vector3 point, Vector3 dir, GameObject by) => usurer.HitDome(point);
    }
}
