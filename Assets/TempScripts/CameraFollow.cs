using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public class OrthoCameraFollow : MonoBehaviour
{
    [Header("References")]
    public Transform target;

    [Header("Follow")]
    public Vector3 targetOffset = Vector3.zero;
    public Vector3 worldOffset = new Vector3(-8f, 10f, -8f);
    public float focusSharpness = 14f;
    public float followSharpness = 12f;

    [Header("Look Ahead")]
    public bool enableLookAhead = true;
    public float lookAheadDistance = 0.5f;
    public float lookAheadSharpness = 10f;

    [Header("Zoom")]
    public bool allowZoom = true;
    public float orthoSize = 7f;
    public float minSize = 3f;
    public float maxSize = 12f;
    public float zoomSensitivity = 2f;
    public float zoomSharpness = 12f;

    [Header("Options")]
    public bool snapToTargetOnStart = true;
    public bool useUnscaledTime = false;

    private Camera cam;
    private Vector3 smoothedFocusPoint;
    private Vector3 smoothedLookAhead;
    private Vector3 lastTargetPosition;
    private float zoomTarget;

    private float DeltaTime => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
    private const float Tiny = 0.0001f;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        cam.orthographic = true;
    }

    private void Start()
    {
        zoomTarget = orthoSize;
        cam.orthographicSize = orthoSize;

        if (target != null)
        {
            Vector3 targetFocus = target.position + targetOffset;
            smoothedFocusPoint = targetFocus;
            lastTargetPosition = target.position;
        }

        if (snapToTargetOnStart)
            SnapInstant();
    }

    private void Update()
    {
        HandleZoom();

        cam.orthographic = true;
        cam.orthographicSize = DampFloat(
            cam.orthographicSize,
            zoomTarget,
            zoomSharpness,
            DeltaTime
        );
    }

    private void LateUpdate()
    {
        if (target == null || cam == null)
            return;

        UpdateCameraPose();
    }

    private void HandleZoom()
    {
        if (!allowZoom || Mouse.current == null)
            return;

        float scroll = Mouse.current.scroll.ReadValue().y;

        if (Mathf.Abs(scroll) > 0.01f)
        {
            zoomTarget = Mathf.Clamp(
                zoomTarget - scroll * 0.01f * zoomSensitivity,
                minSize,
                maxSize
            );
        }

        if (Mouse.current.middleButton.wasPressedThisFrame)
            zoomTarget = orthoSize;
    }

    private void UpdateCameraPose()
    {
        float dt = DeltaTime;
        Vector3 rawFocusPoint = target.position + targetOffset;

        Vector3 lookAhead = Vector3.zero;
        if (enableLookAhead && dt > 0f)
        {
            Vector3 delta = target.position - lastTargetPosition;
            delta.y = 0f;

            if (delta.sqrMagnitude > Tiny)
                lookAhead = delta.normalized * lookAheadDistance;
        }

        smoothedLookAhead = DampVector3(
            smoothedLookAhead,
            lookAhead,
            lookAheadSharpness,
            dt
        );

        Vector3 desiredFocus = rawFocusPoint + smoothedLookAhead;

        smoothedFocusPoint = DampVector3(
            smoothedFocusPoint,
            desiredFocus,
            focusSharpness,
            dt
        );

        Vector3 desiredPosition = smoothedFocusPoint + worldOffset;

        transform.position = DampVector3(
            transform.position,
            desiredPosition,
            followSharpness,
            dt
        );

        lastTargetPosition = target.position;
    }

    public void SnapInstant()
    {
        if (target == null || cam == null)
            return;

        Vector3 rawFocusPoint = target.position + targetOffset;
        smoothedFocusPoint = rawFocusPoint;
        smoothedLookAhead = Vector3.zero;
        transform.position = smoothedFocusPoint + worldOffset;
        cam.orthographic = true;
        cam.orthographicSize = zoomTarget;
        lastTargetPosition = target.position;
    }

    public void SetTarget(Transform newTarget, bool snapInstantly = true)
    {
        target = newTarget;

        if (target != null)
        {
            smoothedFocusPoint = target.position + targetOffset;
            lastTargetPosition = target.position;
        }

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

    private static float ExpDampFactor(float sharpness, float dt)
    {
        return 1f - Mathf.Exp(-sharpness * dt);
    }

    private static float DampFloat(float current, float target, float sharpness, float dt)
    {
        return Mathf.Lerp(current, target, ExpDampFactor(sharpness, dt));
    }

    private static Vector3 DampVector3(Vector3 current, Vector3 target, float sharpness, float dt)
    {
        return Vector3.Lerp(current, target, ExpDampFactor(sharpness, dt));
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

        focusSharpness = Mathf.Max(0.01f, focusSharpness);
        followSharpness = Mathf.Max(0.01f, followSharpness);
        lookAheadSharpness = Mathf.Max(0.01f, lookAheadSharpness);
        zoomSharpness = Mathf.Max(0.01f, zoomSharpness);
        lookAheadDistance = Mathf.Max(0f, lookAheadDistance);
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