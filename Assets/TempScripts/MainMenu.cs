using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

public class SceneChanger : MonoBehaviour
{
    [Tooltip("Full-screen UI Image that will be used for fading.")]
    public Image fadeImage;
    
    [Tooltip("Duration of the fade out in seconds.")]
    public float fadeDuration = 1f;

    // Optional: Reference to the pixelation effect on the main camera.
    private PixelationEffect pixelationEffect;

    private void Start()
    {
        // Optionally, disable the pixelation effect on start so the game appears crisp.
        if (Camera.main != null)
        {
            pixelationEffect = Camera.main.GetComponent<PixelationEffect>();
            if (pixelationEffect != null)
            {
                pixelationEffect.enabled = false;
                // Ensure full resolution at start.
                pixelationEffect.targetResolutionX = 60;
            }
        }

        // Make sure our fade image starts fully transparent.
        if (fadeImage != null)
        {
            fadeImage.color = new Color(0, 0, 0, 0);
        }
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            // Begin fade out then change scene.
            StartCoroutine(FadeOutAndChangeScene("Game"));
        }
    }

    private IEnumerator FadeOutAndChangeScene(string sceneName)
    {
        // Enable the pixelation effect for the fade (if available).
        if (pixelationEffect != null)
        {
            pixelationEffect.enabled = true;
        }
        
        float elapsedTime = 0f;
        // Start fade from clear to black.
        while (elapsedTime < fadeDuration)
        {
            float alpha = Mathf.Lerp(0f, 1f, elapsedTime / fadeDuration);
            if (fadeImage != null)
            {
                fadeImage.color = new Color(0, 0, 0, alpha);
            }

            // Optionally transition the pixelation from full resolution (60) to a low resolution (0) for the effect.
            if (pixelationEffect != null)
            {
                pixelationEffect.targetResolutionX = Mathf.RoundToInt(Mathf.Lerp(60, 0, elapsedTime / fadeDuration));
            }

            elapsedTime += Time.deltaTime;
            yield return null;
        }
        
        // Ensure the image is completely black.
        if (fadeImage != null)
        {
            fadeImage.color = new Color(0, 0, 0, 1);
        }
        if (pixelationEffect != null)
        {
            pixelationEffect.targetResolutionX = 0;
        }

        // Now load the specified scene.
        SceneManager.LoadScene(sceneName);
    }
}