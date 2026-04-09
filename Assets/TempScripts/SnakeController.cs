using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class SnakeController : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject HeadPrefab;
    public GameObject SegmentPrefab;
    public GameObject ClickIndicatorPrefab;

    [Header("Snake Shape")]
    [Range(1, 64)]
    public int SegmentCount = 12;

    [Tooltip("Base arc-length distance between segments at rest. Min 0.02.")]
    [Range(0.02f, 5f)]
    public float SegmentSpacing = 0.55f;

    [Header("Movement")]
    [Range(0.5f, 30f)]
    public float MoveSpeed = 6f;

    [Range(1f, 40f)]
    public float TurnSpeed = 10f;

    [Range(0.01f, 2f)]
    public float ArrivalThreshold = 0.15f;

    [Tooltip("Velocity smoothing — higher = more sluggish/inertia.")]
    [Range(0f, 0.98f)]
    public float MoveSmoothing = 0.18f;

    [Header("Trail Resolution")]
    [Range(200, 2000)]
    public int TrailResolution = 800;

    [Range(0.001f, 0.5f)]
    public float TrailMinDistance = 0.02f;

    [Header("Slinky Spacing Stretch")]
    [Tooltip("At max speed each segment samples this multiple of SegmentSpacing further along " +
             "the trail — making the body appear stretched/spaced out. Object scale never changes.")]
    [Range(1f, 5f)]
    public float MaxSpacingStretch = 2.5f;

    [Tooltip("When nearly stopped the tail catches up — segments sample closer together.")]
    [Range(0.1f, 1f)]
    public float MinSpacingStretch = 0.5f;

    [Tooltip("How fast each segment's effective spacing lerps toward its speed-driven target.")]
    [Range(1f, 30f)]
    public float SpacingLerpSpeed = 8f;

    [Header("Anti-Clip Separation")]
    [Tooltip("Minimum world-space distance enforced between adjacent segment centres. " +
             "Set to roughly your segment's visible radius. 0 = disabled.")]
    [Range(0f, 2f)]
    public float MinSeparation = 0.3f;

    [Range(1, 4)]
    public int SeparationIterations = 1;

    [Tooltip("How far segments roll/bank when pushed sideways by the separation solver.")]
    [Range(0f, 90f)]
    public float SeparationRollStrength = 35f;

    [Tooltip("Speed at which roll eases back to zero.")]
    [Range(1f, 20f)]
    public float RollDecaySpeed = 8f;

    [Header("Input Modes")]
    [Tooltip("Single left-click moves the snake to that point.")]
    public bool CanClickMove = true;

    [Tooltip("Hold and drag to draw a path the snake will follow after release.")]
    public bool CanDrawPath = true;

    [Header("Draw Path — Line Renderer")]
    [Tooltip("Visual width of the drawn path line (world units).")]
    [Range(0.01f, 1f)]
    public float PathLineWidth = 0.08f;

    public Color PathLineColor = new Color(1f, 0.85f, 0.2f, 0.85f);

    [Tooltip("How far the mouse must move on the ground before a new path point is recorded.")]
    [Range(0.05f, 2f)]
    public float PathDrawMinDistance = 0.2f;

    [Tooltip("Number of subdivisions used to smooth the path via Catmull-Rom spline.")]
    [Range(2, 20)]
    public int PathSmoothing = 8;

    [Tooltip("Y offset applied to the drawn line so it floats just above the ground.")]
    public float PathLineYOffset = 0.05f;

    [Header("Ground & Camera")]
    public LayerMask GroundMask = ~0;
    public Camera ClickCamera;

    [Tooltip("Assign the OrthoCameraFollow component here and it will automatically " +
             "track the snake head. Leave null if you want to wire it up yourself.")]
    public OrthoCameraFollow CameraFollow;

    [Tooltip("Extra Y padding above the auto-detected collider half-height. Usually 0.")]
    public float GroundOffsetPadding = 0f;

    [Range(0.1f, 5f)]
    public float ClickIndicatorLifetime = 1.0f;

    [Header("Ability — Ball Throw  (1)")]
    public GameObject BallPrefab;
    public float BallArcHeight = 5f;

    [SerializeField] private bool _hasBalls = false;
    public bool HasBalls
    {
        get => _hasBalls;
        set
        {
            if (_hasBalls == value) return;
            _hasBalls = value;
            if (_hasBalls) PowerUpEvents.RaisePowerUpCollected(PowerUpType.Balls);
            else PowerUpEvents.RaisePowerUpLost(PowerUpType.Balls);
        }
    }

    [Header("Ability — Grapple  (2)")]
    [Tooltip("Maximum distance the grapple can reach.")]
    public float GrappleMaxDistance = 15f;

    [Tooltip("How fast the grabbed object is pulled toward the player.")]
    public float GrapplePullSpeed = 12f;

    [Tooltip("Distance at which the pull stops and the grapple releases.")]
    public float GrappleArrivalDistance = 1.5f;

    [Tooltip("Layers that can be grappled.")]
    public LayerMask GrappleableLayers = ~0;

    [Header("Grapple — Rope Extend Animation")]
    public float RopeExtendDuration = 0.15f;
    public float RopeArcHeight = 0.3f;

    [Header("Grapple — Rope Visuals")]
    public Material GrappleMaterial;
    public Color RopeColor = new Color(0.95f, 0.3f, 0.2f, 1f);
    public float RopeStartWidth = 0.15f;
    public float RopeEndWidth = 0.06f;
    public int RopeSegments = 12;

    [Header("Grapple — Rope Wobble")]
    public float WobbleAmplitude = 0.12f;
    public float WobbleFrequency = 6f;
    public float WobbleDamping = 4f;

    [SerializeField] private bool _hasGrapple = false;
    public bool HasGrapple
    {
        get => _hasGrapple;
        set
        {
            if (_hasGrapple == value) return;
            _hasGrapple = value;
            if (_hasGrapple) PowerUpEvents.RaisePowerUpCollected(PowerUpType.Grapple);
            else PowerUpEvents.RaisePowerUpLost(PowerUpType.Grapple);
            if (!_hasGrapple && _isGrappling) StopGrapple();
        }
    }

    [Header("Ability — Glide  (3)")]
    [SerializeField] private bool _hasGlide = false;
    public bool HasGlide
    {
        get => _hasGlide;
        set
        {
            if (_hasGlide == value) return;
            _hasGlide = value;
            if (_hasGlide) PowerUpEvents.RaisePowerUpCollected(PowerUpType.Glide);
            else PowerUpEvents.RaisePowerUpLost(PowerUpType.Glide);
        }
    }

    private GameObject _head;
    private List<GameObject> _segments = new List<GameObject>();

    private float[] _segEffSpacing;
    private float[] _segCurrentRoll;
    private Vector3[] _solvedPos;

    private Vector3[] _trail;
    private int _trailWriteIdx = 0;
    private int _trailCount = 0;
    private Vector3 _lastRecordedPos;

    private Vector3 _targetPosition;
    private bool _hasTarget = false;
    private Vector3 _smoothVelocity = Vector3.zero;
    private float _currentSpeed = 0f;
    private Vector3 _prevHeadPos;

    private float _headHalfHeight;
    private float _segHalfHeight;

    private List<Vector3> _drawnRaw = new List<Vector3>();
    private List<Vector3> _drawnPath = new List<Vector3>();
    private int _pathIndex = 0;
    private bool _followingPath = false;
    private bool _isDragging = false;
    private Vector3 _lastDrawnPos;

    private LineRenderer _pathLine;
    private GameObject _pathLineGO;

    private LineRenderer _grappleLine;
    private bool _isGrappling = false;
    private bool _ropeFullyExtended = false;
    private Rigidbody _grabbedRb;
    private Vector3 _grapplePoint;
    private Vector3 _animatedRopeEnd;
    private float _wobbleFade = 1f;
    private float _grappleStartTime;

    public bool IsGrappling => _isGrappling;

    public float CurrentSpeed => _currentSpeed;
    public GameObject Head => _head;
    public IReadOnlyList<GameObject> Segments => _segments;

    private Transform HeadTransform => _head != null ? _head.transform : null;

    void Start()
    {
        if (ClickCamera == null) ClickCamera = Camera.main;
        BuildSnake();
        SeedTrail(_head.transform.position);
        BuildPathLine();
        BuildGrappleLine();

        if (CameraFollow != null)
            CameraFollow.SetTarget(_head.transform, snapInstantly: true);
    }

    void Update()
    {
        HandleInput();
        MoveHead();
        UpdateHeadSpeed();
        RecordTrail();
        UpdateSegments();

        HandleBallThrowInput();
        HandleGrappleInput();
        HandleGlideInput();

        if (_isGrappling && _ropeFullyExtended && _grabbedRb != null)
            PullGrabbedObject();

        if (_isGrappling)
            DrawGrappleRope();
    }

    void BuildSnake()
    {
        _head = HeadPrefab != null
            ? Instantiate(HeadPrefab, transform.position, Quaternion.identity)
            : MakeSphere(transform.position, new Color(0.95f, 0.3f, 0.2f), 0.55f);

        _head.name = "SnakeHead";
        _head.tag = "Player";
        _head.transform.SetParent(transform);
        _headHalfHeight = GetColliderHalfHeight(_head);
        _prevHeadPos = _head.transform.position;
        _targetPosition = _head.transform.position;

        _segEffSpacing = new float[SegmentCount];
        _segCurrentRoll = new float[SegmentCount];
        _solvedPos = new Vector3[SegmentCount + 1];

        for (int i = 0; i < SegmentCount; i++)
        {
            Vector3 pos = transform.position - transform.forward * SegmentSpacing * (i + 1);

            GameObject seg = SegmentPrefab != null
                ? Instantiate(SegmentPrefab, pos, Quaternion.identity)
                : MakeSphere(pos, Color.Lerp(
                      new Color(0.2f, 0.85f, 0.45f),
                      new Color(0.15f, 0.45f, 0.9f),
                      (float)i / Mathf.Max(1, SegmentCount - 1)), 0.45f);

            seg.name = $"Segment_{i:D2}";
            seg.tag = "Player";
            seg.transform.SetParent(transform);
            _segments.Add(seg);

            _segEffSpacing[i] = SegmentSpacing;
            _segCurrentRoll[i] = 0f;
        }

        _segHalfHeight = _segments.Count > 0 ? GetColliderHalfHeight(_segments[0]) : 0.225f;
    }

    void BuildPathLine()
    {
        _pathLineGO = new GameObject("DrawPathLine");
        _pathLineGO.transform.SetParent(transform);

        _pathLine = _pathLineGO.AddComponent<LineRenderer>();
        _pathLine.useWorldSpace = true;
        _pathLine.positionCount = 0;
        _pathLine.startWidth = PathLineWidth;
        _pathLine.endWidth = PathLineWidth;
        _pathLine.numCornerVertices = 8;
        _pathLine.numCapVertices = 8;

        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = PathLineColor;
        _pathLine.material = mat;
        _pathLine.startColor = PathLineColor;
        _pathLine.endColor = new Color(PathLineColor.r, PathLineColor.g,
                                         PathLineColor.b, PathLineColor.a * 0.4f);
        _pathLineGO.SetActive(false);
    }

    void BuildGrappleLine()
    {
        var go = new GameObject("GrappleLine");
        go.transform.SetParent(transform);

        _grappleLine = go.AddComponent<LineRenderer>();
        _grappleLine.useWorldSpace = true;
        _grappleLine.positionCount = 0;
        _grappleLine.startWidth = RopeStartWidth;
        _grappleLine.endWidth = RopeEndWidth;
        _grappleLine.enabled = false;

        if (GrappleMaterial == null)
        {
            GrappleMaterial = new Material(Shader.Find("Sprites/Default"));
            GrappleMaterial.color = RopeColor;
        }
        _grappleLine.material = GrappleMaterial;
        _grappleLine.startColor = RopeColor;
        _grappleLine.endColor = RopeColor;
    }
    float GetColliderHalfHeight(GameObject go)
    {
        Collider col = go.GetComponent<Collider>();
        if (col == null) return go.transform.lossyScale.y * 0.5f;
        return Mathf.Max(go.transform.position.y - col.bounds.min.y, 0f);
    }

    GameObject MakeSphere(Vector3 pos, Color col, float radius)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * radius;
        var c = go.GetComponent<Collider>();
        if (c) Destroy(c);
        var mr = go.GetComponent<MeshRenderer>();
        if (mr)
        {
            var mat = new Material(Shader.Find("Standard"));
            mat.color = col;
            mr.material = mat;
        }
        return go;
    }

    Vector3 SnapToGround(Vector3 pos, float halfHeight)
    {
        Vector3 origin = new Vector3(pos.x, pos.y + 10f, pos.z);
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 30f, GroundMask))
            return new Vector3(pos.x, hit.point.y + halfHeight + GroundOffsetPadding, pos.z);
        return pos;
    }

    Vector3 GetMouseWorldPosition()
    {
        if (Mouse.current == null) return Vector3.zero;

        Vector2 mouseScreen = Mouse.current.position.ReadValue();
        Ray ray = ClickCamera.ScreenPointToRay(new Vector3(mouseScreen.x, mouseScreen.y, 0f));

        if (Physics.Raycast(ray, out RaycastHit hit, 200f, GroundMask))
            return hit.point;

        if (Mathf.Abs(ray.direction.y) > 0.001f)
        {
            float t = -ray.origin.y / ray.direction.y;
            if (t > 0f) return ray.origin + ray.direction * t;
        }

        return Vector3.zero;
    }

    void SeedTrail(Vector3 pos)
    {
        _trail = new Vector3[TrailResolution];
        for (int i = 0; i < TrailResolution; i++) _trail[i] = pos;
        _trailWriteIdx = TrailResolution - 1;
        _trailCount = TrailResolution;
        _lastRecordedPos = pos;
    }

    void RecordTrail()
    {
        Vector3 pos = _head.transform.position;
        if (Vector3.Distance(pos, _lastRecordedPos) < TrailMinDistance) return;
        _trailWriteIdx = (_trailWriteIdx + 1) % TrailResolution;
        _trail[_trailWriteIdx] = pos;
        _lastRecordedPos = pos;
        if (_trailCount < TrailResolution) _trailCount++;
    }

    Vector3 SampleTrail(float targetDist)
    {
        if (_trailCount <= 1) return _head.transform.position;

        float accumulated = 0f;
        int idxA = _trailWriteIdx;
        Vector3 posA = _trail[idxA];

        for (int step = 1; step < _trailCount; step++)
        {
            int idxB = (_trailWriteIdx - step + TrailResolution) % TrailResolution;
            Vector3 posB = _trail[idxB];
            float segLen = Vector3.Distance(posA, posB);
            accumulated += segLen;

            if (accumulated >= targetDist)
            {
                float overshoot = accumulated - targetDist;
                float t = segLen > 0.0001f ? overshoot / segLen : 0f;
                return Vector3.Lerp(posA, posB, t);
            }

            posA = posB;
            idxA = idxB;
        }

        return _trail[(_trailWriteIdx - _trailCount + 1 + TrailResolution) % TrailResolution];
    }

    float GetCumulativeDist(int segIndex)
    {
        float d = 0f;
        for (int j = 0; j <= segIndex; j++) d += _segEffSpacing[j];
        return d;
    }

    void HandleInput()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        bool pressed = mouse.leftButton.wasPressedThisFrame;
        bool held = mouse.leftButton.isPressed;
        bool released = mouse.leftButton.wasReleasedThisFrame;

        Vector2 screenPos = mouse.position.ReadValue();
        Ray ray = ClickCamera.ScreenPointToRay(new Vector3(screenPos.x, screenPos.y, 0f));
        bool groundHit = Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, GroundMask);

        if (CanDrawPath)
        {
            if (pressed && groundHit)
            {
                _isDragging = true;
                _followingPath = false;
                _hasTarget = false;

                _drawnRaw.Clear();
                _drawnPath.Clear();

                Vector3 startPt = hit.point;
                startPt.y += PathLineYOffset;
                _drawnRaw.Add(startPt);
                _lastDrawnPos = startPt;

                _pathLineGO.SetActive(true);
                RefreshLineRenderer(_drawnRaw);
            }

            if (_isDragging && held && groundHit)
            {
                Vector3 pt = hit.point;
                pt.y += PathLineYOffset;

                if (Vector3.Distance(pt, _lastDrawnPos) >= PathDrawMinDistance)
                {
                    _drawnRaw.Add(pt);
                    _lastDrawnPos = pt;
                    RefreshLineRenderer(_drawnRaw);
                }
            }

            if (_isDragging && released)
            {
                _isDragging = false;

                if (_drawnRaw.Count >= 2)
                {
                    _drawnPath = CatmullRomSpline(_drawnRaw, PathSmoothing);

                    for (int i = 0; i < _drawnPath.Count; i++)
                    {
                        Vector3 snapped = SnapToGround(_drawnPath[i], _headHalfHeight);
                        snapped.y = Mathf.Max(snapped.y,
                                              _drawnPath[i].y - PathLineYOffset + _headHalfHeight);
                        _drawnPath[i] = snapped;
                    }

                    List<Vector3> displayPts = new List<Vector3>(_drawnPath.Count);
                    foreach (var p in _drawnPath)
                        displayPts.Add(new Vector3(p.x, p.y - _headHalfHeight + PathLineYOffset, p.z));
                    RefreshLineRenderer(displayPts);

                    _pathIndex = 0;
                    _followingPath = true;
                }
                else
                {
                    _pathLineGO.SetActive(false);
                    if (CanClickMove && groundHit)
                        SetClickTarget(hit.point);
                }
            }

            if (_isDragging) return;
        }

        if (CanClickMove && !CanDrawPath && pressed && groundHit)
        {
            SetClickTarget(hit.point);
        }
    }

    void SetClickTarget(Vector3 groundPoint)
    {
        _followingPath = false;
        _pathLineGO.SetActive(false);
        _targetPosition = groundPoint + Vector3.up * (_headHalfHeight + GroundOffsetPadding);
        _hasTarget = true;

        if (ClickIndicatorPrefab != null)
            Destroy(Instantiate(ClickIndicatorPrefab, groundPoint, Quaternion.identity),
                    ClickIndicatorLifetime);
    }

    void MoveHead()
    {
        if (_followingPath && _drawnPath.Count > 0)
        {
            while (_pathIndex < _drawnPath.Count - 1)
            {
                float dist = Vector3.Distance(_head.transform.position, _drawnPath[_pathIndex]);
                if (dist < ArrivalThreshold) _pathIndex++;
                else break;
            }

            if (_pathIndex >= _drawnPath.Count)
            {
                _followingPath = false;
                _pathLineGO.SetActive(false);
                return;
            }

            _targetPosition = _drawnPath[_pathIndex];
            _hasTarget = true;
            UpdatePathLineFade();
        }

        if (!_hasTarget)
        {
            _smoothVelocity = Vector3.Lerp(_smoothVelocity, Vector3.zero, Time.deltaTime * 8f);
            if (_smoothVelocity.sqrMagnitude > 0.0001f)
                _head.transform.position = SnapToGround(
                    _head.transform.position + _smoothVelocity * Time.deltaTime, _headHalfHeight);
            return;
        }

        Vector3 pos = _head.transform.position;
        Vector3 diff = _targetPosition - pos;
        diff.y = 0f;

        if (diff.magnitude <= ArrivalThreshold)
        {
            if (!_followingPath) _hasTarget = false;
            return;
        }

        Vector3 desired = diff.normalized * MoveSpeed;
        float lerpSpeed = Mathf.Lerp(20f, 2f, MoveSmoothing);
        _smoothVelocity = Vector3.Lerp(_smoothVelocity, desired, Time.deltaTime * lerpSpeed);

        _head.transform.position = SnapToGround(pos + _smoothVelocity * Time.deltaTime, _headHalfHeight);

        Vector3 faceDir = _smoothVelocity; faceDir.y = 0f;
        if (faceDir.sqrMagnitude > 0.01f)
            _head.transform.rotation = Quaternion.Slerp(_head.transform.rotation,
                Quaternion.LookRotation(faceDir.normalized), Time.deltaTime * TurnSpeed);
    }

    void UpdateHeadSpeed()
    {
        _currentSpeed = (_head.transform.position - _prevHeadPos).magnitude / Time.deltaTime;
        _prevHeadPos = _head.transform.position;
    }

    void RefreshLineRenderer(List<Vector3> points)
    {
        _pathLine.startWidth = PathLineWidth;
        _pathLine.endWidth = PathLineWidth;
        _pathLine.startColor = PathLineColor;
        _pathLine.endColor = new Color(PathLineColor.r, PathLineColor.g,
                                         PathLineColor.b, PathLineColor.a * 0.4f);

        _pathLine.positionCount = points.Count;
        for (int i = 0; i < points.Count; i++)
            _pathLine.SetPosition(i, points[i]);
    }

    void UpdatePathLineFade()
    {
        if (_pathIndex <= 0 || _pathIndex >= _drawnPath.Count) return;

        int remaining = _drawnPath.Count - _pathIndex;
        if (remaining < 2) { _pathLineGO.SetActive(false); return; }

        _pathLine.positionCount = remaining + 1;
        _pathLine.SetPosition(0, new Vector3(
            _head.transform.position.x,
            _head.transform.position.y - _headHalfHeight + PathLineYOffset,
            _head.transform.position.z));

        for (int i = 0; i < remaining; i++)
        {
            Vector3 p = _drawnPath[_pathIndex + i];
            _pathLine.SetPosition(i + 1,
                new Vector3(p.x, p.y - _headHalfHeight + PathLineYOffset, p.z));
        }
    }

    List<Vector3> CatmullRomSpline(List<Vector3> pts, int subdivisions)
    {
        var result = new List<Vector3>();
        if (pts.Count < 2) return result;

        var padded = new List<Vector3>();
        padded.Add(pts[0] + (pts[0] - pts[1]));
        padded.AddRange(pts);
        padded.Add(pts[pts.Count - 1] + (pts[pts.Count - 1] - pts[pts.Count - 2]));

        for (int i = 1; i < padded.Count - 2; i++)
        {
            Vector3 p0 = padded[i - 1];
            Vector3 p1 = padded[i];
            Vector3 p2 = padded[i + 1];
            Vector3 p3 = padded[i + 2];

            for (int s = 0; s < subdivisions; s++)
            {
                float t = (float)s / subdivisions;
                float t2 = t * t;
                float t3 = t2 * t;

                Vector3 point = 0.5f * (
                    (2f * p1) +
                    (-p0 + p2) * t +
                    (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                    (-p0 + 3f * p1 - 3f * p2 + p3) * t3
                );
                result.Add(point);
            }
        }
        result.Add(padded[padded.Count - 2]);
        return result;
    }

    void UpdateSegments()
    {
        float speedRatio = Mathf.Clamp01(_currentSpeed / Mathf.Max(MoveSpeed, 0.001f));
        float targetStretchMult = Mathf.Lerp(MinSpacingStretch, MaxSpacingStretch, speedRatio);

        for (int i = 0; i < SegmentCount; i++)
        {
            float lagFactor = 1f - (float)i / (SegmentCount + 1) * 0.6f;
            _segEffSpacing[i] = Mathf.Max(
                Mathf.Lerp(_segEffSpacing[i],
                           SegmentSpacing * targetStretchMult,
                           Time.deltaTime * SpacingLerpSpeed * lagFactor),
                0.02f);
        }

        _solvedPos[0] = _head.transform.position;

        float cumulativeDist = 0f;
        for (int i = 0; i < SegmentCount; i++)
        {
            cumulativeDist += _segEffSpacing[i];
            Vector3 raw = SampleTrail(cumulativeDist);
            _solvedPos[i + 1] = SnapToGround(raw, _segHalfHeight);
        }

        if (MinSeparation > 0f)
        {
            for (int iter = 0; iter < SeparationIterations; iter++)
            {
                for (int i = SegmentCount; i >= 1; i--)
                {
                    Vector3 diff = _solvedPos[i] - _solvedPos[i - 1];
                    float dist = diff.magnitude;
                    if (dist < MinSeparation && dist > 0.0001f)
                    {
                        Vector3 push = diff.normalized * (MinSeparation - dist);
                        push.y = 0f;
                        _solvedPos[i] += push;
                        _solvedPos[i] = SnapToGround(_solvedPos[i], _segHalfHeight);
                    }
                }
            }
        }

        for (int i = 0; i < SegmentCount; i++)
        {
            Vector3 finalPos = _solvedPos[i + 1];
            Vector3 aheadPos = _solvedPos[i];

            _segments[i].transform.position = finalPos;

            Vector3 toAhead = aheadPos - finalPos;
            toAhead.y = 0f;
            Quaternion baseRot = toAhead.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(toAhead.normalized)
                : _segments[i].transform.rotation;

            float targetRoll = 0f;
            if (MinSeparation > 0f && SeparationRollStrength > 0f)
            {
                Vector3 rawPos = SampleTrail(GetCumulativeDist(i));
                Vector3 nudge = new Vector3(finalPos.x - rawPos.x, 0f, finalPos.z - rawPos.z);
                if (nudge.sqrMagnitude > 0.0001f)
                {
                    Vector3 right = baseRot * Vector3.right;
                    float sideways = Vector3.Dot(nudge.normalized, right);
                    targetRoll = sideways * SeparationRollStrength
                                 * Mathf.Clamp01(nudge.magnitude / Mathf.Max(MinSeparation, 0.001f));
                }
            }

            float rollLerp = targetRoll != 0f ? SpacingLerpSpeed : RollDecaySpeed;
            _segCurrentRoll[i] = Mathf.Lerp(_segCurrentRoll[i], targetRoll,
                                             Time.deltaTime * rollLerp);

            _segments[i].transform.rotation =
                baseRot * Quaternion.AngleAxis(_segCurrentRoll[i], Vector3.forward);
        }
    }

    void HandleBallThrowInput()
    {
        if (!_hasBalls) return;
        if (BallPrefab == null) return;

        if (Keyboard.current != null && Keyboard.current.digit1Key.wasPressedThisFrame)
            ThrowBall();
    }

    void ThrowBall()
    {
        if (HeadTransform == null) return;

        Ray ray = ClickCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        Vector3 targetPoint;

        if (Physics.Raycast(ray, out RaycastHit hit))
            targetPoint = hit.point;
        else
            targetPoint = ray.GetPoint(30f);

        Vector3 spawnPos = HeadTransform.position;
        GameObject ball = Instantiate(BallPrefab, spawnPos, Quaternion.identity);
        Rigidbody rb = ball.GetComponent<Rigidbody>();

        if (rb != null)
            rb.linearVelocity = CalculateBallArcVelocity(spawnPos, targetPoint, BallArcHeight);
    }

    Vector3 CalculateBallArcVelocity(Vector3 start, Vector3 end, float height)
    {
        float gravity = Physics.gravity.y;
        Vector3 displacement = end - start;
        Vector3 displacementXZ = new Vector3(displacement.x, 0, displacement.z);

        float timeUp = Mathf.Sqrt(-2 * height / gravity);
        float timeDown = Mathf.Sqrt(2 * (displacement.y - height) / gravity);
        float totalTime = timeUp + timeDown;

        Vector3 velocityY = Vector3.up * Mathf.Sqrt(-2 * gravity * height);
        Vector3 velocityXZ = displacementXZ / totalTime;
        return velocityXZ + velocityY;
    }

    void HandleGrappleInput()
    {
        if (!_hasGrapple) return;

        if (Keyboard.current != null && Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            if (!_isGrappling)
                TryStartGrapple();
            else
                StopGrapple();
        }
    }

    void TryStartGrapple()
    {
        if (HeadTransform == null || ClickCamera == null) return;

        Vector3 mouseWorld = GetMouseWorldPosition();
        if (mouseWorld == Vector3.zero) return;

        Vector3 headPos = HeadTransform.position;
        Vector3 direction = mouseWorld - headPos;
        direction.y = 0f;
        float distance = Mathf.Min(direction.magnitude, GrappleMaxDistance);

        if (distance < 0.1f) return;
        direction = direction.normalized;

        if (Physics.SphereCast(headPos, 0.3f, direction, out RaycastHit hit, distance, GrappleableLayers))
        {
            _grapplePoint = hit.point;
            _grabbedRb = hit.rigidbody; // null if static

            _isGrappling = true;
            _ropeFullyExtended = false;
            _wobbleFade = 1f;
            _grappleStartTime = Time.time;
            _animatedRopeEnd = headPos;

            _grappleLine.enabled = true;
            StartCoroutine(AnimateRopeExtend(_grapplePoint));
        }
    }

    void StopGrapple()
    {
        _isGrappling = false;
        _ropeFullyExtended = false;
        _grabbedRb = null;
        _grappleLine.positionCount = 0;
        _grappleLine.enabled = false;
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
        Vector3 toPlayer = headPos - _grabbedRb.position;
        toPlayer.y = 0f;
        float dist = toPlayer.magnitude;

        if (dist <= GrappleArrivalDistance)
        {
            _grabbedRb.linearVelocity = Vector3.zero;
            StopGrapple();
            return;
        }

        _grabbedRb.AddForce(toPlayer.normalized * GrapplePullSpeed, ForceMode.Acceleration);
    }

    IEnumerator AnimateRopeExtend(Vector3 target)
    {
        Vector3 start = HeadTransform.position;
        float elapsed = 0f;

        while (elapsed < RopeExtendDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / RopeExtendDuration);

            Vector3 basePos = Vector3.Lerp(start, target, t);
            float arc = Mathf.Sin(Mathf.PI * t) * RopeArcHeight;
            _animatedRopeEnd = basePos + Vector3.up * arc;

            yield return null;
        }

        _animatedRopeEnd = target;
        _ropeFullyExtended = true;

        if (_grabbedRb == null)
        {
            yield return new WaitForSeconds(0.2f);
            StopGrapple();
        }
    }

    void DrawGrappleRope()
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

        int segments = RopeSegments;
        _grappleLine.positionCount = segments + 1;

        float timeSinceStart = Time.time - _grappleStartTime;
        float wobble = WobbleAmplitude * Mathf.Exp(-WobbleDamping * Mathf.Max(0f, timeSinceStart - RopeExtendDuration));

        Vector3 forward = end - start;
        forward.y = 0f;
        Vector3 perp = Vector3.Cross(forward.normalized, Vector3.up);

        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            Vector3 pos = Vector3.Lerp(start, end, t);

            float envelope = Mathf.Sin(Mathf.PI * t);
            float osc = Mathf.Sin(WobbleFrequency * t * Mathf.PI * 2f + Time.time * 8f) * wobble * envelope;
            pos += perp * osc;

            _grappleLine.SetPosition(i, pos);
        }
    }

    void HandleGlideInput()
    {
        if (!_hasGlide) return;

        if (Keyboard.current != null && Keyboard.current.digit3Key.wasPressedThisFrame)
        {
            // TODO: toggle glide on/off — plug your fly logic in here
        }
    }

    public void MoveTo(Vector3 worldPosition)
    {
        _followingPath = false;
        _pathLineGO.SetActive(false);
        _targetPosition = worldPosition + Vector3.up * (_headHalfHeight + GroundOffsetPadding);
        _hasTarget = true;
    }

    public void Teleport(Vector3 worldPosition)
    {
        Vector3 p = worldPosition + Vector3.up * (_headHalfHeight + GroundOffsetPadding);
        _head.transform.position = p;
        _prevHeadPos = p;
        _smoothVelocity = Vector3.zero;
        _hasTarget = false;
        _followingPath = false;
        _pathLineGO.SetActive(false);
        SeedTrail(p);
        for (int i = 0; i < _segments.Count; i++)
            _segments[i].transform.position = p;
    }
}
