// Life of the crab or of a creature: damage, healing, death.
using System;

namespace Crab.Core
{
    public sealed class Health
    {
        public float Max { get; private set; }
        public float Value { get; private set; }
        public bool Dead => Value <= 0;
        public event Action<float> Damaged;
        public event Action Died, Changed;

        public Health(float max)
        {
            Max = max;
            Value = max;
        }

        /// <summary>Take damage; true if this blow killed.</summary>
        public bool Hurt(float amount)
        {
            if (Dead || amount <= 0) return false;
            Value = Math.Max(0, Value - amount);
            Damaged?.Invoke(amount);
            Changed?.Invoke();
            if (Value > 0) return false;
            Died?.Invoke();
            return true;
        }

        public void Heal(float amount)
        {
            if (Dead || amount <= 0) return;
            Value = Math.Min(Max, Value + amount);
            Changed?.Invoke();
        }

        public void Reset(float max)
        {
            Max = max;
            Value = max;
            Changed?.Invoke();
        }
    }
}
