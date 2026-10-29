// The crab's body language, made procedurally from its rigid parts (Tools/blender/make_hero.py):
// eight legs stepping in two alternating groups, the shell bobbing and leaning with the ground,
// eyes on stalks turning to what the crab looks at, and the claws posing for what it does —
// striking, throwing, aiming, creating things from matter, drawing matter in, eating.
// Axes are the body's own (Unity: +Z forward, +Y up, -X the crab's left). A joint's turn is made in
// those axes about the joint and laid over its pose at rest, so the same code moves rigid parts with
// no turn at rest (make_hero.py) and the bones of a skinned model (import_hero.py).
using System.Collections.Generic;
using UnityEngine;

namespace Crab.Game
{
    public enum ClawPose { Idle, Strike, Throw, Aim, Create, Absorb, Eat, Hurt, Think }

    public sealed class CrabRig : MonoBehaviour
    {
        /// <summary>A moving part: its pose at rest, and the rest turn relative to the body.</summary>
        sealed class Joint
        {
            public Transform t;
            public Quaternion rest, frame, turn = Quaternion.identity;

            /// <summary>Turn the joint by q (in the body's axes, about the joint).</summary>
            public void Set(Quaternion q)
            {
                turn = q;
                t.localRotation = rest * (Quaternion.Inverse(frame) * q * frame);
            }

            public void Toward(Quaternion q, float k) => Set(Quaternion.Slerp(turn, q, k));
        }

        sealed class Leg
        {
            public Joint hip, knee, ankle;
            public Vector3 outDir, liftAxis, tangent;
            public float phaseOffset;
        }

        sealed class Claw
        {
            public Joint merus, carpus, finger;
            public Vector3 hinge;   // the movable finger opens about this (body axes)
            public float side;   // -1 left, +1 right
        }

        [Tooltip("The body part of the model (scaled, origin on the ground)")]
        public Transform body;
        public Transform gripRight, gripLeft;

        /// <summary>World velocity of the crab (set by the controller).</summary>
        [System.NonSerialized] public Vector3 velocity;
        [System.NonSerialized] public bool grounded = true;
        [System.NonSerialized] public Vector3 groundNormal = Vector3.up;
        /// <summary>A point the eyes look at, if any.</summary>
        [System.NonSerialized] public Vector3? lookAt;

        public ClawPose Pose { get; private set; }
        float poseT, poseDur = 1;

        readonly List<Leg> legs = new List<Leg>();
        readonly Claw[] claws = new Claw[2];
        Joint eyeL, eyeR;
        float phase, bob, idleT;
        Quaternion tilt = Quaternion.identity, bodyRestRot;
        Vector3 bodyRest;

        void Awake() => Bind();

        public void Bind()
        {
            if (!body) body = Find(transform, "Body") ?? (transform.childCount > 0 ? transform.GetChild(0) : null);
            if (!body) return;
            bodyRest = body.localPosition;
            bodyRestRot = body.localRotation;
            tilt = Quaternion.identity;
            legs.Clear();
            claws[0] = claws[1] = null;
            for (int s = 0; s < 2; s++)
                for (int i = 0; i < 4; i++)
                {
                    string n = $"Leg{(s == 0 ? "L" : "R")}{i}";
                    var hip = Find(body, n + "_0");
                    if (!hip) continue;
                    var knee = Find(hip, n + "_1");
                    var ankle = knee ? Find(knee, n + "_2") : null;
                    var o = body.InverseTransformPoint(knee ? knee.position : hip.position + hip.forward) - body.InverseTransformPoint(hip.position);
                    o.y = 0;
                    o.Normalize();
                    // two alternating groups: L0 R1 L2 R3 / R0 L1 R2 L3
                    bool groupA = (i + s) % 2 == 0;
                    legs.Add(new Leg
                    {
                        hip = JointOf(hip), knee = JointOf(knee), ankle = JointOf(ankle), outDir = o,
                        liftAxis = Vector3.Cross(o, Vector3.up).normalized,
                        tangent = Vector3.Cross(Vector3.up, o).normalized,
                        phaseOffset = (groupA ? 0 : Mathf.PI) + i * 0.15f,
                    });
                }
            for (int s = 0; s < 2; s++)
            {
                string n = "Claw" + (s == 0 ? "L" : "R");
                var m = Find(body, n + "_0");
                if (!m) continue;
                var c = Find(m, n + "_1");
                var p = c ? Find(c, n + "_2") : null;
                var f = p ? Find(p, n + "_3") : null;
                // the finger opens upward, about the axis across the line palm → finger
                var along = p && f ? body.InverseTransformDirection(f.position - p.position) : Vector3.forward;
                var hinge = Vector3.Cross(along, Vector3.up);
                claws[s] = new Claw
                {
                    merus = JointOf(m), carpus = JointOf(c), finger = JointOf(f), side = s == 0 ? -1 : 1,
                    hinge = hinge.sqrMagnitude > 1e-8f ? hinge.normalized : Vector3.left,
                };
            }
            eyeL = JointOf(Find(body, "EyeL"));
            eyeR = JointOf(Find(body, "EyeR"));
            if (!gripRight || !gripRight.IsChildOf(body)) gripRight = Find(body, "GripR");
            if (!gripLeft || !gripLeft.IsChildOf(body)) gripLeft = Find(body, "GripL");
            // a held thing lies along the body at rest (a grip under a bone comes in with the bone's turn)
            if (gripRight) gripRight.rotation = body.rotation;
            if (gripLeft) gripLeft.rotation = body.rotation;
        }

        Joint JointOf(Transform t) => t ? new Joint
        {
            t = t,
            rest = t.localRotation,
            frame = Quaternion.Inverse(body.rotation) * t.rotation,
        } : null;

        /// <summary>Put another model on the rig (the crab's new form): the old one goes.</summary>
        public Transform Swap(GameObject prefab)
        {
            var old = FindModel();
            var model = Instantiate(prefab, transform, false);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            if (old) { old.SetParent(null); Destroy(old.gameObject); }
            body = BodyOf(model.transform);
            gripRight = gripLeft = null;
            Bind();
            return model.transform;
        }

        Transform FindModel()
        {
            for (var t = body; t; t = t.parent)
                if (t.parent == transform) return t;
            return null;
        }

        /// <summary>
        /// The "Body" part of a model. glTFast imports a model whose only root node is Body as that
        /// node itself (named after the asset), so the model's root is then the body.
        /// </summary>
        public static Transform BodyOf(Transform modelRoot) => Find(modelRoot, "Body") ?? modelRoot;

        public static Transform Find(Transform root, string name)
        {
            if (!root) return null;
            if (root.name == name) return root;
            foreach (Transform c in root)
            {
                var r = Find(c, name);
                if (r) return r;
            }
            return null;
        }

        /// <summary>Strike a pose for a while (then back to idle).</summary>
        public void Play(ClawPose pose, float duration)
        {
            Pose = pose;
            poseT = 0;
            poseDur = Mathf.Max(0.05f, duration);
        }

        /// <summary>Hold a pose until told otherwise (aiming, creating, absorbing).</summary>
        public void Hold(ClawPose pose)
        {
            if (Pose == pose && poseDur > 1e5f) return;
            Pose = pose;
            poseT = 0;
            poseDur = 1e6f;
        }

        public void Release(ClawPose pose)
        {
            if (Pose == pose) { Pose = ClawPose.Idle; poseT = 0; poseDur = 1; }
        }

        /// <summary>0..1 through the current timed pose.</summary>
        public float PoseProgress => Mathf.Clamp01(poseT / poseDur);

        void LateUpdate()
        {
            if (!body) return;
            float dt = Time.deltaTime;
            poseT += dt;
            if (poseT > poseDur && poseDur < 1e5f) { Pose = ClawPose.Idle; poseT = 0; poseDur = 1; }
            idleT += dt;

            var local = transform.InverseTransformDirection(velocity);
            local.y = 0;
            float speed = local.magnitude;
            var move = speed > 0.01f ? local / speed : Vector3.zero;
            phase += dt * (grounded ? Mathf.Lerp(2.5f, 15f, Mathf.Clamp01(speed / 5f)) : 4f) * (speed > 0.05f ? 1 : 0.15f);
            float amp = grounded ? Mathf.Clamp01(speed / 1.2f) : 0.3f;

            foreach (var l in legs)
            {
                float ph = phase + l.phaseOffset;
                float lift = Mathf.Max(0, Mathf.Sin(ph)) * 32 * amp;
                float push = Vector3.Dot(move, l.tangent);
                float swing = Mathf.Cos(ph) * 22 * amp * push;
                // reaching toward / tucking away from the direction of motion
                float reach = Mathf.Cos(ph) * 14 * amp * Vector3.Dot(move, l.outDir);
                float breathe = Mathf.Sin(idleT * 1.6f + l.phaseOffset) * 2.5f * (1 - amp);
                var hip = Quaternion.AngleAxis(swing, Vector3.up) * Quaternion.AngleAxis(lift + reach * 0.5f + breathe, l.liftAxis);
                var knee = Quaternion.AngleAxis(-lift * 0.6f - reach * 0.4f, l.liftAxis);
                if (!grounded)   // legs dangle and spread in the air
                {
                    hip *= Quaternion.AngleAxis(12, l.liftAxis);
                    knee = Quaternion.AngleAxis(18, l.liftAxis);
                }
                l.hip.Set(hip);
                l.knee?.Set(knee);
                l.ankle?.Set(Quaternion.AngleAxis(lift * 0.35f, l.liftAxis));
            }

            // the shell: a bounce in step with the legs, leaning into motion and with the ground
            bob = Mathf.Lerp(bob, Mathf.Abs(Mathf.Sin(phase)) * 0.02f * amp, dt * 10);
            var lean = Quaternion.Euler(local.z * 1.6f, 0, -local.x * 1.6f);
            var ground = Quaternion.FromToRotation(Vector3.up, transform.InverseTransformDirection(groundNormal));
            tilt = Quaternion.Slerp(tilt, ground * lean, dt * 6);
            body.localRotation = bodyRestRot * tilt;
            body.localPosition = bodyRest + Vector3.up * (bob + Mathf.Sin(idleT * 1.6f) * 0.004f * (1 - amp));

            Eyes(dt, speed);
            for (int s = 0; s < 2; s++) if (claws[s] != null) ClawUpdate(claws[s], dt);
        }

        void Eyes(float dt, float speed)
        {
            foreach (var e in new[] { eyeL, eyeR })
            {
                if (e == null) continue;
                var target = Quaternion.identity;
                if (lookAt.HasValue)
                {
                    var d = body.InverseTransformDirection((lookAt.Value - e.t.position).normalized);
                    float yaw = Mathf.Clamp(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, -50, 50);
                    float pitch = Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(d.y, -1, 1)) * Mathf.Rad2Deg, -35, 25);
                    target = Quaternion.Euler(pitch * 0.6f, yaw * 0.7f, 0);
                }
                // stalks sway back when running, perk up when thinking
                float sway = Mathf.Sin(idleT * 2.3f + (e == eyeL ? 0 : 1.3f)) * 3 + speed * 4;
                if (Pose == ClawPose.Think) sway -= 12;
                e.Toward(target * Quaternion.Euler(sway, 0, 0), dt * 8);
            }
        }

        static Quaternion Raise(float deg) => Quaternion.AngleAxis(-deg, Vector3.right);

        void ClawUpdate(Claw c, float dt)
        {
            bool right = c.side > 0;
            float p = PoseProgress;
            float open = 0.2f + 0.15f * Mathf.Sin(idleT * 1.3f + c.side);   // finger open 0..1
            var m = Raise(Mathf.Sin(idleT * 1.1f + c.side) * 4);
            var k = Quaternion.identity;
            switch (Pose)
            {
                case ClawPose.Strike when right:
                {
                    // wind up high and back, then a fast blow forward and down
                    float a = p < 0.35f ? Mathf.SmoothStep(0, 1, p / 0.35f) : 1 - Mathf.SmoothStep(0, 1, (p - 0.35f) / 0.25f) * 1.6f;
                    m = Raise(55 * a) * Quaternion.AngleAxis(-25 * a * c.side, Vector3.up);
                    k = Raise(30 * a);
                    open = 0;
                    break;
                }
                case ClawPose.Throw when right:
                {
                    float a = p < 0.5f ? p / 0.5f : 1 - (p - 0.5f) / 0.25f * 1.8f;
                    m = Raise(70 * a) * Quaternion.AngleAxis(-30 * a * c.side, Vector3.up);
                    open = p < 0.55f ? 0 : 1;
                    break;
                }
                case ClawPose.Aim when right:
                    m = Raise(24) * Quaternion.AngleAxis(-18 * c.side, Vector3.up);
                    k = Raise(-10);
                    open = 0;
                    break;
                case ClawPose.Create:
                case ClawPose.Absorb:
                {
                    float shiver = Mathf.Sin(idleT * 40) * 2;
                    m = Raise((Pose == ClawPose.Create ? 50 : 30) + shiver) * Quaternion.AngleAxis(-12 * c.side, Vector3.up);
                    k = Raise(-20);
                    open = 1;
                    break;
                }
                case ClawPose.Eat:
                {
                    float a = Mathf.Abs(Mathf.Sin((p * 3 + (right ? 0 : 0.5f)) * Mathf.PI));
                    m = Raise(20 + 25 * a) * Quaternion.AngleAxis(-35 * a * c.side, Vector3.up);
                    open = 1 - a;
                    break;
                }
                case ClawPose.Hurt:
                    m = Raise(40 * (1 - p)) * Quaternion.AngleAxis(25 * (1 - p) * c.side, Vector3.up);
                    open = 1;
                    break;
                case ClawPose.Think:
                    m = Raise(right ? 35 : 10) * Quaternion.AngleAxis(right ? -40 : 0, Vector3.up);
                    open = 0.1f;
                    break;
            }
            if (right && (Pose == ClawPose.Idle || Pose == ClawPose.Think) && gripRight && gripRight.childCount > 0) open = 0;   // holding something
            c.merus.Toward(m, dt * 14);
            c.carpus?.Toward(k, dt * 14);
            c.finger?.Toward(Quaternion.AngleAxis(open * 35, c.hinge), dt * 16);
        }
    }
}
