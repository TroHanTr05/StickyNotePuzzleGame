using UnityEngine;

public class SimplePowerUp : MonoBehaviour
{
    [Header("Which abilities does this power-up grant?")]
    public bool givesBalls  = false;
    public bool givesGrapple = false;
    public bool givesGlide  = false;

    [Header("Reference")]
    [Tooltip("Drag the SnakeController here, or leave null to auto-find by Player tag.")]
    public SnakeController snakeController;

    private void OnCollisionEnter(Collision collision)
    {
        if (!collision.gameObject.CompareTag("Player")) return;

        // Auto-find if not assigned
        if (snakeController == null)
            snakeController = collision.gameObject.GetComponentInParent<SnakeController>();

        if (snakeController == null) return;

        if (givesBalls)   snakeController.HasBalls   = true;
        if (givesGrapple) snakeController.HasGrapple  = true;
        if (givesGlide)   snakeController.HasGlide    = true;

        Destroy(gameObject);
    }
}
