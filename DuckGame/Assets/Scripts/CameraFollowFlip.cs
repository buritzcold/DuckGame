using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class CameraFollowFlip : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Player or object to follow. If null, found by tag \"Player\" at runtime.")]
    public Transform target;

    [Header("Framing (Direction Only)")]
    [Tooltip("Only the DIRECTION of this vector is used (it is normalized).")]
    public Vector3 offset = new Vector3(0f, 1.8f, -1f);

    [Header("Distance / Zoom")]
    [Tooltip("How far the camera sits along the offset direction from the target.")]
    public float distance = 4.0f;
    public float minDistance = 2.0f;
    public float maxDistance = 8.0f;
    [Tooltip("Seconds to smoothly blend distance changes.")]
    public float distanceSmoothTime = 0.15f;

    [Header("Orientation")]
    [Tooltip("Constant downward tilt in degrees (negative looks down).")]
    public float pitch = -10f;
    [Tooltip("Starting yaw (positive rotates to the right).")]
    public float initialYaw = 15f;

    [Header("Smoothing")]
    [Tooltip("Seconds to smoothly catch up position along the path.")]
    public float positionSmoothTime = 0.15f;
    [Tooltip("Seconds to smoothly blend yaw when flipped/changed.")]
    public float yawSmoothTime = 0.25f;

    [Header("X-Offset Flip")]
    [Tooltip("If true, the X component of the camera's offset flips when yaw flips (mirrors horizontally).")]
    public bool autoFlipXWithYaw = true;
    [Tooltip("Seconds to smoothly blend the X-flip (1 -> -1).")]
    public float xFlipSmoothTime = 0.2f;

    [Header("Anti-Jitter")]
    [Tooltip("Follow during FixedUpdate instead of LateUpdate (useful if the target is physics-driven).")]
    public bool followInFixedUpdate = false;
    [Tooltip("Smooth the target's own position before aiming the camera (reduces micro-jitter).")]
    public bool smoothTargetPosition = true;
    [Tooltip("Seconds to smooth target position toward its actual position.")]
    public float targetSmoothTime = 0.06f;

    [Header("Startup")]
    [Tooltip("Snap camera onto target immediately in Awake if possible.")]
    public bool snapImmediatelyOnAwake = true;
    [Tooltip("Also snap when a scene is loaded (before first Update).")]
    public bool snapOnSceneLoaded = true;

    public static CameraFollowFlip Instance { get; private set; }

    // Internal state
    private float _targetYaw;
    private float _currentYaw;
    private float _yawVelocity;          // For SmoothDampAngle
    private Vector3 _posVelocity;        // For SmoothDamp (camera position)

    private float _currentDistance;
    private float _distanceVel;          // For SmoothDamp (distance)

    // X-offset flip state (smoothly transitions between +1 and -1)
    private float _targetXSign = 1f;
    private float _currentXSign = 1f;
    private float _xSignVel = 0f;

    // Smoothed target position
    private Vector3 _smoothedTargetPos;
    private Vector3 _targetPosVel;

    // First-frame snap guards
    private bool _initialSnapDone = false;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Initialize core state
        _targetYaw = initialYaw;
        _currentYaw = initialYaw;

        distance = Mathf.Clamp(distance, minDistance, maxDistance);
        _currentDistance = distance;

        if (offset.sqrMagnitude < 1e-6f)
            offset = new Vector3(0f, 1.5f, -1f);

        if (autoFlipXWithYaw)
            _targetXSign = _currentXSign = (Mathf.Sign(_currentYaw) >= 0f ? 1f : -1f);

        // Try to find target early
        if (target == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) target = player.transform;
        }

        if (target != null)
        {
            _smoothedTargetPos = target.position;
            if (snapImmediatelyOnAwake)
            {
                SnapImmediate();          // <— Instant, no smoothing, before first render
                _initialSnapDone = true;  // We’re good; OnPreCull safety likely not needed
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
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Reacquire player if needed
        if (target == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) target = player.transform;
        }

        if (target != null)
        {
            _smoothedTargetPos = target.position;
            SnapImmediate();          // Snap again on fresh scene
            _initialSnapDone = true;
        }
        else
        {
            // If target still missing, allow OnPreCull to handle once it appears
            _initialSnapDone = false;
        }
    }

    // Safety net: If for any reason we haven't snapped yet, do it right before the first render.
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
        if (target == null) return;

        // Smooth the logical target position to reduce micro-jitter
        Vector3 targetPos = target.position;
        if (smoothTargetPosition)
            _smoothedTargetPos = Vector3.SmoothDamp(_smoothedTargetPos, targetPos, ref _targetPosVel, targetSmoothTime, Mathf.Infinity, dt);
        else
            _smoothedTargetPos = targetPos;

        // Smooth yaw towards target
        _currentYaw = Mathf.SmoothDampAngle(_currentYaw, _targetYaw, ref _yawVelocity, yawSmoothTime, Mathf.Infinity, dt);

        // Compose rotation (constant pitch, smoothed yaw)
        Quaternion rot = Quaternion.Euler(pitch, _currentYaw, 0f);

        // Smooth X-sign flip if enabled
        if (autoFlipXWithYaw)
            _currentXSign = Mathf.SmoothDamp(_currentXSign, _targetXSign, ref _xSignVel, xFlipSmoothTime, Mathf.Infinity, dt);
        else
            _currentXSign = 1f;

        // Apply X-sign to the offset direction before rotation
        Vector3 effectiveOffset = new Vector3(offset.x * _currentXSign, offset.y, offset.z);

        // Direction from target to camera (normalize only, distance handled separately)
        Vector3 offsetDir = (rot * effectiveOffset).normalized;
        if (offsetDir.sqrMagnitude < 1e-6f) offsetDir = rot * Vector3.back;

        // Smooth distance towards target distance
        distance = Mathf.Clamp(distance, minDistance, maxDistance);
        _currentDistance = Mathf.SmoothDamp(_currentDistance, distance, ref _distanceVel, distanceSmoothTime, Mathf.Infinity, dt);

        // Desired camera position
        Vector3 desiredPos = _smoothedTargetPos + offsetDir * _currentDistance;

        // Smooth position follow
        transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref _posVelocity, positionSmoothTime, Mathf.Infinity, dt);

        // Apply orientation (keeps constant pitch)
        transform.rotation = rot;
    }

    /// <summary>Instantly place the camera on the target using current yaw/distance. No smoothing.</summary>
    public void SnapImmediate()
    {
        if (target == null) return;

        // Reset all velocities to prevent drift after snap
        _yawVelocity = 0f;
        _distanceVel = 0f;
        _posVelocity = Vector3.zero;
        _targetPosVel = Vector3.zero;

        // Ensure current state matches target state
        _currentYaw = _targetYaw;
        _currentDistance = Mathf.Clamp(distance, minDistance, maxDistance);
        if (autoFlipXWithYaw) { _currentXSign = _targetXSign; _xSignVel = 0f; } else { _currentXSign = 1f; }

        // Use current *actual* target position (no smoothing) for the snap
        Vector3 anchor = target.position;
        Quaternion rot = Quaternion.Euler(pitch, _currentYaw, 0f);
        Vector3 effectiveOffset = new Vector3(offset.x * _currentXSign, offset.y, offset.z);
        Vector3 offsetDir = (rot * effectiveOffset).normalized;
        if (offsetDir.sqrMagnitude < 1e-6f) offsetDir = rot * Vector3.back;

        transform.position = anchor + offsetDir * _currentDistance;
        transform.rotation  = rot;

        // Initialize smoothed target position to current anchor after snapping
        _smoothedTargetPos = anchor;
    }

    /// <summary>Set the camera's target yaw in degrees (smoothly).</summary>
    public void SetYaw(float yawDegrees)
    {
        _targetYaw = yawDegrees;
        if (autoFlipXWithYaw)
            _targetXSign = (Mathf.Sign(_targetYaw) >= 0f ? 1f : -1f);
    }

    /// <summary>Flip the target yaw around 0 (e.g., 15 -> -15) and mirror X offset.</summary>
    public void FlipYaw()
    {
        _targetYaw = -_targetYaw;
        if (autoFlipXWithYaw)
            _targetXSign = -_targetXSign;
    }

    /// <summary>Set a new target distance (will smoothly interpolate).</summary>
    public void SetDistance(float newDistance)
    {
        distance = Mathf.Clamp(newDistance, minDistance, maxDistance);
    }
}
