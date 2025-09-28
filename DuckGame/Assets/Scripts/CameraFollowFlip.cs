using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class CameraFollowFlip : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Framing (Direction Only)")]
    public Vector3 offset = new Vector3(0f, 1.8f, -1f);

    [Header("Distance / Zoom")]
    public float distance = 4.0f;
    public float minDistance = 2.0f;
    public float maxDistance = 8.0f;
    public float distanceSmoothTime = 0.15f;

    [Header("Orientation")]
    public float pitch = -10f;
    public float initialYaw = 15f;

    [Header("Smoothing")]
    public float positionSmoothTime = 0.15f;
    public float yawSmoothTime = 0.25f;

    [Header("X-Offset Flip")]
    public bool autoFlipXWithYaw = true;
    public float xFlipSmoothTime = 0.2f;

    [Header("Anti-Jitter")]
    public bool followInFixedUpdate = false;
    public bool smoothTargetPosition = true;
    public float targetSmoothTime = 0.06f;

    [Header("Startup")]
    public bool snapImmediatelyOnAwake = true;
    public bool snapOnSceneLoaded = true;

    public static CameraFollowFlip Instance { get; private set; }

    // Internal state
    private float _targetYaw;
    private float _currentYaw;
    private float _yawVelocity;
    private Vector3 _posVelocity;

    private float _currentDistance;
    private float _distanceVel;

    private float _targetXSign = 1f;
    private float _currentXSign = 1f;
    private float _xSignVel = 0f;

    private Vector3 _smoothedTargetPos;
    private Vector3 _targetPosVel;

    private bool _initialSnapDone = false;

    // NEW: lightweight re-acquire throttle so we don’t Find() every frame
    private float _reacquireTimer = 0f;
    private const float ReacquireInterval = 0.25f; // seconds

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _targetYaw = initialYaw;
        _currentYaw = initialYaw;

        distance = Mathf.Clamp(distance, minDistance, maxDistance);
        _currentDistance = distance;

        if (offset.sqrMagnitude < 1e-6f)
            offset = new Vector3(0f, 1.5f, -1f);

        if (autoFlipXWithYaw)
            _targetXSign = _currentXSign = (Mathf.Sign(_currentYaw) >= 0f ? 1f : -1f);

        // Try to find target early
        TryFindTargetNow();

        if (target != null)
        {
            _smoothedTargetPos = target.position;
            if (snapImmediatelyOnAwake)
            {
                SnapImmediate();
                _initialSnapDone = true;
            }
        }
    }

    void OnEnable()
    {
        if (snapOnSceneLoaded)
            SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        if (snapOnSceneLoaded)
            SceneManager.sceneLoaded -= OnSceneLoaded;

        if (Instance == this) Instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // If PlayerController already exists (DDOL), prefer it
        if (target == null || IsUnityNull(target))
            TryFindTargetNow();

        if (target != null)
        {
            _smoothedTargetPos = target.position;
            SnapImmediate();
            _initialSnapDone = true;
        }
        else
        {
            _initialSnapDone = false; // allow OnPreCull safety
        }
    }

    void OnPreCull()
    {
        if (!_initialSnapDone && target != null)
        {
            SnapImmediate();
            _initialSnapDone = true;
        }
    }

    void LateUpdate()
    {
        if (!followInFixedUpdate) FollowTick(Time.deltaTime);
    }

    void FixedUpdate()
    {
        if (followInFixedUpdate) FollowTick(Time.fixedDeltaTime);
    }

    private void FollowTick(float dt)
    {
        // NEW: self-heal if target went missing (e.g., scene reload in one level)
        if (target == null || IsUnityNull(target) || !target.gameObject.activeInHierarchy)
        {
            _reacquireTimer -= dt;
            if (_reacquireTimer <= 0f)
            {
                _reacquireTimer = ReacquireInterval;
                if (TryFindTargetNow())
                {
                    SnapImmediate(); // once on reacquire
                    _initialSnapDone = true;
                }
            }
            // If still no target, do nothing this frame
            if (target == null) return;
        }

        Vector3 targetPos = target.position;
        if (smoothTargetPosition)
            _smoothedTargetPos = Vector3.SmoothDamp(_smoothedTargetPos, targetPos, ref _targetPosVel, targetSmoothTime, Mathf.Infinity, dt);
        else
            _smoothedTargetPos = targetPos;

        _currentYaw = Mathf.SmoothDampAngle(_currentYaw, _targetYaw, ref _yawVelocity, yawSmoothTime, Mathf.Infinity, dt);

        Quaternion rot = Quaternion.Euler(pitch, _currentYaw, 0f);

        if (autoFlipXWithYaw)
            _currentXSign = Mathf.SmoothDamp(_currentXSign, _targetXSign, ref _xSignVel, xFlipSmoothTime, Mathf.Infinity, dt);
        else
            _currentXSign = 1f;

        Vector3 effectiveOffset = new Vector3(offset.x * _currentXSign, offset.y, offset.z);
        Vector3 offsetDir = (rot * effectiveOffset).normalized;
        if (offsetDir.sqrMagnitude < 1e-6f) offsetDir = rot * Vector3.back;

        distance = Mathf.Clamp(distance, minDistance, maxDistance);
        _currentDistance = Mathf.SmoothDamp(_currentDistance, distance, ref _distanceVel, distanceSmoothTime, Mathf.Infinity, dt);

        Vector3 desiredPos = _smoothedTargetPos + offsetDir * _currentDistance;

        transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref _posVelocity, positionSmoothTime, Mathf.Infinity, dt);
        transform.rotation = rot;
    }

    public void SnapImmediate()
    {
        if (target == null || IsUnityNull(target)) return;

        _yawVelocity = 0f;
        _distanceVel = 0f;
        _posVelocity = Vector3.zero;
        _targetPosVel = Vector3.zero;

        _currentYaw = _targetYaw;
        _currentDistance = Mathf.Clamp(distance, minDistance, maxDistance);
        if (autoFlipXWithYaw) { _currentXSign = _targetXSign; _xSignVel = 0f; } else { _currentXSign = 1f; }

        Vector3 anchor = target.position;
        Quaternion rot = Quaternion.Euler(pitch, _currentYaw, 0f);
        Vector3 effectiveOffset = new Vector3(offset.x * _currentXSign, offset.y, offset.z);
        Vector3 offsetDir = (rot * effectiveOffset).normalized;
        if (offsetDir.sqrMagnitude < 1e-6f) offsetDir = rot * Vector3.back;

        transform.position = anchor + offsetDir * _currentDistance;
        transform.rotation  = rot;

        _smoothedTargetPos = anchor;
    }

    public void SetYaw(float yawDegrees)
    {
        _targetYaw = yawDegrees;
        if (autoFlipXWithYaw)
            _targetXSign = (Mathf.Sign(_targetYaw) >= 0f ? 1f : -1f);
    }

    public void FlipYaw()
    {
        _targetYaw = -_targetYaw;
        if (autoFlipXWithYaw)
            _targetXSign = -_targetXSign;
    }

    public void SetDistance(float newDistance)
    {
        distance = Mathf.Clamp(newDistance, minDistance, maxDistance);
    }

    // --- helpers ---
    private bool TryFindTargetNow()
    {
        // Prefer the live PlayerController singleton if present
        if (PlayerController.Instance != null)
        {
            target = PlayerController.Instance.transform;
            return target != null;
        }

        // Fallback to tag search
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            target = player.transform;
            return true;
        }
        return false;
    }

    private static bool IsUnityNull(Object o)
    {
        // Unity’s destroyed objects compare equal to null
        return o == null;
    }
}
