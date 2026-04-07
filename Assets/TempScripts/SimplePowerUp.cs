using UnityEngine;

public class SimplePowerUp : MonoBehaviour
{
    public bool givesBalls = false;
    public bool givesGrapple = false;
    public bool givesGlide = false;

    public MonoBehaviour ballThrowScript;
    public MonoBehaviour grappleScript;
    public MonoBehaviour flyScript;

    private void OnCollisionEnter(Collision collision)
    {
        if (!collision.gameObject.CompareTag("Player")) return;

        if (givesBalls && ballThrowScript != null)
            ballThrowScript.enabled = true;

        if (givesGrapple && grappleScript != null)
            grappleScript.enabled = true;

        if (givesGlide && flyScript != null)
            flyScript.enabled = true;

        Destroy(gameObject);
    }
}