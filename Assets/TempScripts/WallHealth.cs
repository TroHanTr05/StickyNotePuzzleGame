using System;
using UnityEngine;
using UnityEngine.UI;

public class WallHealthModel
{
    public int MaxHealth    { get; }
    public int CurrentHealth { get; private set; }
    public bool IsDestroyed => CurrentHealth <= 0;

    public float NormalisedHealth =>
        MaxHealth <= 0 ? 0f : Mathf.Clamp01((float)CurrentHealth / MaxHealth);

    public event Action<int, int> OnHealthChanged;   // (current, max)
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

public class WallHealth : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private int maxHealth = 3;

    [Header("Damage")]
    [SerializeField] private int    damageFromBall   = 1;
    [SerializeField] private string ballTag          = "Ball";
    [SerializeField] private bool   destroyBallOnHit = true;

    [Header("World-Space Health Bar")]
    [Tooltip("Drag the Slider that lives on this wall's prefab here. " +
             "The Slider's Canvas should be set to World Space and parented " +
             "to the wall so it moves with it.")]
    [SerializeField] private Slider healthSlider;

    [Tooltip("Optional: GameObject wrapping the slider — hidden when wall is at full health.")]
    [SerializeField] private GameObject healthBarRoot;

    [Tooltip("Hide the bar when the wall is at full health.")]
    [SerializeField] private bool hideWhenFull = true;

    public WallHealthModel Model { get; private set; }

    public int  CurrentHealth => Model.CurrentHealth;
    public int  MaxHealth     => Model.MaxHealth;
    public bool IsDestroyed   => Model.IsDestroyed;

    private void Awake()
    {
        Model = new WallHealthModel(maxHealth);
        Model.OnHealthChanged += OnHealthChanged;
        Model.OnDestroyed     += OnWallDestroyed;
        RefreshSlider();
    }

    private void OnDestroy()
    {
        if (Model == null) return;
        Model.OnHealthChanged -= OnHealthChanged;
        Model.OnDestroyed     -= OnWallDestroyed;
    }

    private void OnCollisionEnter(Collision col) => HandleHit(col.gameObject);
    private void OnTriggerEnter(Collider other)  => HandleHit(other.gameObject);

    private void HandleHit(GameObject hit)
    {
        if (!hit.CompareTag(ballTag)) return;

        Model.TakeDamage(damageFromBall);          // model fires events

        if (destroyBallOnHit) Destroy(hit);
    }

    public void TakeDamage(int amount) => Model.TakeDamage(amount);

    void OnHealthChanged(int current, int max)
    {
        Debug.Log($"{gameObject.name} HP: {current}/{max}");
        RefreshSlider();
    }

    void OnWallDestroyed()
    {
        Debug.Log($"{gameObject.name} destroyed.");
        Destroy(gameObject);
    }

    void RefreshSlider()
    {
        if (healthSlider == null) return;

        healthSlider.minValue = 0f;
        healthSlider.maxValue = 1f;                // always use 0-1 range
        healthSlider.value    = Model.NormalisedHealth;

        if (healthBarRoot != null && hideWhenFull)
            healthBarRoot.SetActive(!Mathf.Approximately(Model.NormalisedHealth, 1f));
    }
}
