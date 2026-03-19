using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PointClickMovementInput : MonoBehaviour
{
    [Header("References")]
    public Camera mainCamera;
    public Transform playerRoot;
    public Rigidbody playerRb;

    [Header("Input")]
    public bool enablePointAndClick = true;
    public int mouseButton = 0;
    public LayerMask clickableLayers = ~0;

    [Header("Movement")]
    public float moveSpeed = 5f;
    public float arrivalDistance = 0.2f;
    public bool rotateToMoveDirection = true;
    public float rotationSpeed = 12f;

    [Header("Click Walk")]
    public float maxClickDistance = 1000f;
    public bool cancelWalkTargetOnManualMovement = false;
    public KeyCode moveForwardKey = KeyCode.W;
    public KeyCode moveBackwardKey = KeyCode.S;
    public KeyCode moveLeftKey = KeyCode.A;
    public KeyCode moveRightKey = KeyCode.D;

    [Header("Debug")]
    public bool drawDebug = true;
    public bool drawRaycastLine = true;
    public Color walkTargetColor = Color.cyan;
    public Color lastClickColor = Color.green;
    public Color rayHitColor = Color.yellow;
    public Color rayMissColor = Color.red;

    public bool HasWalkTarget { get; private set; }
    public Vector3 WalkTarget { get; private set; }
    public Vector3 DesiredMoveDirection { get; private set; }
    public Vector3 LastValidClickPoint { get; private set; }

    // Cached ray debug info
    private Vector3 lastRayOrigin;
    private Vector3 lastRayDirection;
    private float lastRayLength;
    private bool lastRayHit;
    private bool hasRayDebug;

    void Awake()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (playerRoot == null)
            playerRoot = transform;

        if (playerRb == null)
            playerRb = GetComponent<Rigidbody>();

        playerRb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    void Update()
    {
        if (!enablePointAndClick || mainCamera == null || playerRoot == null)
            return;

        if (cancelWalkTargetOnManualMovement && HasManualMovementInput())
            ClearWalkTarget();

        HandleClickInput();
        UpdateWalkDirection();
    }

    void FixedUpdate()
    {
        HandleMovement();
    }

    private void HandleClickInput()
    {
        if (!Input.GetMouseButtonDown(mouseButton))
            return;

        if (TryGetPointerClickPoint(out Vector3 hitPoint))
            SetWalkTarget(hitPoint);
    }

    private void UpdateWalkDirection()
    {
        if (!HasWalkTarget)
        {
            DesiredMoveDirection = Vector3.zero;
            return;
        }

        Vector3 currentPosition = playerRb.position;
        Vector3 toTarget = WalkTarget - currentPosition;
        Vector3 flatToTarget = Vector3.ProjectOnPlane(toTarget, Vector3.up);

        if (flatToTarget.magnitude <= arrivalDistance)
        {
            ClearWalkTarget();
            return;
        }

        DesiredMoveDirection = flatToTarget.normalized;
    }

    private void HandleMovement()
    {
        if (!HasWalkTarget)
            return;

        Vector3 currentPosition = playerRb.position;
        Vector3 toTarget = WalkTarget - currentPosition;
        Vector3 flatToTarget = Vector3.ProjectOnPlane(toTarget, Vector3.up);
        float distance = flatToTarget.magnitude;

        if (distance <= arrivalDistance)
        {
            ClearWalkTarget();
            return;
        }

        Vector3 moveDir = flatToTarget.normalized;
        float moveStep = moveSpeed * Time.fixedDeltaTime;

        Vector3 newPosition = moveStep >= distance
            ? currentPosition + flatToTarget
            : currentPosition + moveDir * moveStep;

        playerRb.MovePosition(newPosition);

        if (rotateToMoveDirection && moveDir.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDir, Vector3.up);
            Quaternion smoothedRotation = Quaternion.Slerp(
                playerRb.rotation,
                targetRotation,
                rotationSpeed * Time.fixedDeltaTime
            );

            playerRb.MoveRotation(smoothedRotation);
        }
    }

    private bool TryGetPointerClickPoint(out Vector3 point)
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        lastRayOrigin = ray.origin;
        lastRayDirection = ray.direction;
        hasRayDebug = true;

        if (Physics.Raycast(ray, out RaycastHit hit, maxClickDistance, clickableLayers, QueryTriggerInteraction.Ignore))
        {
            point = hit.point;

            lastRayHit = true;
            lastRayLength = hit.distance;

            return true;
        }

        point = Vector3.zero;

        lastRayHit = false;
        lastRayLength = maxClickDistance;

        return false;
    }

    private bool HasManualMovementInput()
    {
        return Input.GetKey(moveForwardKey) ||
               Input.GetKey(moveBackwardKey) ||
               Input.GetKey(moveLeftKey) ||
               Input.GetKey(moveRightKey);
    }

    public void ClearWalkTarget()
    {
        HasWalkTarget = false;
        DesiredMoveDirection = Vector3.zero;
    }

    public void SetWalkTarget(Vector3 worldPoint)
    {
        HasWalkTarget = true;
        WalkTarget = worldPoint;
        LastValidClickPoint = worldPoint;

        Vector3 currentPosition = playerRb != null ? playerRb.position : playerRoot.position;
        Vector3 flatToTarget = Vector3.ProjectOnPlane(WalkTarget - currentPosition, Vector3.up);

        DesiredMoveDirection = flatToTarget.sqrMagnitude > 0.0001f
            ? flatToTarget.normalized
            : Vector3.zero;
    }

    public float DistanceToWalkTarget(Vector3 currentPosition)
    {
        if (!HasWalkTarget)
            return 0f;

        return Vector3.ProjectOnPlane(WalkTarget - currentPosition, Vector3.up).magnitude;
    }

    void OnDrawGizmosSelected()
    {
        if (!drawDebug)
            return;

        if (drawRaycastLine && hasRayDebug)
        {
            Gizmos.color = lastRayHit ? rayHitColor : rayMissColor;
            Gizmos.DrawLine(lastRayOrigin, lastRayOrigin + lastRayDirection * lastRayLength);

            Vector3 rayEnd = lastRayOrigin + lastRayDirection * lastRayLength;
            Gizmos.DrawWireSphere(rayEnd, 0.08f);
        }

        if (HasWalkTarget)
        {
            Gizmos.color = walkTargetColor;
            Gizmos.DrawWireSphere(WalkTarget, 0.2f);

            if (playerRoot != null)
                Gizmos.DrawLine(playerRoot.position, WalkTarget);
        }
        else if (LastValidClickPoint != Vector3.zero)
        {
            Gizmos.color = lastClickColor;
            Gizmos.DrawWireSphere(LastValidClickPoint, 0.15f);
        }
    }
}