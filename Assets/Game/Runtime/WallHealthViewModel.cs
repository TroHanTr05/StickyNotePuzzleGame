using System;
using UnityEngine;
using Game339.Shared.Infrastructure.Diagnostics;
using Game339.Shared.Runtime;

namespace Game.Runtime
{
    public class WallHealthViewModel : MonoBehaviour
    {
        private static IGameLog Log => ServiceResolver.Resolve<IGameLog>();

        [Header("Health")]
        public int MaxHealth = 5;

        [Header("Damage")]
        public string BallTag = "Ball";
        public int DamagePerBallHit = 1;
        public bool DestroyBallOnHit = false;

        [Header("Destroy Wall")]
        public bool DestroyWallWhenDepleted = true;
        public float DestroyDelay = 0f;

        private WallHealthModel _model;

        public event Action<int, int> OnHealthChanged;
        public event Action<WallHealthViewModel> OnWallDestroyed;

        public int CurrentHealth => _model != null ? _model.CurrentHealth : MaxHealth;
        public int Max => _model != null ? _model.MaxHealth : MaxHealth;
        public float NormalisedHealth => _model != null ? _model.NormalisedHealth : 1f;
        public bool IsDestroyed => _model != null && _model.IsDestroyed;

        private void Awake()
        {
            _model = new WallHealthModel(MaxHealth);
            _model.OnHealthChanged += HandleHealthChanged;
            _model.OnDestroyed += HandleDestroyed;
        }

        private void Start()
        {
            OnHealthChanged?.Invoke(_model.CurrentHealth, _model.MaxHealth);
        }

        private void OnDestroy()
        {
            if (_model == null) return;

            _model.OnHealthChanged -= HandleHealthChanged;
            _model.OnDestroyed -= HandleDestroyed;
        }

        private void OnCollisionEnter(Collision collision)
        {
            TryDamageFrom(collision.gameObject);
        }

        private void OnTriggerEnter(Collider other)
        {
            TryDamageFrom(other.gameObject);
        }

        private void TryDamageFrom(GameObject other)
        {
            if (!other.CompareTag(BallTag)) return;

            TakeDamage(DamagePerBallHit);

            if (DestroyBallOnHit)
                Destroy(other);
        }

        public int TakeDamage(int amount)
        {
            if (_model == null) return 0;

            int damageDealt = _model.TakeDamage(amount);

            if (damageDealt > 0)
                Log.Info($"[WallHealth] {gameObject.name} took {damageDealt} damage. Health: {_model.CurrentHealth}/{_model.MaxHealth}");

            return damageDealt;
        }

        private void HandleHealthChanged(int current, int max)
        {
            OnHealthChanged?.Invoke(current, max);
        }

        private void HandleDestroyed()
        {
            Log.Info($"[WallHealth] {gameObject.name} destroyed.");

            OnWallDestroyed?.Invoke(this);

            if (DestroyWallWhenDepleted)
                Destroy(gameObject, DestroyDelay);
        }
    }
}