using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class OrthoSmoothClickMover3D : MonoBehaviour
{
    [Header("References")]
    public Camera mainCamera;
    public CharacterController controller;
    public Transform visualRoot;

    [Header("Input")]
    public bool enableClickToMove = true;
    public int mouseButton = 0;
    public float maxClickDistance = 1000f;
    public LayerMask groundLayers;
    public LayerMask obstacleLayers;

    [Header("Movement")]
    public float moveSpeed = 5f;
    public float acceleration = 14f;
    public float deceleration = 20f;
    public float arrivalDistance = 0.06f;
    public float slowDownDistance = 1.0f;
    public float rotationSharpness = 18f;

    [Header("Grounding")]
    public float groundProbeStartHeight = 1.5f;
    public float groundProbeDistance = 4f;
    public float groundStickVelocity = 8f;
    public float groundedSnapDistance = 0.6f;
    public float maxGroundAngle = 60f;
    public float groundOffset = 0.02f;

    [Header("Collision Sliding")]
    public float shell = 0.02f;
    public int maxSlideIterations = 3;
    public float minRemainingDistance = 0.001f;

    [Header("Debug")]
    public bool drawDebug = false;
    public Color targetColor = Color.cyan;
    public Color groundProbeColor = Color.yellow;
    public Color slideColor = Color.green;

    public bool HasMoveTarget { get; private set; }
    public Vector3 MoveTarget { get; private set; }
    public Vector3 LastClickedPoint { get; private set; }

    private Vector3 planarVelocity;
    private Vector3 desiredFacing;
    private float verticalVelocity;
    private bool hasGround;
    private Vector3 lastGroundNormal = Vector3.up;

    private const float Tiny = 0.0001f;

    private void Awake()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (controller == null) controller = GetComponent<CharacterController>();
        if (visualRoot == null) visualRoot = transform;

        controller.enableOverlapRecovery = true;
        controller.minMoveDistance = 0f;
    }

    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0.01f, moveSpeed);
        acceleration = Mathf.Max(0.01f, acceleration);
        deceleration = Mathf.Max(0.01f, deceleration);
        arrivalDistance = Mathf.Max(0.001f, arrivalDistance);
        slowDownDistance = Mathf.Max(arrivalDistance, slowDownDistance);
        rotationSharpness = Mathf.Max(0.01f, rotationSharpness);

        groundProbeStartHeight = Mathf.Max(0.01f, groundProbeStartHeight);
        groundProbeDistance = Mathf.Max(0.01f, groundProbeDistance);
        groundStickVelocity = Mathf.Max(0.01f, groundStickVelocity);
        groundedSnapDistance = Mathf.Max(0.01f, groundedSnapDistance);
        maxGroundAngle = Mathf.Clamp(maxGroundAngle, 1f, 89f);
        groundOffset = Mathf.Max(0f, groundOffset);

        shell = Mathf.Max(0.001f, shell);
        maxSlideIterations = Mathf.Clamp(maxSlideIterations, 1, 8);
        minRemainingDistance = Mathf.Max(0.0001f, minRemainingDistance);
    }

    private void Start()
    {
        SnapToGroundImmediate();
        desiredFacing = transform.forward.sqrMagnitude > Tiny ? transform.forward : Vector3.forward;
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        if (enableClickToMove)
            HandleClickInput();

        UpdateGroundInfo();

        Vector3 desiredPlanarVelocity = ComputeDesiredPlanarVelocity();
        float accel = desiredPlanarVelocity.sqrMagnitude > planarVelocity.sqrMagnitude ? acceleration : deceleration;

        planarVelocity = Vector3.MoveTowards(
            planarVelocity,
            desiredPlanarVelocity,
            accel * dt
        );

        if (planarVelocity.sqrMagnitude > Tiny)
            desiredFacing = planarVelocity.normalized;

        UpdateRotation(dt);
        UpdateVerticalVelocity(dt);

        Vector3 motion = planarVelocity * dt + Vector3.up * (verticalVelocity * dt);
        motion = ResolveMotionWithSliding(transform.position, motion);

        controller.Move(motion);

        if (HasMoveTarget)
        {
            Vector3 flatToTarget = MoveTarget - transform.position;
            flatToTarget.y = 0f;

            if (flatToTarget.magnitude <= arrivalDistance)
            {
                HasMoveTarget = false;
                planarVelocity = Vector3.zero;
            }
        }
    }

    private void HandleClickInput()
    {
        if (mainCamera == null || Mouse.current == null)
            return;

        bool clicked =
            mouseButton == 0 ? Mouse.current.leftButton.wasPressedThisFrame :
            mouseButton == 1 ? Mouse.current.rightButton.wasPressedThisFrame :
            mouseButton == 2 ? Mouse.current.middleButton.wasPressedThisFrame :
            false;

        if (!clicked)
            return;

        Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (!Physics.Raycast(ray, out RaycastHit hit, maxClickDistance, groundLayers, QueryTriggerInteraction.Ignore))
            return;

        LastClickedPoint = hit.point;

        if (TryGetGroundPoint(hit.point, out Vector3 groundPoint, out _))
            SetMoveTarget(groundPoint);
    }

    private Vector3 ComputeDesiredPlanarVelocity()
    {
        if (!HasMoveTarget)
            return Vector3.zero;

        Vector3 current = transform.position;
        Vector3 toTarget = MoveTarget - current;
        toTarget.y = 0f;

        float distance = toTarget.magnitude;
        if (distance <= arrivalDistance)
            return Vector3.zero;

        Vector3 dir = toTarget.normalized;
        float speedFactor = Mathf.Clamp01(distance / slowDownDistance);
        float targetSpeed = moveSpeed * speedFactor;

        return dir * targetSpeed;
    }

    private void UpdateGroundInfo()
    {
        hasGround = TryGetGroundPoint(transform.position, out Vector3 groundPoint, out Vector3 groundNormal);
        lastGroundNormal = hasGround ? groundNormal : Vector3.up;

        if (drawDebug && hasGround)
        {
            Vector3 from = transform.position + Vector3.up * groundProbeStartHeight;
            Debug.DrawLine(from, groundPoint, groundProbeColor);
        }
    }

    private void UpdateVerticalVelocity(float dt)
    {
        if (hasGround)
        {
            float feetY = transform.position.y;
            float groundY = GetDesiredGroundedY(transform.position, lastGroundNormal);

            float deltaToGround = groundY - feetY;

            if (Mathf.Abs(deltaToGround) <= groundedSnapDistance)
            {
                verticalVelocity = deltaToGround / Mathf.Max(dt, 0.0001f);
                verticalVelocity = Mathf.Clamp(verticalVelocity, -groundStickVelocity, groundStickVelocity);
            }
            else
            {
                verticalVelocity = -groundStickVelocity;
            }

            planarVelocity = Vector3.ProjectOnPlane(planarVelocity, lastGroundNormal);
        }
        else
        {
            verticalVelocity += Physics.gravity.y * dt;
        }
    }

    private void UpdateRotation(float dt)
    {
        if (desiredFacing.sqrMagnitude < Tiny)
            return;

        Quaternion targetRot = Quaternion.LookRotation(desiredFacing, Vector3.up);
        float t = 1f - Mathf.Exp(-rotationSharpness * dt);

        if (visualRoot != null)
            visualRoot.rotation = Quaternion.Slerp(visualRoot.rotation, targetRot, t);
        else
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, t);
    }

    private Vector3 ResolveMotionWithSliding(Vector3 startPosition, Vector3 motion)
    {
        Vector3 remaining = motion;
        Vector3 position = startPosition;

        for (int i = 0; i < maxSlideIterations; i++)
        {
            float distance = remaining.magnitude;
            if (distance <= minRemainingDistance)
                break;

            Vector3 dir = remaining / distance;

            if (!CapsuleCast(position, dir, distance + shell, out RaycastHit hit))
            {
                position += remaining;
                break;
            }

            float moveDist = Mathf.Max(0f, hit.distance - shell);
            if (moveDist > 0f)
                position += dir * moveDist;

            Vector3 leftover = remaining - dir * moveDist;
            remaining = Vector3.ProjectOnPlane(leftover, hit.normal);

            if (drawDebug)
                Debug.DrawRay(hit.point, hit.normal, slideColor, 0f, false);
        }

        return position - startPosition;
    }

    private bool CapsuleCast(Vector3 centerPosition, Vector3 direction, float distance, out RaycastHit hit)
    {
        GetCapsulePoints(centerPosition, out Vector3 p1, out Vector3 p2, out float radius);

        return Physics.CapsuleCast(
            p1,
            p2,
            Mathf.Max(0.001f, radius - shell),
            direction,
            out hit,
            distance,
            obstacleLayers,
            QueryTriggerInteraction.Ignore
        );
    }

    private void GetCapsulePoints(Vector3 centerPosition, out Vector3 p1, out Vector3 p2, out float radius)
    {
        float scaleY = Mathf.Abs(transform.lossyScale.y);
        float scaleXZ = Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z));

        radius = controller.radius * scaleXZ;
        float height = Mathf.Max(controller.height * scaleY, radius * 2f);

        Vector3 worldCenter = centerPosition + transform.TransformVector(controller.center);
        float halfStraight = Mathf.Max(0f, (height * 0.5f) - radius);

        p1 = worldCenter + Vector3.up * halfStraight;
        p2 = worldCenter - Vector3.up * halfStraight;
    }

    private bool TryGetGroundPoint(Vector3 nearPosition, out Vector3 groundPoint, out Vector3 groundNormal)
    {
        float radius = Mathf.Max(0.05f, controller.radius * 0.9f);
        Vector3 origin = nearPosition + Vector3.up * groundProbeStartHeight;

        if (Physics.SphereCast(
            origin,
            radius,
            Vector3.down,
            out RaycastHit hit,
            groundProbeStartHeight + groundProbeDistance,
            groundLayers,
            QueryTriggerInteraction.Ignore))
        {
            float angle = Vector3.Angle(hit.normal, Vector3.up);
            if (angle <= maxGroundAngle)
            {
                groundPoint = hit.point;
                groundNormal = hit.normal;
                return true;
            }
        }

        groundPoint = default;
        groundNormal = Vector3.up;
        return false;
    }

    private float GetDesiredGroundedY(Vector3 currentPosition, Vector3 groundNormal)
    {
        if (!TryGetGroundPoint(currentPosition, out Vector3 groundPoint, out _))
            return currentPosition.y;

        float bottomOffset = (controller.height * 0.5f) - controller.radius;
        float footToCenter = Mathf.Max(controller.radius, bottomOffset + controller.radius);

        return groundPoint.y + footToCenter + groundOffset;
    }

    private void SnapToGroundImmediate()
    {
        if (TryGetGroundPoint(transform.position, out Vector3 groundPoint, out _))
        {
            float y = GetDesiredGroundedY(transform.position, Vector3.up);
            Vector3 pos = transform.position;
            pos.y = y;
            transform.position = pos;
        }
    }

    public void SetMoveTarget(Vector3 point)
    {
        MoveTarget = point;
        HasMoveTarget = true;

        Vector3 toTarget = point - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude > Tiny)
            desiredFacing = toTarget.normalized;
    }

    public void ClearMoveTarget()
    {
        HasMoveTarget = false;
        planarVelocity = Vector3.zero;
    }

    private void OnDrawGizmosSelected()
    {
        if (HasMoveTarget)
        {
            Gizmos.color = targetColor;
            Gizmos.DrawWireSphere(MoveTarget, 0.12f);
            Gizmos.DrawLine(transform.position, MoveTarget);
        }

        if (LastClickedPoint != Vector3.zero)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(LastClickedPoint, 0.08f);
        }
    }
}