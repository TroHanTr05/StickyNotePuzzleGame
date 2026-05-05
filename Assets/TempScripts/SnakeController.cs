// ─────────────────────────────────────────────────────────────────────────────
//  SnakeController.cs
//
//  CHANGES FROM ORIGINAL:
//    • Added Game.Runtime namespace.
//    • IGameLog resolved from ServiceResolver — replaces bare Debug.LogWarning.
//    • No logic changes.
// ─────────────────────────────────────────────────────────────────────────────
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Game339.Shared.Infrastructure.Diagnostics;

namespace Game.Runtime
{
    public class SnakeController : MonoBehaviour
    {
        private static IGameLog Log => ServiceResolver.Resolve<IGameLog>();

        [Header("Prefabs")]
        public GameObject HeadPrefab;
        public GameObject SegmentPrefab;

        [Header("Hierarchy")]
        [Tooltip("Optional root parent. Defaults to this transform.")]
        public Transform PlayerBody;
        public string HeadName          = "Head";
        public string SegmentFolderName = "Segments";
        public string PathFolderName    = "Runtime Helpers";

        [Header("Snake Shape")]
        [Range(1, 2000)] public int SegmentCount = 12;
        [Tooltip("Arc-distance between neighbouring segments at rest stretch (= 1).")]
        [Range(0.05f, 5f)] public float SegmentSpacing = 0.35f;

        [Header("Stretch — Levels  (0 = squashed · 1 = normal · 2 = stretched)")]
        [Range(0f, 2f)] public float StationaryLevel = 0.55f;
        [Range(0f, 2f)] public float NormalLevel     = 1.0f;
        [Range(0f, 2f)] public float SprintLevel     = 1.65f;

        [Header("Stretch — Limits & Response")]
        [Range(0.02f, 0.6f)] public float MinSquash      = 0.12f;
        [Range(1.0f, 2.5f)]  public float MaxStretchCap  = 1.8f;
        [Min(0f)]            public float StretchResponse = 8f;

        [Header("Stretch — Slinky Propagation (head → tail)")]
        [Min(0.01f)]      public float FollowRate  = 1.5f;
        [Range(0f, 1f)]   public float HeadPull    = 0.9f;
        [Range(0f, 0.2f)] public float RestBleed   = 0.05f;

        [Header("Movement")]
        [Range(0.5f, 30f)]  public float MoveSpeed         = 6f;
        [Range(1f, 40f)]    public float TurnSpeed         = 10f;
        [Range(1f, 40f)]    public float HeadAccelRate     = 6f;
        [Range(0.01f, 2f)]  public float ArrivalThreshold  = 0.15f;
        [Range(0f, 0.98f)]  public float MoveSmoothing     = 0.18f;
        [Range(0.1f, 5f)]   public float GroundCheckAhead  = 1.0f;

        [Tooltip("Speed below which the body starts squashing toward StationaryLevel.")]
        [Min(0f)] public float StallSpeedThreshold = 0.3f;

        [Header("Sprint")]
        public bool EnableSprint = true;
        [Range(1f, 4f)] public float SprintMultiplier    = 1.8f;
        public float SprintSecondsPerLoss = 2f;

        [Header("Trail Resolution")]
        [Range(400, 8000)]      public int   TrailResolution = 2000;
        [Range(0.001f, 0.2f)]   public float TrailMinDist    = 0.01f;

        [Header("Input Modes")]
        public bool CanClickMove = true;
        public bool CanDrawPath  = true;

        [Header("Draw Path — Line Renderer")]
        [Range(0.01f, 1f)]  public float PathLineWidth       = 0.08f;
        public Color        PathLineColor                    = new Color(1f, 0.85f, 0.2f, 0.85f);
        [Range(0.05f, 2f)]  public float PathDrawMinDistance = 0.2f;
        [Range(2, 20)]      public int   PathSmoothing       = 8;
        public float PathLineYOffset = 0.05f;

        [Header("Ground & Camera")]
        public LayerMask       GroundMask    = ~0;
        public Camera          ClickCamera;
        public OrthoCameraFollow CameraFollow;

        [Header("Collision")]
        public LayerMask CollisionMask = ~0;
        [Range(0.05f, 2f)]   public float CollisionRadius            = 0.35f;
        [Range(-0.3f, 0.3f)] public float GroundOffsetPadding        = 0f;
        [Range(-2f, 2f)]     public float HeadGroundHeightCorrection  = 0f;
        [Range(-2f, 2f)]     public float SegmentGroundHeightCorrection = 0f;

        [Header("Ball Throwing")]
        public GameObject BallPrefab;
        [Range(1f, 50f)]    public float ThrowForce      = 20f;
        [Range(0f, 1f)]     public float ThrowUpwardBias = 0.1f;
        [Range(0.1f, 5f)]   public float ThrowCooldown   = 0.5f;
        [Range(1f, 500f)]   public float MaxAimDistance  = 100f;
        public LayerMask AimLayerMask = ~0;
        public bool ShowThrowDebugRay = true;

        public float CurrentSpeed                    => _currentSpeed;
        public GameObject Head                       => _head;
        public IReadOnlyList<GameObject> Segments    => _segments;

        // ── Private state ─────────────────────────────────────────────────────

        GameObject _head;
        readonly List<GameObject> _segments = new();
        Transform _segmentFolder, _runtimeFolder;
        LineRenderer _pathLine;
        GameObject   _pathLineGO;

        float _headGroundH, _segGroundH;

        struct TrailNode { public Vector3 p; public float s; }
        readonly List<TrailNode> _trail = new();
        float   _trailLength;
        Vector3 _lastRecordedPos;

        float _tailSParam;

        readonly List<float> _stretch = new();
        float _headStretch = 1f;
        float _smoothedSpeed = 0f;

        Vector3 _nodeHead;
        Vector3 _prevHeadPos;
        Vector3 _smoothVelocity;
        float   _currentSpeed;
        float   _targetSpeed;

        Vector3 _targetPosition;
        bool _hasTarget;
        readonly List<Vector3> _drawnRaw  = new();
        readonly List<Vector3> _drawnPath = new();
        int  _pathIndex;
        bool _followingPath;
        bool _isDragging;
        Vector3 _lastDrawnPos;

        float _sprintAccum;
        int   _minSegCount;

        float _nextThrowTime;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        void Start()
        {
            if (ClickCamera == null) ClickCamera = Camera.main;

            SetupHierarchyFolders();
            BuildSnake();
            SeedTrail(_nodeHead);
            BuildPathLine();

            if (CameraFollow != null)
                CameraFollow.SetTarget(_head.transform, snapInstantly: true);
        }

        void Update()
        {
            HandleInput();
            HandleThrowInput();

            MoveHead();
            UpdateSpeed();

            bool sprinting = EnableSprint && Input.GetKey(KeyCode.Mouse0) &&
                             _segments.Count > _minSegCount;
            HandleSprintDecay(sprinting);
            UpdateStretch(sprinting);

            RecordTrail();
            PlaceSegments();
            ApplyTransforms();
        }

        // ── Hierarchy setup ───────────────────────────────────────────────────

        void SetupHierarchyFolders()
        {
            if (PlayerBody == null) PlayerBody = transform;
            gameObject.tag = "Player";
            if (PlayerBody != transform) PlayerBody.gameObject.tag = "Player";

            _segmentFolder = GetOrCreateChild(PlayerBody, SegmentFolderName);
            _runtimeFolder = GetOrCreateChild(PlayerBody, PathFolderName);
            _segmentFolder.gameObject.tag = "Player";
        }

        Transform GetOrCreateChild(Transform parent, string name)
        {
            Transform t = parent.Find(name);
            if (t) return t;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        // ── Build ─────────────────────────────────────────────────────────────

        void BuildSnake()
        {
            Vector3 start = SnapToGround(transform.position, 0f);
            if (start == Vector3.zero) start = transform.position;

            _head = HeadPrefab
                ? Instantiate(HeadPrefab, start, Quaternion.identity, PlayerBody)
                : MakeSphere(start, new Color(0.95f, 0.3f, 0.2f), 0.55f, PlayerBody);
            _head.name = HeadName;
            _head.tag  = "Player";

            _headGroundH  = ResolveGroundHeight(_head, HeadGroundHeightCorrection);
            Vector3 headStart = SnapToGround(start, _headGroundH);
            if (headStart == Vector3.zero) headStart = start + Vector3.up * _headGroundH;

            _head.transform.position = headStart;
            _nodeHead      = headStart;
            _prevHeadPos   = headStart;
            _targetPosition = headStart;

            for (int i = 0; i < SegmentCount; i++)
            {
                Vector3 raw = transform.position - transform.forward * SegmentSpacing * (i + 1);
                Vector3 pos = SnapToGround(raw, 0f);
                if (pos == Vector3.zero) pos = raw;

                GameObject seg = SegmentPrefab
                    ? Instantiate(SegmentPrefab, pos, Quaternion.identity, _segmentFolder)
                    : MakeSphere(pos,
                        Color.Lerp(new Color(0.2f, 0.85f, 0.45f), new Color(0.15f, 0.45f, 0.9f),
                                   (float)i / Mathf.Max(1, SegmentCount - 1)),
                        0.45f, _segmentFolder);

                seg.name = $"Segment_{i:D2}";
                seg.tag  = "Player";

                if (i == 0)
                    _segGroundH = ResolveGroundHeight(seg, SegmentGroundHeightCorrection);

                _segments.Add(seg);
            }

            if (_segments.Count == 0) _segGroundH = _headGroundH;

            _minSegCount = _segments.Count;
            ResizeStretchArrays();

            _headStretch = LevelToStretch(NormalLevel);
            for (int i = 0; i < _stretch.Count; i++) _stretch[i] = _headStretch;
        }

        void BuildPathLine()
        {
            _pathLineGO = new GameObject("DrawPathLine");
            _pathLineGO.transform.SetParent(_runtimeFolder, false);
            _pathLine = _pathLineGO.AddComponent<LineRenderer>();
            _pathLine.useWorldSpace    = true;
            _pathLine.positionCount    = 0;
            _pathLine.startWidth       = PathLineWidth;
            _pathLine.endWidth         = PathLineWidth;
            _pathLine.numCornerVertices = 8;
            _pathLine.numCapVertices   = 8;
            var mat = new Material(Shader.Find("Sprites/Default")) { color = PathLineColor };
            _pathLine.material   = mat;
            _pathLine.startColor = PathLineColor;
            _pathLine.endColor   = new Color(PathLineColor.r, PathLineColor.g, PathLineColor.b,
                                             PathLineColor.a * 0.4f);
            _pathLineGO.SetActive(false);
        }

        // ── Movement ──────────────────────────────────────────────────────────

        void MoveHead()
        {
            if (_followingPath && _drawnPath.Count > 0)
            {
                while (_pathIndex < _drawnPath.Count - 1 &&
                       Vector3.Distance(_nodeHead, _drawnPath[_pathIndex]) < ArrivalThreshold)
                    _pathIndex++;

                if (_pathIndex >= _drawnPath.Count)
                {
                    _followingPath = false;
                    _pathLineGO.SetActive(false);
                }
                else
                {
                    _targetPosition = _drawnPath[_pathIndex];
                    _hasTarget = true;
                    UpdatePathFade();
                }
            }

            if (!_hasTarget)
            {
                _smoothVelocity = Vector3.Lerp(_smoothVelocity, Vector3.zero, Time.deltaTime * 8f);
                if (_smoothVelocity.sqrMagnitude > 0.0001f)
                {
                    Vector3 g = SnapToGround(_nodeHead + _smoothVelocity * Time.deltaTime, _headGroundH);
                    if (g != Vector3.zero) _nodeHead = g;
                    else _smoothVelocity = Vector3.zero;
                }
                return;
            }

            Vector3 diff = _targetPosition - _nodeHead;
            diff.y = 0f;
            if (diff.magnitude <= ArrivalThreshold)
            {
                if (!_followingPath) _hasTarget = false;
                return;
            }

            bool sprinting = EnableSprint && Input.GetKey(KeyCode.Mouse0) && _segments.Count > _minSegCount;
            float topSpeed  = sprinting ? MoveSpeed * SprintMultiplier : MoveSpeed;
            _targetSpeed    = topSpeed;

            _smoothVelocity = Vector3.Lerp(
                _smoothVelocity,
                diff.normalized * _targetSpeed,
                Time.deltaTime * Mathf.Lerp(20f, 2f, MoveSmoothing));

            Vector3 step = _smoothVelocity * Time.deltaTime;

            if (step.magnitude > 0.0001f)
            {
                Vector3 origin = _nodeHead + Vector3.up * _headGroundH;
                if (Physics.SphereCast(origin, CollisionRadius, step.normalized,
                                       out RaycastHit wh, step.magnitude + CollisionRadius,
                                       CollisionMask, QueryTriggerInteraction.Ignore))
                {
                    Vector3 n = wh.normal; n.y = 0f;
                    if (n.sqrMagnitude > 0.001f) n.Normalize();
                    _smoothVelocity = Vector3.ProjectOnPlane(_smoothVelocity, n);
                    step = _smoothVelocity * Time.deltaTime;
                }
            }

            Vector3 cand = _nodeHead + step;
            Vector3 fwd  = _smoothVelocity.sqrMagnitude > 0.001f
                           ? _smoothVelocity.normalized * GroundCheckAhead
                           : Vector3.zero;
            if (!HasGroundAt(cand + fwd))
            {
                _smoothVelocity = Vector3.zero;
                _hasTarget      = false;
                _followingPath  = false;
                _pathLineGO.SetActive(false);
                return;
            }

            Vector3 sn = SnapToGround(cand, _headGroundH);
            if (sn == Vector3.zero) { _smoothVelocity = Vector3.zero; _hasTarget = false; return; }

            _nodeHead = sn;

            Vector3 fd = _smoothVelocity; fd.y = 0f;
            if (fd.sqrMagnitude > 0.01f)
            {
                _head.transform.rotation = Quaternion.Slerp(
                    _head.transform.rotation,
                    Quaternion.LookRotation(fd.normalized),
                    Time.deltaTime * TurnSpeed);
            }
        }

        void UpdateSpeed()
        {
            if (Time.deltaTime <= 0f) return;
            _currentSpeed = (_nodeHead - _prevHeadPos).magnitude / Time.deltaTime;
            _prevHeadPos  = _nodeHead;
        }

        // ── Trail ─────────────────────────────────────────────────────────────

        void SeedTrail(Vector3 pos)
        {
            _trail.Clear();
            _trailLength = 0f;
            PushTrailNode(pos);
            PushTrailNode(pos);
            _lastRecordedPos = pos;
            _tailSParam      = 0f;
        }

        void PushTrailNode(Vector3 p)
        {
            float s = _trail.Count == 0
                      ? 0f
                      : _trail[^1].s + Vector3.Distance(_trail[^1].p, p);
            _trail.Add(new TrailNode { p = p, s = s });
            _trailLength = s;
        }

        void RecordTrail()
        {
            if (_trail.Count == 0) { PushTrailNode(_nodeHead); PushTrailNode(_nodeHead); return; }

            float d = Vector3.Distance(_nodeHead, _lastRecordedPos);
            if (d < TrailMinDist) return;

            PushTrailNode(_nodeHead);
            _lastRecordedPos = _nodeHead;

            TrimTrail();

            if (_trail.Count > TrailResolution)
                _trail.RemoveRange(0, _trail.Count - TrailResolution);
        }

        void TrimTrail()
        {
            int   gaps  = Mathf.Max(0, _segments.Count);
            float worst = gaps * SegmentSpacing * Mathf.Max(1f, MaxStretchCap);
            float keep  = (worst + 6f * SegmentSpacing) * 1.6f;

            float minS = Mathf.Max(0f, _trailLength - keep);
            minS = Mathf.Min(minS, _tailSParam);

            int remove = 0;
            for (int i = 0; i < _trail.Count - 2; i++)
            {
                if (_trail[i + 1].s <= minS) remove++;
                else break;
            }

            if (remove > 0)
            {
                float baseS = _trail[0].s;
                _trail.RemoveRange(0, remove);
                for (int i = 0; i < _trail.Count; i++)
                { var n = _trail[i]; n.s -= baseS; _trail[i] = n; }
                _trailLength -= baseS;
                _tailSParam   = Mathf.Max(0f, _tailSParam - baseS);
            }
        }

        Vector3 SampleTrail(float backFromHead)
        {
            if (_trail.Count < 2) return _nodeHead;

            float targetS = Mathf.Max(0f, _trailLength - backFromHead);

            int lo = 0, hi = _trail.Count - 1;
            while (lo < hi)
            {
                int   mid = (lo + hi) >> 1;
                float sm  = (mid == _trail.Count - 1) ? _trailLength : _trail[mid].s;
                if (sm < targetS) lo = mid + 1;
                else              hi = mid;
            }

            int i2 = Mathf.Clamp(lo, 1, _trail.Count - 1);
            int i1 = i2 - 1;

            Vector3 p1 = _trail[i1].p; float s1 = _trail[i1].s;
            Vector3 p2 = (i2 == _trail.Count - 1) ? _nodeHead : _trail[i2].p;
            float   s2 = (i2 == _trail.Count - 1) ? _trailLength : _trail[i2].s;

            if (Mathf.Approximately(s1, s2)) return p2;
            return Vector3.Lerp(p1, p2, Mathf.InverseLerp(s1, s2, targetS));
        }

        // ── Segment placement ─────────────────────────────────────────────────

        void PlaceSegments()
        {
            int gaps = _segments.Count;
            if (gaps == 0) return;

            float desiredLen = 0f;
            for (int i = 0; i < gaps; i++)
                desiredLen += SegmentSpacing * GetStretchClamped(i);

            float targetTailS = Mathf.Max(0f, _trailLength - desiredLen);
            if (targetTailS > _tailSParam) _tailSParam = targetTailS;

            float sFromHead = 0f;
            for (int i = 0; i < gaps; i++)
            {
                sFromHead += SegmentSpacing * GetStretchClamped(i);
                float distFromTail = desiredLen - sFromHead;
                float sAbs         = Mathf.Clamp(_tailSParam + distFromTail, 0f, _trailLength);
                float backFromHead = Mathf.Max(0f, _trailLength - sAbs);

                Vector3 raw      = SampleTrail(backFromHead);
                Vector3 grounded = SnapToGround(raw, _segGroundH);
                _segments[i].transform.position = grounded != Vector3.zero ? grounded : raw;
            }
        }

        // ── Stretch ───────────────────────────────────────────────────────────

        float LevelToStretch(float lv)
        {
            lv = Mathf.Clamp(lv, 0f, 2f);
            if (lv <= 1f) return Mathf.Lerp(MinSquash, 1f, lv);
            return Mathf.Lerp(1f, MaxStretchCap, lv - 1f);
        }

        float ComputeTargetLevel(bool sprinting)
        {
            if (sprinting) return SprintLevel;
            float move01 = Mathf.InverseLerp(
                StallSpeedThreshold,
                Mathf.Max(StallSpeedThreshold + 0.0001f, MoveSpeed),
                _smoothedSpeed);
            return Mathf.Lerp(StationaryLevel, NormalLevel, move01);
        }

        void UpdateStretch(bool sprinting)
        {
            _smoothedSpeed = Mathf.Lerp(_smoothedSpeed, _currentSpeed, Time.deltaTime * 8f);

            float targetLevel   = ComputeTargetLevel(sprinting);
            float targetStretch = Mathf.Clamp(LevelToStretch(targetLevel), MinSquash, MaxStretchCap);

            _headStretch = Mathf.MoveTowards(_headStretch, targetStretch, Time.deltaTime * StretchResponse);
            StepStretchChain(Time.deltaTime, targetStretch);
        }

        void StepStretchChain(float dt, float restStretch)
        {
            if (_stretch.Count == 0) return;
            float prev = _headStretch;
            for (int i = 0; i < _stretch.Count; i++)
            {
                float cur      = _stretch[i];
                float fromPrev = Mathf.Lerp(cur, prev, HeadPull);
                float target   = Mathf.Lerp(fromPrev, restStretch, RestBleed);
                float delta    = target - cur;
                float maxStep  = FollowRate * dt;
                cur = Mathf.Abs(delta) > maxStep ? cur + Mathf.Sign(delta) * maxStep : target;
                _stretch[i] = Mathf.Clamp(cur, MinSquash, MaxStretchCap);
                prev = _stretch[i];
            }
        }

        void ResizeStretchArrays(bool inheritTail = false)
        {
            int needed = _segments.Count;
            if (_stretch.Count == needed) return;
            if (_stretch.Count < needed)
            {
                float tail    = _stretch.Count > 0 ? _stretch[^1] : 1f;
                float clamped = Mathf.Clamp(tail, MinSquash, MaxStretchCap);
                for (int i = _stretch.Count; i < needed; i++)
                    _stretch.Add(inheritTail ? clamped : 1f);
            }
            else
            {
                _stretch.RemoveRange(needed, _stretch.Count - needed);
            }
        }

        float GetStretchClamped(int i)
        {
            float s = (i >= 0 && i < _stretch.Count) ? _stretch[i] : 1f;
            return Mathf.Clamp(s, MinSquash, MaxStretchCap);
        }

        // ── Transform application ─────────────────────────────────────────────

        void ApplyTransforms()
        {
            _head.transform.position = _nodeHead;

            for (int i = 0; i < _segments.Count; i++)
            {
                Vector3 ahead   = i == 0 ? _nodeHead : _segments[i - 1].transform.position;
                Vector3 self    = _segments[i].transform.position;
                Vector3 toAhead = ahead - self; toAhead.y = 0f;
                if (toAhead.sqrMagnitude > 0.0001f)
                {
                    _segments[i].transform.rotation = Quaternion.Slerp(
                        _segments[i].transform.rotation,
                        Quaternion.LookRotation(toAhead.normalized),
                        Time.deltaTime * TurnSpeed * 2f);
                }
            }
        }

        // ── Sprint ────────────────────────────────────────────────────────────

        void HandleSprintDecay(bool sprinting)
        {
            if (!sprinting) return;
            _sprintAccum += Time.deltaTime;
            while (_sprintAccum >= SprintSecondsPerLoss && _segments.Count > _minSegCount)
            {
                RemoveSegment();
                _sprintAccum -= SprintSecondsPerLoss;
            }
        }

        public void AddSegment()
        {
            Vector3 tailPos = _segments.Count > 0
                              ? _segments[^1].transform.position
                              : _nodeHead;

            GameObject seg = SegmentPrefab
                ? Instantiate(SegmentPrefab, tailPos, Quaternion.identity, _segmentFolder)
                : MakeSphere(tailPos, new Color(0.2f, 0.85f, 0.45f), 0.45f, _segmentFolder);

            seg.name = $"Segment_{_segments.Count:D2}";
            seg.tag  = "Player";
            _segments.Add(seg);
            ResizeStretchArrays(inheritTail: true);
        }

        void RemoveSegment()
        {
            if (_segments.Count <= _minSegCount) return;
            GameObject tail = _segments[^1];
            _segments.RemoveAt(_segments.Count - 1);
            if (tail) Destroy(tail);
            ResizeStretchArrays();
        }

        // ── Input ─────────────────────────────────────────────────────────────

        void HandleInput()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            bool pressed  = mouse.leftButton.wasPressedThisFrame;
            bool held     = mouse.leftButton.isPressed;
            bool released = mouse.leftButton.wasReleasedThisFrame;

            Vector2 sp  = mouse.position.ReadValue();
            Ray ray     = ClickCamera.ScreenPointToRay(new Vector3(sp.x, sp.y, 0f));
            bool hit    = Physics.Raycast(ray, out RaycastHit rh, Mathf.Infinity, GroundMask);

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
                        var smoothed = CatmullRom(_drawnRaw, PathSmoothing);
                        _drawnPath.Clear();
                        foreach (var p in smoothed)
                        {
                            if (!HasGroundAt(p)) continue;
                            Vector3 sn = SnapToGround(p, _headGroundH);
                            if (sn != Vector3.zero) _drawnPath.Add(sn);
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

            if (CanClickMove && !CanDrawPath && pressed && hit) SetClickTarget(rh.point);
        }

        void SetClickTarget(Vector3 gp)
        {
            if (!HasGroundAt(gp)) return;
            _followingPath = false; _pathLineGO.SetActive(false);
            _targetPosition = SnapToGround(gp, _headGroundH);
            _hasTarget = _targetPosition != Vector3.zero;
        }

        void HandleThrowInput()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;
            if (mouse.rightButton.wasPressedThisFrame && Time.time >= _nextThrowTime)
            { ThrowBall(); _nextThrowTime = Time.time + ThrowCooldown; }
        }

        void ThrowBall()
        {
            if (!BallPrefab)
            {
                Log.Warn("SnakeController: BallPrefab not assigned.");
                return;
            }

            Vector3 spawnPos   = _head.transform.position;
            Vector3 aimTarget  = GetAimTarget(spawnPos);
            Vector3 throwDir   = (aimTarget - spawnPos).normalized + Vector3.up * ThrowUpwardBias;
            throwDir.Normalize();

            GameObject ball = Instantiate(BallPrefab, spawnPos, Quaternion.identity);

            Collider ballCol = ball.GetComponent<Collider>();
            if (ballCol) IgnoreSnakeCollision(ballCol);

            Rigidbody rb = ball.GetComponent<Rigidbody>();
            if (rb) rb.AddForce(throwDir * ThrowForce, ForceMode.Impulse);
            Destroy(ball, 10f);
        }

        void IgnoreSnakeCollision(Collider ballCol)
        {
            Collider hc = _head.GetComponent<Collider>();
            if (hc) Physics.IgnoreCollision(ballCol, hc);
            foreach (var seg in _segments)
            {
                Collider sc = seg.GetComponent<Collider>();
                if (sc) Physics.IgnoreCollision(ballCol, sc);
            }
        }

        Vector3 GetAimTarget(Vector3 spawn)
        {
            var mouse  = Mouse.current;
            Vector2 sp = mouse.position.ReadValue();
            Ray ray    = ClickCamera.ScreenPointToRay(new Vector3(sp.x, sp.y, 0f));
            if (ShowThrowDebugRay)
                Debug.DrawRay(ray.origin, ray.direction * MaxAimDistance, Color.red, 0.5f);
            return Physics.Raycast(ray, out RaycastHit hit, MaxAimDistance, AimLayerMask)
                   ? hit.point
                   : ray.origin + ray.direction * MaxAimDistance;
        }

        // ── Ground utilities ──────────────────────────────────────────────────

        Vector3 SnapToGround(Vector3 pos, float groundHeight)
        {
            if (Physics.Raycast(new Vector3(pos.x, pos.y + 10f, pos.z),
                                Vector3.down, out RaycastHit hit, 30f, GroundMask))
                return new Vector3(pos.x, hit.point.y + groundHeight, pos.z);
            return Vector3.zero;
        }

        bool HasGroundAt(Vector3 pos) =>
            Physics.Raycast(new Vector3(pos.x, pos.y + 10f, pos.z), Vector3.down, 30f, GroundMask);

        float ResolveGroundHeight(GameObject go, float correction)
        {
            if (TryGetVisualBounds(go, out Bounds b))
                return Mathf.Max(0f, go.transform.position.y - b.min.y) + GroundOffsetPadding + correction;
            Collider col = go.GetComponentInChildren<Collider>();
            if (col)
                return Mathf.Max(0f, go.transform.position.y - col.bounds.min.y) + GroundOffsetPadding + correction;
            return GroundOffsetPadding + correction;
        }

        bool TryGetVisualBounds(GameObject go, out Bounds bounds)
        {
            bounds = new Bounds(go.transform.position, Vector3.zero);
            bool found = false;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled) continue;
                if (!found) { bounds = r.bounds; found = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return found;
        }

        // ── Path line helpers ─────────────────────────────────────────────────

        void RefreshLine(List<Vector3> pts)
        {
            _pathLine.startWidth = PathLineWidth;
            _pathLine.endWidth   = PathLineWidth;
            _pathLine.startColor = PathLineColor;
            _pathLine.endColor   = new Color(PathLineColor.r, PathLineColor.g, PathLineColor.b,
                                             PathLineColor.a * 0.4f);
            _pathLine.positionCount = pts.Count;
            for (int i = 0; i < pts.Count; i++) _pathLine.SetPosition(i, pts[i]);
        }

        List<Vector3> DisplayPath(List<Vector3> path)
        {
            var d = new List<Vector3>(path.Count);
            foreach (var p in path)
                d.Add(new Vector3(p.x, p.y - _headGroundH + PathLineYOffset, p.z));
            return d;
        }

        void UpdatePathFade()
        {
            if (_pathIndex <= 0 || _pathIndex >= _drawnPath.Count) return;
            int rem = _drawnPath.Count - _pathIndex;
            if (rem < 2) { _pathLineGO.SetActive(false); return; }

            _pathLine.positionCount = rem + 1;
            _pathLine.SetPosition(0, new Vector3(_nodeHead.x,
                                                  _nodeHead.y - _headGroundH + PathLineYOffset,
                                                  _nodeHead.z));
            for (int i = 0; i < rem; i++)
            {
                Vector3 p = _drawnPath[_pathIndex + i];
                _pathLine.SetPosition(i + 1, new Vector3(p.x, p.y - _headGroundH + PathLineYOffset, p.z));
            }
        }

        List<Vector3> CatmullRom(List<Vector3> pts, int sub)
        {
            var result = new List<Vector3>();
            if (pts.Count < 2) return result;
            var pad = new List<Vector3>();
            pad.Add(pts[0] + (pts[0] - pts[1]));
            pad.AddRange(pts);
            pad.Add(pts[^1] + (pts[^1] - pts[^2]));
            for (int i = 1; i < pad.Count - 2; i++)
            {
                Vector3 p0 = pad[i - 1], p1 = pad[i], p2 = pad[i + 1], p3 = pad[i + 2];
                for (int s = 0; s < sub; s++)
                {
                    float t = (float)s / sub, t2 = t * t, t3 = t2 * t;
                    result.Add(0.5f * (2f * p1 + (-p0 + p2) * t +
                                       (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                                       (-p0 + 3f * p1 - 3f * p2 + p3) * t3));
                }
            }
            result.Add(pad[^2]);
            return result;
        }

        // ── Public API ────────────────────────────────────────────────────────

        public void MoveTo(Vector3 worldPosition)
        {
            if (!HasGroundAt(worldPosition)) return;
            _followingPath  = false; _pathLineGO.SetActive(false);
            _targetPosition = SnapToGround(worldPosition, _headGroundH);
            _hasTarget      = _targetPosition != Vector3.zero;
        }

        public void Teleport(Vector3 worldPosition)
        {
            Vector3 p = SnapToGround(worldPosition, _headGroundH);
            if (p == Vector3.zero) p = worldPosition + Vector3.up * _headGroundH;
            _nodeHead = p; _prevHeadPos = p;
            _head.transform.position = p;
            foreach (var seg in _segments) seg.transform.position = p;
            _smoothVelocity = Vector3.zero; _hasTarget = false;
            _followingPath  = false; _pathLineGO.SetActive(false);
            SeedTrail(p);
            ResizeStretchArrays();
        }

        // ── Fallback sphere builder ───────────────────────────────────────────

        GameObject MakeSphere(Vector3 pos, Color col, float radius, Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.transform.SetParent(parent, true);
            go.transform.position   = pos;
            go.transform.localScale = Vector3.one * radius;
            var c = go.GetComponent<Collider>(); if (c) Destroy(c);
            var mr = go.GetComponent<MeshRenderer>();
            if (mr) mr.material = new Material(Shader.Find("Standard")) { color = col };
            go.tag = "Player";
            return go;
        }
    }
}
