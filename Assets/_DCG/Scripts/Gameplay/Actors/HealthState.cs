using System;

namespace DCG.Gameplay
{
    public sealed class HealthState
    {
        public float Maximum { get; }
        public float Current { get; private set; }
        public bool IsAlive => Current > 0;
        public event Action Died;
        public HealthState(float maximum)
        {
            if (float.IsNaN(maximum) || float.IsInfinity(maximum) || maximum <= 0)
                throw new ArgumentOutOfRangeException(nameof(maximum));
            Maximum = Current = maximum;
        }
        public bool Apply(float amount)
        {
            if (!IsAlive || amount <= 0 || float.IsNaN(amount) || float.IsInfinity(amount)) return false;
            Current = Math.Max(0, Current - amount);
            if (!IsAlive) Died?.Invoke();
            return true;
        }
    }
}
