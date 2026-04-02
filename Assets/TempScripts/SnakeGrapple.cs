using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class SnakeGrapple : MonoBehaviour
{
    [Header("References")]
    public SnakeController snakeController;
    public Camera gameCamera;

    [Header("Grapple Settings")]
    [Tooltip("Maximum distance the grapple can reach.")]
    public float maxDistance = 15f;

    [Tooltip("How fast the grabbed object is pulled toward the player.")]
    public float pullSpeed = 12f;

    [Tooltip("Distance at which the pull stops and the grapple releases.")]
    public float arrivalDistance = 1.5f;

    [Tooltip("Layers that can be grappled.")]
    public LayerMask GrappleableLayers = ~0;

    [Tooltip("Ground layers for mouse→world raycasting.")]
    public LayerMask GroundMask = ~0;

    [Header("Rope Extend Animation")]
    [Tooltip("How long the rope takes to reach the target (seconds).")]
    public float ropeExtendDuration = 0.15f;

    [Tooltip("Arc height of the rope while extending.")]
    public float ropeArcHeight = 0.3f;

    [Header("Rope Visuals")]
    public Material GrappleMaterial;
    public Color ropeColor = new Color(0.95f, 0.3f, 0.2f, 1f);
    public float ropeStartWidth = 0.15f;
    public float ropeEndWidth = 0.06f;
    public int ropeSegments = 12;

    [Header("Rope Wobble")]
    [Tooltip("Lateral wobble amplitude while the rope is alive.")]
    public float wobbleAmplitude = 0.12f;
    public float wobbleFrequency = 6f;
    [Tooltip("How fast wobble decays after the rope finishes extending.")]
    public float wobbleDamping = 4f;

    private LineRenderer _line;
    private bool _isGrappling = false;
    private bool _ropeFullyExtended = false;
    private Rigidbody _grabbedRb;
    private Vector3 _grapplePoint;       // world hit point (for non-rb hits)
    private Vector3 _animatedRopeEnd;    // tip of rope during extend animation
    private float _wobbleFade = 1f;
    private float _grappleStartTime;

    public bool IsGrappling => _isGrappling;

    private Transform HeadTransform => snakeController != null && snakeController.Head != null
        ? snakeController.Head.transform
        : null;

    void Awake()
    {
        // Build LineRenderer
        _line = gameObject.AddComponent<LineRenderer>();
        _line.useWorldSpace = true;
        _line.positionCount = 0;
        _line.startWidth = ropeStartWidth;
        _line.endWidth = ropeEndWidth;
        _line.enabled = false;

        if (GrappleMaterial == null)
        {
            GrappleMaterial = new Material(Shader.Find("Sprites/Default"));
            GrappleMaterial.color = ropeColor;
        }
        _line.material = GrappleMaterial;
        _line.startColor = ropeColor;
        _line.endColor = ropeColor;
    }

    void Start()
    {
        if (gameCamera == null)
            gameCamera = Camera.main;

        if (snakeController == null)
            snakeController = GetComponent<SnakeController>();
    }

    void Update()
    {
        // Press 1 to fire grapple
        if (Keyboard.current != null && Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            if (!_isGrappling)
                TryStartGrapple();
            else
                StopGrapple();
        }

        // While grappling, pull the object
        if (_isGrappling && _ropeFullyExtended && _grabbedRb != null)
        {
            PullGrabbedObject();
        }

        // Draw rope every frame
        if (_isGrappling)
        {
            DrawRope();
        }
    }

    void TryStartGrapple()
    {
        if (HeadTransform == null || gameCamera == null) return;

        // Raycast from mouse position onto the ground to get a world target
        Vector3 mouseWorld = GetMouseWorldPosition();
        if (mouseWorld == Vector3.zero) return; // no ground hit

        Vector3 headPos = HeadTransform.position;
        Vector3 direction = (mouseWorld - headPos);
        direction.y = 0f; // keep it flat for ortho
        float distance = Mathf.Min(direction.magnitude, maxDistance);

        if (distance < 0.1f) return;

        direction = direction.normalized;

        // SphereCast from the head toward the mouse to find a grappable object
        RaycastHit hit;
        if (Physics.SphereCast(headPos, 0.3f, direction, out hit, distance, GrappleableLayers))
        {
            _grapplePoint = hit.point;

            if (hit.rigidbody != null)
            {
                _grabbedRb = hit.rigidbody;
            }
            else
            {
                // Hit something static — no pull, just snap the rope to it briefly
                _grabbedRb = null;
            }

            _isGrappling = true;
            _ropeFullyExtended = false;
            _wobbleFade = 1f;
            _grappleStartTime = Time.time;
            _animatedRopeEnd = headPos;

            _line.enabled = true;

            StartCoroutine(AnimateRopeExtend(_grapplePoint));
        }
    }

    void StopGrapple()
    {
        _isGrappling = false;
        _ropeFullyExtended = false;
        _grabbedRb = null;
        _line.positionCount = 0;
        _line.enabled = false;
        StopAllCoroutines();
    }

    void PullGrabbedObject()
    {
        if (HeadTransform == null || _grabbedRb == null)
        {
            StopGrapple();
            return;
        }

        Vector3 headPos = HeadTransform.position;
        Vector3 objPos = _grabbedRb.position;
        Vector3 toPlayer = (headPos - objPos);
        toPlayer.y = 0f;
        float dist = toPlayer.magnitude;

        if (dist <= arrivalDistance)
        {
            // Arrived — release
            _grabbedRb.linearVelocity = Vector3.zero;
            StopGrapple();
            return;
        }

        // Apply force to pull the object toward the player
        Vector3 pullDir = toPlayer.normalized;
        _grabbedRb.AddForce(pullDir * pullSpeed, ForceMode.Acceleration);
    }

    Vector3 GetMouseWorldPosition()
    {
        if (Mouse.current == null) return Vector3.zero;

        Vector2 mouseScreen = Mouse.current.position.ReadValue();
        Ray ray = gameCamera.ScreenPointToRay(new Vector3(mouseScreen.x, mouseScreen.y, 0f));

        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, 200f, GroundMask))
            return hit.point;

        // Fallback: project onto y=0 plane
        if (Mathf.Abs(ray.direction.y) > 0.001f)
        {
            float t = -ray.origin.y / ray.direction.y;
            if (t > 0f)
                return ray.origin + ray.direction * t;
        }

        return Vector3.zero;
    }

    IEnumerator AnimateRopeExtend(Vector3 target)
    {
        Vector3 start = HeadTransform.position;
        float elapsed = 0f;

        while (elapsed < ropeExtendDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / ropeExtendDuration);

            // Lerp with a slight arc
            Vector3 basePos = Vector3.Lerp(start, target, t);
            float arc = Mathf.Sin(Mathf.PI * t) * ropeArcHeight;
            _animatedRopeEnd = basePos + Vector3.up * arc;

            yield return null;
        }

        _animatedRopeEnd = target;
        _ropeFullyExtended = true;

        // If we didn't hit a rigidbody, auto-release after the rope reaches
        if (_grabbedRb == null)
        {
            yield return new WaitForSeconds(0.2f);
            StopGrapple();
        }
    }
    void DrawRope()
    {
        if (HeadTransform == null) return;

        Vector3 start = HeadTransform.position;
        Vector3 end;

        if (_ropeFullyExtended && _grabbedRb != null)
            end = _grabbedRb.position;
        else if (_ropeFullyExtended)
            end = _grapplePoint;
        else
            end = _animatedRopeEnd;

        int segments = ropeSegments;
        _line.positionCount = segments + 1;

        // Decay wobble over time
        float timeSinceStart = Time.time - _grappleStartTime;
        float wobble = wobbleAmplitude * Mathf.Exp(-wobbleDamping * Mathf.Max(0f, timeSinceStart - ropeExtendDuration));

        // Get a perpendicular axis for the wobble (flat on ground plane)
        Vector3 forward = (end - start);
        forward.y = 0f;
        Vector3 perp = Vector3.Cross(forward.normalized, Vector3.up);

        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            Vector3 pos = Vector3.Lerp(start, end, t);

            // Sine wobble — peaks in the middle, zero at endpoints
            float envelope = Mathf.Sin(Mathf.PI * t);
            float osc = Mathf.Sin(wobbleFrequency * t * Mathf.PI * 2f + Time.time * 8f) * wobble * envelope;
            pos += perp * osc;

            _line.SetPosition(i, pos);
        }
    }
}
