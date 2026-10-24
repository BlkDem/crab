using UnityEngine;

namespace Crab.Game
{
    /// <summary>Turns a part slowly round an axis (the gears of the labyrinth).</summary>
    public sealed class Spinner : MonoBehaviour
    {
        public Vector3 axis = Vector3.forward;
        public float speed = 20;
        void Update() => transform.Rotate(axis, speed * Time.deltaTime, Space.Self);
    }
}
