// The Guide (circle of Betrayal): a lanky clockwork figure with a lantern who offers to lead the
// crab through the labyrinth. It walks its path a little ahead, waits when the crab falls behind,
// stops before closed gates — and at the heart of the labyrinth it shows what it is.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Crab.Game
{
    public sealed class Guide : MonoBehaviour
    {
        public List<Vector3> path = new List<Vector3>();
        [Tooltip("Gates on the way: the guide waits before each until it is open")]
        public List<MechGate> gates = new List<MechGate>();
        public float speed = 3.2f, lead = 7;
        public Transform model;
        public int At { get; private set; }
        public bool Arrived => At >= path.Count - 1;
        public event Action<int> Reached;
        Transform body, legL, legR, armL, armR;
        float phase;

        void Awake()
        {
            if (!model && transform.childCount > 0) model = transform.GetChild(0);
            body = model ? CrabRig.BodyOf(model) : transform;
            legL = CrabRig.Find(transform, "LegL");
            legR = CrabRig.Find(transform, "LegR");
            armL = CrabRig.Find(transform, "ArmL");
            armR = CrabRig.Find(transform, "ArmR");
        }

        /// <summary>Skip to a point of the path (a loaded game, tests).</summary>
        public void Jump(int i)
        {
            At = Mathf.Clamp(i, 0, path.Count - 1);
            transform.position = path[At];
        }

        void Update()
        {
            float dt = Time.deltaTime;
            var hero = Hero.Instance;
            float spd = 0;
            if (hero && !Arrived)
            {
                var next = path[At + 1];
                bool blocked = gates.Exists(g => g && !g.open && Vector3.Distance(g.transform.position, next) < 4);
                bool near = Vector3.Distance(hero.transform.position, transform.position) < lead;
                if (near && !blocked)
                {
                    var to = next - transform.position;
                    to.y = 0;
                    if (to.magnitude < 0.3f)
                    {
                        At++;
                        Reached?.Invoke(At);
                    }
                    else
                    {
                        var step = to.normalized * Mathf.Min(speed * dt, to.magnitude);
                        transform.position += step;
                        spd = speed;
                        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), 1 - Mathf.Exp(-6 * dt));
                    }
                    if (Physics.Raycast(transform.position + Vector3.up * 3, Vector3.down, out var hit, 8, ~0, QueryTriggerInteraction.Ignore))
                        transform.position = new Vector3(transform.position.x, hit.point.y, transform.position.z);
                }
                else
                {
                    // turns back to see where the crab is
                    var back = hero.transform.position - transform.position;
                    back.y = 0;
                    if (back.sqrMagnitude > 0.1f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(back), 1 - Mathf.Exp(-3 * dt));
                }
            }
            phase += dt * (1 + spd * 1.6f);
            float s = Mathf.Sin(phase * 2) * 25 * Mathf.Clamp01(spd);
            if (legL) legL.localRotation = Quaternion.Euler(s, 0, 0);
            if (legR) legR.localRotation = Quaternion.Euler(-s, 0, 0);
            if (armL) armL.localRotation = Quaternion.Euler(-50 + Mathf.Sin(phase) * 4, 0, 0);   // the lantern held up
            if (armR) armR.localRotation = Quaternion.Euler(-s * 0.5f, 0, 0);
            if (body) body.localRotation = Quaternion.Euler(6, 0, Mathf.Sin(phase * 2) * 3 * Mathf.Clamp01(spd));
        }

        public void Say(string line) => Story.Think($"Проводник: «{line}»", 4.5f);
    }
}
