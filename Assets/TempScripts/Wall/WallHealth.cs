using UnityEngine;
using UnityEngine.UI;

public class WallHealth : MonoBehaviour
{
    [Header("Wall Health")]
    public int maxHealth = 3;
    private int currentHealth;

    [Header("Health Bar")]
    public Slider healthBar;

    [Header("Damage Settings")]
    public int damageFromBall = 1;
    public string ballTag = "Ball";
    public bool destroyBallAfterHit = true;

    private bool isDestroyed = false;

    void Start()
    {
        currentHealth = maxHealth;
        UpdateHealthBar();
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag(ballTag))
        {
            TakeDamage(damageFromBall);

            if (destroyBallAfterHit)
            {
                Destroy(collision.gameObject);
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag(ballTag))
        {
            TakeDamage(damageFromBall);

            if (destroyBallAfterHit)
            {
                Destroy(other.gameObject);
            }
        }
    }

    public void TakeDamage(int damageAmount)
    {
        if (isDestroyed) return;

        currentHealth -= damageAmount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        UpdateHealthBar();

        Debug.Log("Wall took damage. Current health: " + currentHealth);

        if (currentHealth <= 0)
        {
            DestroyWall();
        }
    }

    void UpdateHealthBar()
    {
        if (healthBar != null)
        {
            healthBar.maxValue = maxHealth;
            healthBar.value = currentHealth;
        }
    }

    void DestroyWall()
    {
        isDestroyed = true;
        Debug.Log("Wall destroyed.");
        Destroy(gameObject);
    }
}