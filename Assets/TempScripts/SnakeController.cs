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
    [Range(0.02f, 5f)]
    public float SegmentSpacing = 0.55f;

    [Header("Movement")]
    [Range(0.5f, 30f)]
    public float MoveSpeed = 6f;
    [Range(1f, 40f)]
    public float TurnSpeed = 10f;
    [Range(0.01f, 2f)]
    public float ArrivalThreshold = 0.15f;
    [Range(0f, 0.98f)]
    public float MoveSmoothing = 0.18f;
    [Range(0.1f, 5f)]
    public float GroundCheckAhead = 1.0f;

    [Header("Trail Resolution")]
    [Range(200, 2000)]
    public int TrailResolution = 800;
    [Range(0.001f, 0.5f)]
    public float TrailMinDistance = 0.02f;

    [Header("Slinky Spacing Stretch")]
    [Range(1f, 5f)]
    public float MaxSpacingStretch = 2.5f;
    [Range(0.1f, 1f)]
    public float MinSpacingStretch = 0.5f;
    [Range(1f, 30f)]
    public float SpacingLerpSpeed = 8f;

    [Header("Anti-Clip Separation")]
    [Range(0f, 2f)]
    public float MinSeparation = 0.3f;
    [Range(1, 4)]
    public int SeparationIterations = 1;
    [Range(0f, 90f)]
    public float SeparationRollStrength = 35f;
    [Range(1f, 20f)]
    public float RollDecaySpeed = 8f;

    [Header("Input Modes")]
    public bool CanClickMove = true;
    public bool CanDrawPath = true;

    [Header("Draw Path — Line Renderer")]
    [Range(0.01f, 1f)]
    public float PathLineWidth = 0.08f;
    public Color PathLineColor = new Color(1f, 0.85f, 0.2f, 0.85f);
    [Range(0.05f, 2f)]
    public float PathDrawMinDistance = 0.2f;
    [Range(2, 20)]
    public int PathSmoothing = 8;
    public float PathLineYOffset = 0.05f;

    [Header("Ground & Camera")]
    public LayerMask GroundMask = ~0;
    public Camera ClickCamera;
    public OrthoCameraFollow CameraFollow;
    public float GroundOffsetPadding = 0f;
    [Range(0.1f, 5f)]
    public float ClickIndicatorLifetime = 1.0f;

    [Header("Ability — Ball Throw (1)")]
    public GameObject BallPrefab;
    public Transform ThrowPoint;
    public float BallLaunchSpeed = 15f;
    public string BallTag = "Ball";

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

    [Header("Ability — Grapple (2)")]
    public float GrappleMaxDistance = 15f;
    public float GrapplePullSpeed = 12f;
    public float GrappleArrivalDistance = 1.5f;
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

    [Header("Ability — Glide (3)")]
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
        return Vector3.zero;
    }

    bool HasGroundAt(Vector3 pos)
    {
        Vector3 origin = new Vector3(pos.x, pos.y + 10f, pos.z);
        return Physics.Raycast(origin, Vector3.down, 30f, GroundMask);
    }

    Vector3 GetMouseGroundPosition()
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

                    for (int i = _drawnPath.Count - 1; i >= 0; i--)
                    {
                        if (!HasGroundAt(_drawnPath[i]))
                        {
                            _drawnPath.RemoveAt(i);
                            continue;
                        }
                        Vector3 snapped = SnapToGround(_drawnPath[i], _headHalfHeight);
                        if (snapped == Vector3.zero)
                        {
                            _drawnPath.RemoveAt(i);
                            continue;
                        }
                        snapped.y = Mathf.Max(snapped.y,
                                              _drawnPath[i].y - PathLineYOffset + _headHalfHeight);
                        _drawnPath[i] = snapped;
                    }

                    if (_drawnPath.Count >= 2)
                    {
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
                    }
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
        if (!HasGroundAt(groundPoint)) return;

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
            {
                Vector3 nextPos = _head.transform.position + _smoothVelocity * Time.deltaTime;
                Vector3 grounded = SnapToGround(nextPos, _headHalfHeight);
                if (grounded != Vector3.zero)
                    _head.transform.position = grounded;
                else
                    _smoothVelocity = Vector3.zero;
            }
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

        Vector3 candidatePos = pos + _smoothVelocity * Time.deltaTime;
        Vector3 edgeCheck = candidatePos + _smoothVelocity.normalized * GroundCheckAhead;

        if (!HasGroundAt(edgeCheck))
        {
            _smoothVelocity = Vector3.zero;
            _hasTarget = false;
            _followingPath = false;
            _pathLineGO.SetActive(false);
            return;
        }

        Vector3 snapped = SnapToGround(candidatePos, _headHalfHeight);
        if (snapped == Vector3.zero)
        {
            _smoothVelocity = Vector3.zero;
            _hasTarget = false;
            return;
        }

        _head.transform.position = snapped;

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
            Vector3 grounded = SnapToGround(raw, _segHalfHeight);
            _solvedPos[i + 1] = grounded != Vector3.zero ? grounded : _solvedPos[i];
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
                        Vector3 pushed = SnapToGround(_solvedPos[i], _segHalfHeight);
                        if (pushed != Vector3.zero) _solvedPos[i] = pushed;
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

        Vector3 spawnPos = ThrowPoint != null ? ThrowPoint.position : HeadTransform.position;
        Vector3 targetPoint = GetBallTargetPoint(spawnPos);

        if (!CalculateLaunchVelocity(spawnPos, targetPoint, BallLaunchSpeed, out Vector3 velocity))
            return;

        GameObject ball = Instantiate(BallPrefab, spawnPos, Quaternion.identity);

        if (!string.IsNullOrEmpty(BallTag))
            ball.tag = BallTag;

        Rigidbody rb = ball.GetComponent<Rigidbody>();
        if (rb != null)
            rb.linearVelocity = velocity;
    }

    Vector3 GetBallTargetPoint(Vector3 origin)
    {
        Plane plane = new Plane(Vector3.up, origin);
        Ray ray = ClickCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (plane.Raycast(ray, out float enter))
            return ray.GetPoint(enter);

        return origin;
    }

    bool CalculateLaunchVelocity(Vector3 start, Vector3 target, float speed, out Vector3 velocity)
    {
        velocity = Vector3.zero;

        Vector3 toTarget = target - start;
        Vector3 toTargetXZ = new Vector3(toTarget.x, 0, toTarget.z);

        float y = toTarget.y;
        float x = toTargetXZ.magnitude;
        float gravity = Mathf.Abs(Physics.gravity.y);
        float speedSquared = speed * speed;

        float underRoot = speedSquared * speedSquared -
                          gravity * (gravity * x * x + 2 * y * speedSquared);

        if (underRoot < 0)
            return false;

        float root = Mathf.Sqrt(underRoot);
        float angle = Mathf.Atan2(speedSquared + root, gravity * x);

        Vector3 direction = toTargetXZ.normalized;
        velocity = direction * speed * Mathf.Cos(angle) + Vector3.up * speed * Mathf.Sin(angle);

        if (float.IsNaN(velocity.x) || float.IsNaN(velocity.y) || float.IsNaN(velocity.z))
            return false;

        return true;
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

        Vector3 mouseWorld = GetMouseGroundPosition();
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
            _grabbedRb = hit.rigidbody;

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
            // TODO: glide logic
        }
    }

    public void MoveTo(Vector3 worldPosition)
    {
        if (!HasGroundAt(worldPosition)) return;
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
