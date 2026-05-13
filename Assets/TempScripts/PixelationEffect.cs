using UnityEngine;

[ExecuteInEditMode]
public class PixelationEffect : MonoBehaviour
{
    [Tooltip("Target horizontal resolution for pixelation. Lower values increase the pixelated effect.")]
    public int targetResolutionX = 120;

    [Tooltip("Material that uses the Custom/PixelationShader.")]
    public Material pixelationMaterial;

    private void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        if (pixelationMaterial != null)
        {
            pixelationMaterial.SetFloat("_PixelResolution", targetResolutionX);
            Graphics.Blit(src, dest, pixelationMaterial);
        }
        else
        {
            Graphics.Blit(src, dest);
        }
    }
}
