using UnityEngine;
using UnityEngine.UI;

public class WallHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private int maxHealth = 3;

    [Header("Damage Settings")]
    [SerializeField] private int damageFromBall = 1;
    [SerializeField] private string ballTag = "Ball";
    [SerializeField] private bool destroyBallOnHit = true;

    [Header("Optional UI")]
    [SerializeField] private Slider healthBar;

    private int currentHealth;
    private bool isDestroyed = false;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public bool IsDestroyed => isDestroyed;

    private void Awake()
    {
        currentHealth = maxHealth;
        UpdateHealthBar();
    }

    private void OnCollisionEnter(Collision collision)
    {
        HandleHit(collision.gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleHit(other.gameObject);
    }

    private void HandleHit(GameObject hitObject)
    {
        if (isDestroyed) return;

        if (!hitObject.CompareTag(ballTag)) return;

        TakeDamage(damageFromBall);

        if (destroyBallOnHit)
        {
            Destroy(hitObject);
        }
    }

    public void TakeDamage(int damageAmount)
    {
        if (isDestroyed) return;

        currentHealth -= damageAmount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        UpdateHealthBar();

        Debug.Log($"{gameObject.name} took damage. Health: {currentHealth}");

        if (currentHealth <= 0)
        {
            DestroyWall();
        }
    }

    private void UpdateHealthBar()
    {
        if (healthBar == null) return;

        healthBar.maxValue = maxHealth;
        healthBar.value = currentHealth;
    }

    private void DestroyWall()
    {
        isDestroyed = true;

        Debug.Log($"{gameObject.name} destroyed.");

        Destroy(gameObject);
    }
}