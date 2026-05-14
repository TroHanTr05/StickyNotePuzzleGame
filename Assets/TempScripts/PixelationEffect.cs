using UnityEngine;

[ExecuteInEditMode]
[RequireComponent(typeof(Camera))]
public class PixelationEffect : MonoBehaviour
{
    [Tooltip("Target horizontal resolution for pixelation. Lower values = more pixelated.")]
    [Range(32, 512)]
    public int targetResolutionX = 120;

    [Tooltip("Material using the Custom/PixelationShaderUnity3D shader.")]
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