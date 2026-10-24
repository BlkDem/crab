// A gate of the labyrinth: a heavy plate that sinks into the floor to open and rises to close.
using UnityEngine;

namespace Crab.Game
{
    public sealed class MechGate : MonoBehaviour
    {
        [Tooltip("The plate that moves")]
        public Transform door;
        public float travel = 6.2f, speed = 2.5f;
        public bool open;
        Vector3 closedPos;
        float k;

        void Awake()
        {
            if (door) closedPos = door.localPosition;
            k = open ? 1 : 0;
            Pose();
        }

        public void Set(bool on)
        {
            if (open == on) return;
            open = on;
            Sfx.Play("slam", transform.position + Vector3.up * 2, 0.5f, 0.7f);
            Hero.Instance?.Cam.Shake(0.3f);
        }

        void Update()
        {
            float goal = open ? 1 : 0;
            if (Mathf.Approximately(k, goal)) return;
            k = Mathf.MoveTowards(k, goal, Time.deltaTime * speed / travel);
            Pose();
        }

        void Pose()
        {
            if (door) door.localPosition = closedPos + Vector3.down * travel * k;
        }
    }
}
