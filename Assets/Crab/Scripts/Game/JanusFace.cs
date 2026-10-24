using UnityEngine;

namespace Crab.Game
{
    /// <summary>One of the Two-Faced's faces: the smiling front, or the true face at the back.</summary>
    public sealed class JanusFace : MonoBehaviour, IHittable
    {
        public Janus janus;
        public bool back;
        public bool Alive => janus && janus.Alive;
        public void Hit(float damage, Vector3 point, Vector3 dir, GameObject by) => janus.HitFace(back, damage, point, dir, by);
    }
}
