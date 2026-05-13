using UnityEngine;

[RequireComponent(typeof(Camera))]
public class PixelationManager2D_v5 : MonoBehaviour
{
    [System.Serializable]
    public class ShadowLayer
    {
        public string layerName = "Default";
        public bool renderInFront = false;
        public Vector2 shadowOffset = new Vector2(0.1f, -0.1f);
        public Color shadowColor = new Color(0f, 0f, 0.5f);

        [HideInInspector] public int layerMask;
        [HideInInspector] public RenderTexture rt;
        [HideInInspector] public Camera shadowCam;
    }

    [Header("Pixelation Settings")]
    public LayerMask pixelateLayer;
    public LayerMask nonPixelatedForegroundLayer;
    public float pixelsPerUnit = 24f;
    public Shader compositeShader;
    public Shader alphaCompositeShader;

    [Header("Shadow Settings")]
    public bool enableShadows = true;
    public bool useRelativeToCameraUp = true;

    [Tooltip("Assign the Hidden/ShadowComposite shader here. This keeps builds from stripping the shadow shader.")]
    public Shader shadowCompositeShader;

    public ShadowLayer[] shadowLayers;

    private Camera mainCamera;
    private Camera pixelCamera;
    private Camera foregroundCamera;

    private RenderTexture pixelRT;
    private RenderTexture foregroundRT;

    private Material compositeMaterial;
    private Material shadowCompositeMat;
    private Material alphaCompositeMaterial;

    void Start()
    {
        mainCamera = GetComponent<Camera>();

#if UNITY_IOS || UNITY_ANDROID
        mainCamera.orthographicSize = Screen.height / (2f * pixelsPerUnit);
#endif

        mainCamera.cullingMask &= ~pixelateLayer;
        mainCamera.cullingMask &= ~nonPixelatedForegroundLayer;

        SetupPixelCamera();
        SetupForegroundCamera();
        SetupCompositeMaterial();
        SetupAlphaCompositeMaterial();
        SetupShadowCompositeMaterial();

        if (enableShadows && shadowLayers != null && shadowLayers.Length > 0 && shadowCompositeMat != null)
        {
            for (int i = 0; i < shadowLayers.Length; i++)
                SetupShadowLayer(shadowLayers[i], i);
        }

        UpdateRenderTexture();
    }

    void SetupPixelCamera()
    {
        GameObject pixelCamObj = new GameObject("PixelationCamera");
        pixelCamObj.transform.parent = mainCamera.transform;
        pixelCamObj.transform.localPosition = Vector3.zero;
        pixelCamObj.transform.localRotation = Quaternion.identity;
        pixelCamObj.transform.localScale = Vector3.one;

        pixelCamera = pixelCamObj.AddComponent<Camera>();
        pixelCamera.CopyFrom(mainCamera);
        pixelCamera.cullingMask = pixelateLayer & ~nonPixelatedForegroundLayer;
        pixelCamera.clearFlags = CameraClearFlags.SolidColor;
        pixelCamera.backgroundColor = Color.clear;
        pixelCamera.depth = mainCamera.depth - 1;
        pixelCamera.enabled = false;
    }

    void SetupForegroundCamera()
    {
        GameObject foregroundCamObj = new GameObject("NonPixelatedForegroundCamera");
        foregroundCamObj.transform.parent = mainCamera.transform;
        foregroundCamObj.transform.localPosition = Vector3.zero;
        foregroundCamObj.transform.localRotation = Quaternion.identity;
        foregroundCamObj.transform.localScale = Vector3.one;

        foregroundCamera = foregroundCamObj.AddComponent<Camera>();
        foregroundCamera.CopyFrom(mainCamera);
        foregroundCamera.cullingMask = nonPixelatedForegroundLayer;
        foregroundCamera.clearFlags = CameraClearFlags.SolidColor;
        foregroundCamera.backgroundColor = Color.clear;
        foregroundCamera.depth = mainCamera.depth + 10;
        foregroundCamera.enabled = false;
    }

    void SetupCompositeMaterial()
    {
        if (compositeShader == null)
        {
            Debug.LogError("Composite shader not assigned!");
            return;
        }

        compositeMaterial = new Material(compositeShader);
    }

    void SetupAlphaCompositeMaterial()
    {
        if (alphaCompositeShader == null)
        {
            Debug.LogError("Alpha Composite shader not assigned!");
            return;
        }

        alphaCompositeMaterial = new Material(alphaCompositeShader);
    }

    void SetupShadowCompositeMaterial()
    {
        Shader shaderToUse = shadowCompositeShader;

        // Inspector assignment is build-safe. Shader.Find is only a fallback for old scenes.
        if (shaderToUse == null)
            shaderToUse = Shader.Find("Hidden/ShadowComposite");

        if (shaderToUse == null)
        {
            Debug.LogError("Shadow Composite shader not assigned/found. Assign Hidden/ShadowComposite to shadowCompositeShader.");
            return;
        }

        shadowCompositeMat = new Material(shaderToUse);
    }

    void SetupShadowLayer(ShadowLayer layer, int index)
    {
        int unityLayer = LayerMask.NameToLayer(layer.layerName);

        if (unityLayer < 0)
        {
            Debug.LogError($"Layer '{layer.layerName}' does not exist.");
            return;
        }

        layer.layerMask = 1 << unityLayer;

        GameObject camObj = new GameObject($"ShadowCam_{layer.layerName}");
        camObj.transform.SetParent(transform);
        camObj.transform.localPosition = Vector3.zero;
        camObj.transform.localRotation = Quaternion.identity;

        layer.shadowCam = camObj.AddComponent<Camera>();
        layer.shadowCam.CopyFrom(mainCamera);
        layer.shadowCam.cullingMask = layer.layerMask;
        layer.shadowCam.clearFlags = CameraClearFlags.SolidColor;
        layer.shadowCam.backgroundColor = Color.clear;
        layer.shadowCam.depth = mainCamera.depth - 10 - index;
        layer.shadowCam.enabled = false;
    }

    void Update()
    {
        SyncExtraCameras();
        UpdateRenderTexture();
    }

    void UpdateRenderTexture()
    {
        if (mainCamera == null) return;

        // Intentionally kept exactly like your working version so pixelation density/behavior does not change.
        float distance = Mathf.Abs(mainCamera.transform.position.z);
        float fovRadians = mainCamera.fieldOfView * Mathf.Deg2Rad;
        float worldHeight = 2f * distance * Mathf.Tan(fovRadians * 0.5f);

        int rtHeight = Mathf.Max(1, Mathf.RoundToInt(worldHeight * pixelsPerUnit));
        int rtWidth = Mathf.Max(1, Mathf.RoundToInt(rtHeight * mainCamera.aspect));

        SetupRT(ref pixelRT, pixelCamera, rtWidth, rtHeight, FilterMode.Point);
        SetupRT(ref foregroundRT, foregroundCamera, Screen.width, Screen.height, FilterMode.Bilinear);

        if (compositeMaterial != null)
            compositeMaterial.SetTexture("_PixelTex", pixelRT);

        if (enableShadows && shadowLayers != null)
        {
            foreach (ShadowLayer layer in shadowLayers)
                SetupRT(ref layer.rt, layer.shadowCam, rtWidth, rtHeight, FilterMode.Point);
        }
    }

    void SetupRT(ref RenderTexture rt, Camera cam, int width, int height, FilterMode filterMode)
    {
        if (cam == null) return;

        if (rt == null || rt.width != width || rt.height != height)
        {
            if (rt != null)
            {
                rt.Release();
                Destroy(rt);
            }

            rt = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32);
            rt.filterMode = filterMode;
            rt.wrapMode = TextureWrapMode.Clamp;
            rt.Create();

            cam.targetTexture = rt;
        }
    }

    void SyncExtraCameras()
    {
        SyncCamera(pixelCamera);
        SyncCamera(foregroundCamera);

        if (enableShadows && shadowLayers != null)
        {
            foreach (ShadowLayer layer in shadowLayers)
                SyncCamera(layer.shadowCam);
        }
    }

    void SyncCamera(Camera cam)
    {
        if (mainCamera == null || cam == null)
            return;

        cam.transform.position = mainCamera.transform.position;
        cam.transform.rotation = mainCamera.transform.rotation;
        cam.orthographic = mainCamera.orthographic;
        cam.orthographicSize = mainCamera.orthographicSize;
        cam.fieldOfView = mainCamera.fieldOfView;
        cam.aspect = mainCamera.aspect;
        cam.rect = mainCamera.rect;
        cam.projectionMatrix = mainCamera.projectionMatrix;
    }

    void OnPreRender()
    {
        SyncExtraCameras();

        if (pixelCamera != null && pixelRT != null)
            pixelCamera.Render();

        if (foregroundCamera != null && foregroundRT != null)
            foregroundCamera.Render();

        if (enableShadows && shadowLayers != null)
        {
            foreach (ShadowLayer layer in shadowLayers)
            {
                if (layer.shadowCam != null && layer.rt != null)
                    layer.shadowCam.Render();
            }
        }
    }

    Vector2 GetPixelOffset(ShadowLayer layer, int screenHeight)
    {
        Vector2 offset = layer.shadowOffset;

        if (useRelativeToCameraUp && mainCamera != null)
        {
            Vector3 camRight = mainCamera.transform.right;
            Vector3 camUp = mainCamera.transform.up;
            Vector3 worldOffset = camRight * layer.shadowOffset.x + camUp * layer.shadowOffset.y;
            offset = new Vector2(worldOffset.x, worldOffset.y);
        }

        return new Vector2(
            offset.x / mainCamera.orthographicSize * screenHeight * 0.5f,
            offset.y / mainCamera.orthographicSize * screenHeight * 0.5f
        );
    }

    void ApplyShadowLayer(ref RenderTexture current, RenderTexture src, ShadowLayer layer)
    {
        if (shadowCompositeMat == null || layer.rt == null)
            return;

        Vector2 pixelOffset = GetPixelOffset(layer, src.height);

        shadowCompositeMat.SetTexture("_ShadowTex", layer.rt);
        shadowCompositeMat.SetColor("_ShadowColor", layer.shadowColor);
        shadowCompositeMat.SetVector("_ShadowOffset", pixelOffset);
        shadowCompositeMat.SetFloat("_RenderInFront", layer.renderInFront ? 1f : 0f);

        RenderTexture temp = RenderTexture.GetTemporary(src.width, src.height, 0, src.format);
        Graphics.Blit(current, temp, shadowCompositeMat);
        RenderTexture.ReleaseTemporary(current);
        current = temp;
    }

    void CompositeTextureOnTop(ref RenderTexture current, RenderTexture overlay)
    {
        if (overlay == null || alphaCompositeMaterial == null)
            return;

        RenderTexture temp = RenderTexture.GetTemporary(current.width, current.height, 0, current.format);

        alphaCompositeMaterial.SetTexture("_OverlayTex", overlay);
        Graphics.Blit(current, temp, alphaCompositeMaterial);

        RenderTexture.ReleaseTemporary(current);
        current = temp;
    }

    void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        if (compositeMaterial == null)
        {
            Graphics.Blit(src, dest);
            return;
        }

        RenderTexture current = RenderTexture.GetTemporary(src.width, src.height, 0, src.format);
        Graphics.Blit(src, current);

        if (enableShadows && shadowCompositeMat != null && shadowLayers != null)
        {
            for (int i = shadowLayers.Length - 1; i >= 0; i--)
            {
                ShadowLayer layer = shadowLayers[i];

                if (layer.rt == null || layer.renderInFront)
                    continue;

                ApplyShadowLayer(ref current, src, layer);
            }
        }

        if (pixelRT != null)
        {
            compositeMaterial.SetTexture("_PixelTex", pixelRT);

            RenderTexture afterPixel = RenderTexture.GetTemporary(src.width, src.height, 0, src.format);
            Graphics.Blit(current, afterPixel, compositeMaterial);
            RenderTexture.ReleaseTemporary(current);
            current = afterPixel;
        }

        if (enableShadows && shadowCompositeMat != null && shadowLayers != null)
        {
            for (int i = shadowLayers.Length - 1; i >= 0; i--)
            {
                ShadowLayer layer = shadowLayers[i];

                if (layer.rt == null || !layer.renderInFront)
                    continue;

                ApplyShadowLayer(ref current, src, layer);
            }
        }

        CompositeTextureOnTop(ref current, foregroundRT);

        Graphics.Blit(current, dest);
        RenderTexture.ReleaseTemporary(current);
    }

    void OnDestroy()
    {
        ReleaseRT(pixelRT);
        ReleaseRT(foregroundRT);

        if (enableShadows && shadowLayers != null)
        {
            foreach (ShadowLayer layer in shadowLayers)
            {
                ReleaseRT(layer.rt);

                if (layer.shadowCam != null)
                    Destroy(layer.shadowCam.gameObject);
            }
        }

        if (pixelCamera != null)
            Destroy(pixelCamera.gameObject);

        if (foregroundCamera != null)
            Destroy(foregroundCamera.gameObject);

        if (compositeMaterial != null)
            Destroy(compositeMaterial);

        if (shadowCompositeMat != null)
            Destroy(shadowCompositeMat);

        if (alphaCompositeMaterial != null)
            Destroy(alphaCompositeMaterial);
    }

    void ReleaseRT(RenderTexture rt)
    {
        if (rt == null) return;

        rt.Release();
        Destroy(rt);
    }
}
