using UnityEngine;

[RequireComponent(typeof(Camera))]
public class OrthoCameraFollow : MonoBehaviour
{
    [Header("References")]
    public Transform target;

    [Header("Follow Settings")]
    public Vector3 targetOffset = Vector3.zero;
    public Vector3 worldOffset = new Vector3(-8f, 10f, -8f);
    public float followSmoothTime = 0.12f;

    [Header("Zoom")]
    public bool allowZoom = true;
    public float orthoSize = 7f;
    public float minSize = 3f;
    public float maxSize = 12f;
    public float zoomSensitivity = 2f;
    public float zoomResetSpeed = 8f;
    public KeyCode resetZoomKey = KeyCode.Mouse2;

    [Header("Options")]
    public bool snapToTargetOnStart = true;
    public bool useUnscaledTime = false;

    private Camera cam;
    private Vector3 currentVelocity;
    private float zoomTarget;

    private float DeltaTime => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

    void Awake()
    {
        cam = GetComponent<Camera>();
        cam.orthographic = true;
    }

    void Start()
    {
        zoomTarget = orthoSize;
        cam.orthographicSize = orthoSize;

        if (snapToTargetOnStart)
            SnapInstant();
    }

    void Update()
    {
        if (cam == null)
            return;

        HandleZoom();

        cam.orthographic = true;
        cam.orthographicSize = Mathf.Lerp(
            cam.orthographicSize,
            zoomTarget,
            DeltaTime * zoomResetSpeed
        );
    }

    void LateUpdate()
    {
        if (target == null || cam == null)
            return;

        UpdateCameraPose();
    }

    private void HandleZoom()
    {
        if (!allowZoom)
            return;

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.01f)
        {
            zoomTarget = Mathf.Clamp(
                zoomTarget - scroll * zoomSensitivity,
                minSize,
                maxSize
            );
        }

        if (Input.GetKeyDown(resetZoomKey))
            zoomTarget = orthoSize;
    }

    private void UpdateCameraPose()
    {
        Vector3 focusPoint = target.position + targetOffset;
        Vector3 desiredPosition = focusPoint + worldOffset;

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref currentVelocity,
            followSmoothTime
        );
    }

    public void SnapInstant()
    {
        if (target == null || cam == null)
            return;

        Vector3 focusPoint = target.position + targetOffset;
        Vector3 desiredPosition = focusPoint + worldOffset;

        transform.position = desiredPosition;
        cam.orthographic = true;
        cam.orthographicSize = zoomTarget;
        currentVelocity = Vector3.zero;
    }

    public void SetTarget(Transform newTarget, bool snapInstantly = true)
    {
        target = newTarget;

        if (snapInstantly)
            SnapInstant();
    }

    public void SetZoom(float newSize)
    {
        zoomTarget = Mathf.Clamp(newSize, minSize, maxSize);
    }

    public void ResetZoom()
    {
        zoomTarget = orthoSize;
    }

    private void OnValidate()
    {
        if (cam == null)
            cam = GetComponent<Camera>();

        if (cam != null)
            cam.orthographic = true;

        orthoSize = Mathf.Max(0.01f, orthoSize);
        minSize = Mathf.Max(0.01f, minSize);
        maxSize = Mathf.Max(minSize, maxSize);
        followSmoothTime = Mathf.Max(0f, followSmoothTime);
        zoomResetSpeed = Mathf.Max(0f, zoomResetSpeed);
    }

    private void OnDrawGizmosSelected()
    {
        if (target == null)
            return;

        Vector3 focusPoint = target.position + targetOffset;
        Vector3 desiredPosition = focusPoint + worldOffset;

        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(focusPoint, 0.08f);

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(focusPoint, desiredPosition);
        Gizmos.DrawSphere(desiredPosition, 0.08f);
    }
}