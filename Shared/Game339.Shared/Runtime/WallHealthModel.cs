using System;

namespace Game339.Shared.Runtime
{
    public class WallHealthModel
    {
        public int  MaxHealth     { get; }
        public int  CurrentHealth { get; private set; }
        public bool IsDestroyed   => CurrentHealth <= 0;

        public float NormalisedHealth =>
            MaxHealth <= 0 ? 0f : (float)Math.Floor((float)CurrentHealth / MaxHealth);

        public event Action<int, int> OnHealthChanged;  // (current, max)
        public event Action           OnDestroyed;

        public WallHealthModel(int maxHealth)
        {
            if (maxHealth <= 0) throw new ArgumentOutOfRangeException(nameof(maxHealth));
            MaxHealth     = maxHealth;
            CurrentHealth = maxHealth;
        }

        public int TakeDamage(int amount)
        {
            if (IsDestroyed || amount <= 0) return 0;

            int before    = CurrentHealth;
            CurrentHealth = Math.Max(0, CurrentHealth - amount);
            int dealt     = before - CurrentHealth;

            OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);

            if (IsDestroyed) OnDestroyed?.Invoke();

            return dealt;
        }

        public int Heal(int amount)
        {
            if (IsDestroyed || amount <= 0) return 0;

            int before    = CurrentHealth;
            CurrentHealth = Math.Min(MaxHealth, CurrentHealth + amount);
            int restored  = CurrentHealth - before;

            if (restored > 0) OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);

            return restored;
        }

        public void Reset()
        {
            CurrentHealth = MaxHealth;
            OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
        }
    }
}