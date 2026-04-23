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
    [Range(1, 64)] public int SegmentCount = 12;
    [Range(0.02f, 5f)] public float MinSpacing = 0.3f;
    [Range(0.02f, 5f)] public float MaxSpacing = 0.8f;

    [Header("Slinky — Squish on Stop")]
    [Range(0f, 20f)] public float SquishSpeed = 4f;

    [Header("Movement")]
    [Range(0.5f, 30f)] public float MoveSpeed = 6f;
    [Range(1f, 40f)] public float TurnSpeed = 10f;
    [Range(0.01f, 2f)] public float ArrivalThreshold = 0.15f;
    [Range(0f, 0.98f)] public float MoveSmoothing = 0.18f;
    [Range(0.1f, 5f)] public float GroundCheckAhead = 1.0f;

    [Header("Trail Resolution")]
    [Range(200, 4000)] public int TrailResolution = 1200;
    [Range(0.001f, 0.5f)] public float TrailMinDistance = 0.015f;

    [Header("Input Modes")]
    public bool CanClickMove = true;
    public bool CanDrawPath = true;

    [Header("Draw Path — Line Renderer")]
    [Range(0.01f, 1f)] public float PathLineWidth = 0.08f;
    public Color PathLineColor = new Color(1f, 0.85f, 0.2f, 0.85f);
    [Range(0.05f, 2f)] public float PathDrawMinDistance = 0.2f;
    [Range(2, 20)] public int PathSmoothing = 8;
    public float PathLineYOffset = 0.05f;

    [Header("Ground & Camera")]
    public LayerMask GroundMask = ~0;
    public Camera ClickCamera;
    public OrthoCameraFollow CameraFollow;
    [Range(-0.3f, 0.3f)] public float GroundOffsetPadding = 0f;
    [Range(0.1f, 5f)] public float ClickIndicatorLifetime = 1.0f;

    private GameObject _head;
    private List<GameObject> _segments = new List<GameObject>();

    private Vector3[] _nodePos;

    private Vector3[] _trail;
    private int _trailWriteIdx = 0;
    private int _trailCount = 0;
    private Vector3 _lastRecordedPos;

    // Movement
    private Vector3 _targetPosition;
    private bool _hasTarget = false;
    private Vector3 _smoothVelocity = Vector3.zero;
    private float _currentSpeed = 0f;
    private Vector3 _prevHeadPos;

    // Cached half-heights
    private float _headHalfHeight;
    private float _segHalfHeight;

    // Drawn-path
    private List<Vector3> _drawnRaw = new List<Vector3>();
    private List<Vector3> _drawnPath = new List<Vector3>();
    private int _pathIndex = 0;
    private bool _followingPath = false;
    private bool _isDragging = false;
    private Vector3 _lastDrawnPos;
    private LineRenderer _pathLine;
    private GameObject _pathLineGO;

    public float CurrentSpeed => _currentSpeed;
    public GameObject Head => _head;
    public IReadOnlyList<GameObject> Segments => _segments;

    void Start()
    {
        if (ClickCamera == null) ClickCamera = Camera.main;
        BuildSnake();
        SeedTrail(_head.transform.position);
        BuildPathLine();
        if (CameraFollow != null)
            CameraFollow.SetTarget(_head.transform, snapInstantly: true);
    }

    void Update()
    {
        HandleInput();
        MoveHead();
        UpdateHeadSpeed();
        RecordTrail();
        UpdateChain();
        ApplyNodePositions();
    }

    void BuildSnake()
    {
        int nodeCount = SegmentCount + 1; // 0 = head
        _nodePos = new Vector3[nodeCount];

        _head = HeadPrefab != null
            ? Instantiate(HeadPrefab, transform.position, Quaternion.identity)
            : MakeSphere(transform.position, new Color(0.95f, 0.3f, 0.2f), 0.55f);

        _head.name = "SnakeHead";
        _head.tag = "Player";
        _head.transform.SetParent(null);

        _headHalfHeight = GetColliderHalfHeight(_head);
        _prevHeadPos = _head.transform.position;
        _targetPosition = _head.transform.position;
        _nodePos[0] = _head.transform.position;

        for (int i = 0; i < SegmentCount; i++)
        {
            Vector3 pos = transform.position - transform.forward * MinSpacing * (i + 1);

            GameObject seg = SegmentPrefab != null
                ? Instantiate(SegmentPrefab, pos, Quaternion.identity)
                : MakeSphere(pos, Color.Lerp(
                      new Color(0.2f, 0.85f, 0.45f),
                      new Color(0.15f, 0.45f, 0.9f),
                      (float)i / Mathf.Max(1, SegmentCount - 1)), 0.45f);

            seg.name = $"Segment_{i:D2}";
            seg.tag = "Player";
            seg.transform.SetParent(null);
            _segments.Add(seg);
            _nodePos[i + 1] = pos;
        }

        _segHalfHeight = _segments.Count > 0
            ? GetColliderHalfHeight(_segments[0]) : 0.225f;
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

        var mat = new Material(Shader.Find("Sprites/Default")) { color = PathLineColor };
        _pathLine.material = mat;
        _pathLine.startColor = PathLineColor;
        _pathLine.endColor = new Color(PathLineColor.r, PathLineColor.g,
                                         PathLineColor.b, PathLineColor.a * 0.4f);
        _pathLineGO.SetActive(false);
    }

    float GetColliderHalfHeight(GameObject go)
    {
        Collider col = go.GetComponent<Collider>();
        Vector3 scale = go.transform.lossyScale;
        if (col is CapsuleCollider cap)
        {
            float ax = cap.direction == 1 ? scale.y : cap.direction == 0 ? scale.x : scale.z;
            return cap.height * 0.5f * ax;
        }
        if (col is SphereCollider sph) return sph.radius * Mathf.Max(scale.x, scale.y, scale.z);
        if (col is BoxCollider box) return box.size.y * 0.5f * scale.y;
        if (col != null) return col.bounds.extents.y;
        return scale.y * 0.5f;
    }

    GameObject MakeSphere(Vector3 pos, Color col, float radius)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * radius;
        var c = go.GetComponent<Collider>(); if (c) Destroy(c);
        var mr = go.GetComponent<MeshRenderer>();
        if (mr) mr.material = new Material(Shader.Find("Standard")) { color = col };
        return go;
    }

    Vector3 SnapToGround(Vector3 pos, float halfHeight)
    {
        if (Physics.Raycast(new Vector3(pos.x, pos.y + 10f, pos.z),
                            Vector3.down, out RaycastHit hit, 30f, GroundMask))
            return new Vector3(pos.x, hit.point.y + halfHeight + GroundOffsetPadding, pos.z);
        return Vector3.zero;
    }

    bool HasGroundAt(Vector3 pos)
        => Physics.Raycast(new Vector3(pos.x, pos.y + 10f, pos.z),
                           Vector3.down, 30f, GroundMask);

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
        Vector3 pos = _nodePos[0];
        if (Vector3.Distance(pos, _lastRecordedPos) < TrailMinDistance) return;
        _trailWriteIdx = (_trailWriteIdx + 1) % TrailResolution;
        _trail[_trailWriteIdx] = pos;
        _lastRecordedPos = pos;
        if (_trailCount < TrailResolution) _trailCount++;
    }

    Vector3 SampleTrail(float targetDist)
    {
        if (_trailCount <= 1) return _nodePos[0];

        float acc = 0f;
        Vector3 posA = _trail[_trailWriteIdx];

        for (int step = 1; step < _trailCount; step++)
        {
            int idxB = (_trailWriteIdx - step + TrailResolution) % TrailResolution;
            Vector3 posB = _trail[idxB];
            float segLen = Vector3.Distance(posA, posB);
            acc += segLen;
            if (acc >= targetDist)
            {
                float t = segLen > 0.0001f ? (acc - targetDist) / segLen : 0f;
                return Vector3.Lerp(posA, posB, t);
            }
            posA = posB;
        }
        return _trail[(_trailWriteIdx - _trailCount + 1 + TrailResolution) % TrailResolution];
    }

    void HandleInput()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        bool pressed = mouse.leftButton.wasPressedThisFrame;
        bool held = mouse.leftButton.isPressed;
        bool released = mouse.leftButton.wasReleasedThisFrame;

        Vector2 sp = mouse.position.ReadValue();
        Ray ray = ClickCamera.ScreenPointToRay(new Vector3(sp.x, sp.y, 0f));
        bool hit = Physics.Raycast(ray, out RaycastHit rh, Mathf.Infinity, GroundMask);

        if (CanDrawPath)
        {
            if (pressed && hit)
            {
                _isDragging = true; _followingPath = false; _hasTarget = false;
                _drawnRaw.Clear(); _drawnPath.Clear();
                Vector3 s = rh.point; s.y += PathLineYOffset;
                _drawnRaw.Add(s); _lastDrawnPos = s;
                _pathLineGO.SetActive(true);
                RefreshLine(_drawnRaw);
            }

            if (_isDragging && held && hit)
            {
                Vector3 pt = rh.point; pt.y += PathLineYOffset;
                if (Vector3.Distance(pt, _lastDrawnPos) >= PathDrawMinDistance)
                { _drawnRaw.Add(pt); _lastDrawnPos = pt; RefreshLine(_drawnRaw); }
            }

            if (_isDragging && released)
            {
                _isDragging = false;
                if (_drawnRaw.Count >= 2)
                {
                    _drawnPath = CatmullRom(_drawnRaw, PathSmoothing);
                    for (int i = _drawnPath.Count - 1; i >= 0; i--)
                    {
                        if (!HasGroundAt(_drawnPath[i])) { _drawnPath.RemoveAt(i); continue; }
                        Vector3 sn = SnapToGround(_drawnPath[i], _headHalfHeight);
                        if (sn == Vector3.zero) { _drawnPath.RemoveAt(i); continue; }
                        _drawnPath[i] = sn;
                    }
                    if (_drawnPath.Count >= 2)
                    { RefreshLine(DisplayPath(_drawnPath)); _pathIndex = 0; _followingPath = true; }
                    else _pathLineGO.SetActive(false);
                }
                else
                {
                    _pathLineGO.SetActive(false);
                    if (CanClickMove && hit) SetClickTarget(rh.point);
                }
            }
            if (_isDragging) return;
        }

        if (CanClickMove && !CanDrawPath && pressed && hit)
            SetClickTarget(rh.point);
    }

    void SetClickTarget(Vector3 gp)
    {
        if (!HasGroundAt(gp)) return;
        _followingPath = false; _pathLineGO.SetActive(false);
        _targetPosition = gp + Vector3.up * (_headHalfHeight + GroundOffsetPadding);
        _hasTarget = true;
        if (ClickIndicatorPrefab != null)
            Destroy(Instantiate(ClickIndicatorPrefab, gp, Quaternion.identity),
                    ClickIndicatorLifetime);
    }

    void MoveHead()
    {
        if (_followingPath && _drawnPath.Count > 0)
        {
            while (_pathIndex < _drawnPath.Count - 1 &&
                   Vector3.Distance(_nodePos[0], _drawnPath[_pathIndex]) < ArrivalThreshold)
                _pathIndex++;

            if (_pathIndex >= _drawnPath.Count)
            { _followingPath = false; _pathLineGO.SetActive(false); return; }

            _targetPosition = _drawnPath[_pathIndex];
            _hasTarget = true;
            UpdatePathFade();
        }

        if (!_hasTarget)
        {
            _smoothVelocity = Vector3.Lerp(_smoothVelocity, Vector3.zero, Time.deltaTime * 8f);
            if (_smoothVelocity.sqrMagnitude > 0.0001f)
            {
                Vector3 g = SnapToGround(_nodePos[0] + _smoothVelocity * Time.deltaTime,
                                         _headHalfHeight);
                if (g != Vector3.zero) _nodePos[0] = g;
                else _smoothVelocity = Vector3.zero;
            }
            return;
        }

        Vector3 diff = _targetPosition - _nodePos[0]; diff.y = 0f;
        if (diff.magnitude <= ArrivalThreshold) { if (!_followingPath) _hasTarget = false; return; }

        _smoothVelocity = Vector3.Lerp(_smoothVelocity, diff.normalized * MoveSpeed,
                                       Time.deltaTime * Mathf.Lerp(20f, 2f, MoveSmoothing));

        Vector3 cand = _nodePos[0] + _smoothVelocity * Time.deltaTime;
        if (!HasGroundAt(cand + _smoothVelocity.normalized * GroundCheckAhead))
        {
            _smoothVelocity = Vector3.zero; _hasTarget = false; _followingPath = false;
            _pathLineGO.SetActive(false); return;
        }

        Vector3 sn = SnapToGround(cand, _headHalfHeight);
        if (sn == Vector3.zero) { _smoothVelocity = Vector3.zero; _hasTarget = false; return; }

        _nodePos[0] = sn;

        Vector3 fd = _smoothVelocity; fd.y = 0f;
        if (fd.sqrMagnitude > 0.01f)
            _head.transform.rotation = Quaternion.Slerp(
                _head.transform.rotation,
                Quaternion.LookRotation(fd.normalized),
                Time.deltaTime * TurnSpeed);
    }

    void UpdateHeadSpeed()
    {
        _currentSpeed = (_nodePos[0] - _prevHeadPos).magnitude / Time.deltaTime;
        _prevHeadPos = _nodePos[0];
    }

    void UpdateChain()
    {
        float cumDist = 0f;

        for (int i = 1; i <= SegmentCount; i++)
        {
            Vector3 ahead = _nodePos[i - 1];
            Vector3 self = _nodePos[i];

            Vector3 toSelf = self - ahead;
            toSelf.y = 0f;
            float dist = toSelf.magnitude;

            if (dist > MaxSpacing)
            {
                float sampleDist = cumDist + MaxSpacing;
                Vector3 trailPt = SampleTrail(sampleDist);
                Vector3 grounded = SnapToGround(trailPt, _segHalfHeight);
                _nodePos[i] = grounded != Vector3.zero ? grounded : trailPt;
            }
            else
            {
                if (SquishSpeed > 0f && dist > MinSpacing)
                {
                    Vector3 dir = (ahead - self); dir.y = 0f;
                    float move = Mathf.Min(SquishSpeed * Time.deltaTime, dist - MinSpacing);
                    Vector3 newPos = self + dir.normalized * move;
                    Vector3 grounded = SnapToGround(newPos, _segHalfHeight);
                    _nodePos[i] = grounded != Vector3.zero ? grounded : newPos;
                }

                toSelf = _nodePos[i] - ahead; toSelf.y = 0f;
                if (toSelf.magnitude < MinSpacing && toSelf.magnitude > 0.0001f)
                {
                    Vector3 pushed = ahead + toSelf.normalized * MinSpacing;
                    Vector3 grounded = SnapToGround(pushed, _segHalfHeight);
                    _nodePos[i] = grounded != Vector3.zero ? grounded : pushed;
                }
            }

            Vector3 finalDiff = _nodePos[i] - _nodePos[i - 1]; finalDiff.y = 0f;
            cumDist += finalDiff.magnitude;
        }
    }

    void ApplyNodePositions()
    {
        _head.transform.position = _nodePos[0];

        for (int i = 0; i < SegmentCount; i++)
        {
            _segments[i].transform.position = _nodePos[i + 1];

            Vector3 toAhead = _nodePos[i] - _nodePos[i + 1]; toAhead.y = 0f;
            if (toAhead.sqrMagnitude > 0.0001f)
                _segments[i].transform.rotation = Quaternion.Slerp(
                    _segments[i].transform.rotation,
                    Quaternion.LookRotation(toAhead.normalized),
                    Time.deltaTime * TurnSpeed * 2f);
        }
    }

    void RefreshLine(List<Vector3> pts)
    {
        _pathLine.startWidth = PathLineWidth; _pathLine.endWidth = PathLineWidth;
        _pathLine.startColor = PathLineColor;
        _pathLine.endColor = new Color(PathLineColor.r, PathLineColor.g,
                                         PathLineColor.b, PathLineColor.a * 0.4f);
        _pathLine.positionCount = pts.Count;
        for (int i = 0; i < pts.Count; i++) _pathLine.SetPosition(i, pts[i]);
    }

    List<Vector3> DisplayPath(List<Vector3> groundedPath)
    {
        var d = new List<Vector3>(groundedPath.Count);
        foreach (var p in groundedPath)
            d.Add(new Vector3(p.x, p.y - _headHalfHeight + PathLineYOffset, p.z));
        return d;
    }

    void UpdatePathFade()
    {
        if (_pathIndex <= 0 || _pathIndex >= _drawnPath.Count) return;
        int rem = _drawnPath.Count - _pathIndex;
        if (rem < 2) { _pathLineGO.SetActive(false); return; }

        _pathLine.positionCount = rem + 1;
        _pathLine.SetPosition(0, new Vector3(_nodePos[0].x,
            _nodePos[0].y - _headHalfHeight + PathLineYOffset, _nodePos[0].z));
        for (int i = 0; i < rem; i++)
        {
            Vector3 p = _drawnPath[_pathIndex + i];
            _pathLine.SetPosition(i + 1, new Vector3(p.x,
                p.y - _headHalfHeight + PathLineYOffset, p.z));
        }
    }

    List<Vector3> CatmullRom(List<Vector3> pts, int sub)
    {
        var result = new List<Vector3>();
        if (pts.Count < 2) return result;
        var pad = new List<Vector3>();
        pad.Add(pts[0] + (pts[0] - pts[1]));
        pad.AddRange(pts);
        pad.Add(pts[pts.Count - 1] + (pts[pts.Count - 1] - pts[pts.Count - 2]));
        for (int i = 1; i < pad.Count - 2; i++)
        {
            Vector3 p0 = pad[i - 1], p1 = pad[i], p2 = pad[i + 1], p3 = pad[i + 2];
            for (int s = 0; s < sub; s++)
            {
                float t = ((float)s / sub), t2 = t * t, t3 = t2 * t;
                result.Add(0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3));
            }
        }
        result.Add(pad[pad.Count - 2]);
        return result;
    }

    public void MoveTo(Vector3 worldPosition)
    {
        if (!HasGroundAt(worldPosition)) return;
        _followingPath = false; _pathLineGO.SetActive(false);
        _targetPosition = worldPosition + Vector3.up * (_headHalfHeight + GroundOffsetPadding);
        _hasTarget = true;
    }

    public void Teleport(Vector3 worldPosition)
    {
        Vector3 p = worldPosition + Vector3.up * (_headHalfHeight + GroundOffsetPadding);
        for (int i = 0; i <= SegmentCount; i++) _nodePos[i] = p;
        _prevHeadPos = p; _smoothVelocity = Vector3.zero;
        _hasTarget = false; _followingPath = false; _pathLineGO.SetActive(false);
        SeedTrail(p);
        ApplyNodePositions();
    }
}