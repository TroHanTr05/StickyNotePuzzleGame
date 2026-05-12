// ─────────────────────────────────────────────────────────────────────────────
//  WallHealth.cs
//
//  CHANGES FROM ORIGINAL:
//    • WallHealth now extends ObserverMonoBehaviour instead of MonoBehaviour.
//      Subscribe() / Unsubscribe() replace the raw event hookup that was in
//      Awake() and OnDestroy(). ObserverMonoBehaviour guarantees these are
//      called at the correct point in Unity's lifecycle and never double-fire.
//    • Debug.Log calls replaced with the injected IGameLog resolved from
//      ServiceResolver — consistent with the rest of the project's logging
//      infrastructure and keeps Unity out of the model layer.
//    • WallHealthModel itself is unchanged — it is pure C# and already
//      tested in Tests.cs.
// ─────────────────────────────────────────────────────────────────────────────
using System;
using UnityEngine;
using UnityEngine.UI;
using Game339.Shared.Infrastructure.Diagnostics;
using Game339.Shared.Runtime;

namespace Game.Runtime
{

    // ── Unity MonoBehaviour view — extends ObserverMonoBehaviour ─────────────
    public class WallHealth : ObserverMonoBehaviour
    {
        // Resolve logger the same way every other MonoBehaviour in the project does
        private static IGameLog Log => ServiceResolver.Resolve<IGameLog>();

        [Header("Health")]
        [SerializeField] private int maxHealth = 3;

        [Header("Damage")]
        [SerializeField] private int    damageFromBall   = 1;
        [SerializeField] private string ballTag          = "Ball";
        [SerializeField] private bool   destroyBallOnHit = true;

        [Header("World-Space Health Bar")]
        [Tooltip("Drag the Slider that lives on this wall's prefab here.")]
        [SerializeField] private Slider healthSlider;

        [Tooltip("Optional: GameObject wrapping the slider — hidden when wall is at full health.")]
        [SerializeField] private GameObject healthBarRoot;

        [Tooltip("Hide the bar when the wall is at full health.")]
        [SerializeField] private bool hideWhenFull = true;

        public WallHealthModel Model { get; private set; }

        public int  CurrentHealth => Model.CurrentHealth;
        public int  MaxHealth     => Model.MaxHealth;
        public bool IsDestroyed   => Model.IsDestroyed;

        protected override void Awake()
        {
            base.Awake();   // <-- always call base so ObserverMonoBehaviour can track state
            Model = new WallHealthModel(maxHealth);
            RefreshSlider();
        }

        // ── ObserverMonoBehaviour contract ────────────────────────────────────

        /// <summary>
        /// Called once by ObserverMonoBehaviour after Start() has run and the
        /// object is enabled. Safe to subscribe here — never called twice.
        /// </summary>
        protected override void Subscribe()
        {
            Model.OnHealthChanged += OnHealthChanged;
            Model.OnDestroyed     += OnWallDestroyed;
        }

        /// <summary>
        /// Called by ObserverMonoBehaviour whenever the object is disabled or
        /// destroyed. Always paired with Subscribe — no leak possible.
        /// </summary>
        protected override void Unsubscribe()
        {
            Model.OnHealthChanged -= OnHealthChanged;
            Model.OnDestroyed     -= OnWallDestroyed;
        }

        // ── Physics callbacks ────────────────────────────────────────────────

        private void OnCollisionEnter(Collision col) => HandleHit(col.gameObject);
        private void OnTriggerEnter(Collider other)  => HandleHit(other.gameObject);

        private void HandleHit(GameObject hit)
        {
            if (!hit.CompareTag(ballTag)) return;
            Model.TakeDamage(damageFromBall);
            if (destroyBallOnHit) Destroy(hit);
        }

        /// <summary>External damage entry point (e.g. from GameButton or traps).</summary>
        public void TakeDamage(int amount) => Model.TakeDamage(amount);

        // ── Model event handlers ─────────────────────────────────────────────

        private void OnHealthChanged(int current, int max)
        {
            Log.Info($"{gameObject.name} HP: {current}/{max}");
            RefreshSlider();
        }

        private void OnWallDestroyed()
        {
            Log.Info($"{gameObject.name} destroyed.");
            Destroy(gameObject);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private void RefreshSlider()
        {
            if (healthSlider == null) return;

            healthSlider.minValue = 0f;
            healthSlider.maxValue = 1f;
            healthSlider.value    = Model.NormalisedHealth;

            if (healthBarRoot != null && hideWhenFull)
                healthBarRoot.SetActive(!Mathf.Approximately(Model.NormalisedHealth, 1f));
        }
    }
}
