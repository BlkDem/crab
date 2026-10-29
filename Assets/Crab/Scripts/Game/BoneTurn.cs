// A moving part of a model — a rigid part (no turn at rest) or a skinned bone — turned in the
// body's axes about itself and laid over its pose at rest: the same code moves both.
using UnityEngine;

namespace Crab.Game
{
    public sealed class BoneTurn
    {
        public readonly Transform t;
        readonly Quaternion rest, frame;
        public Quaternion Turn { get; private set; } = Quaternion.identity;

        BoneTurn(Transform t, Transform body)
        {
            this.t = t;
            rest = t.localRotation;
            frame = Quaternion.Inverse(body.rotation) * t.rotation;
        }

        /// <summary>The part at rest now (null if there is none).</summary>
        public static BoneTurn Of(Transform t, Transform body) => t && body ? new BoneTurn(t, body) : null;

        /// <summary>Turn the part by q (in the body's axes, about the part).</summary>
        public void Set(Quaternion q)
        {
            Turn = q;
            t.localRotation = rest * (Quaternion.Inverse(frame) * q * frame);
        }

        public void Toward(Quaternion q, float k) => Set(Quaternion.Slerp(Turn, q, k));
    }
}
