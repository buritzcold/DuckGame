using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class PlayerController : MonoBehaviour
{
    [Header("Health")]
    public int FullHealth = 3;
    public int currentHealth;
    public HealthUI healthUI;

    [Header("Movement")]
    public float moveSpeed = 6f;

    [Header("Beak Rotation")]
    public Transform head;           // Pivot point for rotation
    public Transform beak;           // Beak object (elongated capsule collider)
    public Transform beakTip;        // End of the beak (attach point)
    public float beakAngularSpeed = 140f; // moving this above ~150 breaks beak related physics
    public float vaultForce = 12f;

    [Header("Beak Collision (Additional Anti-Clip)")]
    [Tooltip("Layer(s) considered ground/solid for the beak casts.")]
    public LayerMask groundMask;
    [Tooltip("Approximated radius of the beak shaft capsule for collision tests.")]
    public float beakShaftRadius = 0.1f;
    [Tooltip("Approximated radius of the beak tip sphere for collision tests.")]
    public float beakTipRadius = 0.1f;
    [Tooltip("Max angle per sub-step when sweeping rotation. Smaller = sturdier but a bit more CPU.")]
    [Range(0.5f, 10f)] public float maxRotationStepDeg = 3f;
    [Tooltip("Small offset to keep the beak just out of geometry on contact.")]
    public float surfaceEpsilon = 0.002f;

    [Header("Pickup / Drop")]
    [Tooltip("Layers that can be picked up (e.g., only the 'Item' layer).")]
    public LayerMask itemMask;
    [Tooltip("Radius around beak tip to search for an item to pick up.")]
    public float grabRadius = 0.2f;

    [Header("Held Item Collision")]
    [Tooltip("Optional: name of a layer that collides with nothing while item is held (configure in Physics settings). Leave empty to skip.")]
    public string heldItemLayerName = "HeldItem";
    [Tooltip("While held, set all item colliders to isTrigger so they pass through world but still fire triggers.")]
    public bool setHeldCollidersAsTrigger = true;

    [Header("Gravity")]
    [Tooltip("Custom Gravity Scale.")]
    public bool useCustomGravity = true;
    [Tooltip("Downward gravity strength (m/s^2).")]
    [Range(0f, 60f)] public float gravity = 14f;

    // --- Anti-wall-stick (horizontal sweep) ---
    [Header("Anti-Wall-Stick")]
    [Tooltip("Small gap to keep off walls.")]
    public float wallSkin = 0.01f;
    [Tooltip("Extra radius safety for the side sweep.")]
    public float wallSweepPadding = 0.005f;

    // ---------------- AUDIO ----------------
    [Header("Audio")]
    [Tooltip("Optional dedicated source for SFX (2D recommended). If none, one will be added.")]
    public AudioSource sfxSource;
    [Tooltip("Optional dedicated source for background music (2D, loop). If none, one will be added.")]
    public AudioSource musicSource;

    [Tooltip("Played once when an item is successfully grabbed.")]
    public AudioClip grabSfx;
    [Range(0f, 1f)] public float grabSfxVolume = 1f;

    [Tooltip("Played when the player takes damage.")]
    public AudioClip damageSfx;
    [Range(0f, 1f)] public float damageSfxVolume = 1f;

    [Tooltip("Looping background music.")]
    public AudioClip musicLoop;
    [Range(0f, 1f)] public float musicVolume = 0.5f;

    // ---------------- FACING / BODY FLIP ----------------
    [Header("Facing / Body Flip")]
    [Tooltip("Child transform that visually represents the duck's body (will rotate/offset smoothly).")]
    public Transform body;
    [Tooltip("Local Y rotation when facing RIGHT.")]
    public float bodyYawRight = 0f;
    [Tooltip("Local Y rotation when facing LEFT.")]
    public float bodyYawLeft = 180f;
    [Tooltip("Target local X when facing RIGHT (e.g., 2.1).")]
    public float bodyXRight = 2.1f;
    [Tooltip("Target local X when facing LEFT (e.g., 0.9).")]
    public float bodyXLeft = 0.9f;
    [Tooltip("Seconds to smooth the body yaw flip.")]
    public float bodyYawSmoothTime = 0.08f;
    [Tooltip("Seconds to smooth the body X offset change.")]
    public float bodyXSmoothTime = 0.08f;

    private bool _facingRight = true;
    private float _targetBodyYaw;
    private float _currentBodyYaw;
    private float _bodyYawVel;
    private float _targetBodyX;
    private float _currentBodyX;
    private float _bodyXVel;

    private Rigidbody rb;

    // --- Held item state ---
    private Rigidbody heldItemRB;
    private int heldItemOriginalLayer = -1;
    private RigidbodyInterpolation heldItemPrevInterpolation = RigidbodyInterpolation.None;
    private Vector3 heldItemOriginalLocalScale = Vector3.one;

    // Track original collider trigger flags for restoration
    private readonly List<(Collider col, bool wasTrigger)> heldItemColliderFlags = new();

    // Optional: we still ignore collisions with player colliders (harmless even when triggers)
    private readonly List<(Collider itemCol, Collider playerCol)> ignoredPairs = new();
    private Collider[] playerColliders;

    // Visual flag only (for gizmo color)
    private bool isAnchored;

    private Vector3 spawnPoint;

    // Socket that cancels parent scale so held items don't get stretched
    private Transform holdSocket;

    // --- Cached refs & input (child colliders) ---
    private Collider[] _cols;
    private float _moveInput;

    // Singleton stuff
    public static PlayerController Instance { get; private set; }
    [SerializeField] private bool _persistent = true;

    void Awake()
    {
        // Singleton
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        else
        {
            Instance = this;
            if (_persistent)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        rb = GetComponent<Rigidbody>();
        playerColliders = GetComponentsInChildren<Collider>(includeInactive: false);
        _cols = GetComponentsInChildren<Collider>(includeInactive: false);

        // lock to XY plane
        rb.constraints = RigidbodyConstraints.FreezeRotationX |
                         RigidbodyConstraints.FreezeRotationY |
                         RigidbodyConstraints.FreezeRotationZ |
                         RigidbodyConstraints.FreezePositionZ;

        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        // Toggle Unity gravity based on slider flag
        rb.useGravity = !useCustomGravity;

        // Set up audio sources safely
        EnsureAudioSources();

        // Start music once (persists because of singleton)
        TryStartMusic();

        // Subscribe to scene load events
        SceneManager.sceneLoaded += OnSceneLoaded;

        // ---- Initialize body flip state ----
        if (body != null)
        {
            // Try to infer facing from current local Y
            float y = body.localEulerAngles.y;
            _facingRight = Mathf.DeltaAngle(y, bodyYawRight) * Mathf.DeltaAngle(y, bodyYawRight) <
                           Mathf.DeltaAngle(y, bodyYawLeft) * Mathf.DeltaAngle(y, bodyYawLeft);

            _targetBodyYaw = _currentBodyYaw = _facingRight ? bodyYawRight : bodyYawLeft;

            float startX = body.localPosition.x;
            // If starting close to one of the targets, use it; else pick by facing.
            if (Mathf.Abs(startX - bodyXRight) < Mathf.Abs(startX - bodyXLeft))
            {
                _targetBodyX = _currentBodyX = bodyXRight;
            }
            else
            {
                _targetBodyX = _currentBodyX = bodyXLeft;
            }

            // Snap body to the initial targets to avoid first-frame drift.
            var lp = body.localPosition; lp.x = _currentBodyX; body.localPosition = lp;
            var lr = body.localEulerAngles; lr.y = _currentBodyYaw; body.localEulerAngles = lr;
        }
    }

    void OnEnable()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (rb != null) rb.useGravity = !useCustomGravity;
    }

    void OnValidate()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (rb != null) rb.useGravity = !useCustomGravity;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void Start()
    {
        spawnPoint = transform.position;
        ResetPlayer();
    }

    private void ResetPlayer()
    {
        currentHealth = FullHealth;
        if (healthUI != null) healthUI.UpdateHealth(currentHealth);
        rb.position = spawnPoint;
        rb.linearVelocity = Vector3.zero;

        if (heldItemRB != null) DropHeldItem();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResetPlayer();
        // Music already running due to singleton; avoid restarting
        TryStartMusic();
    }

    public void DamagePlayer(int damage = 1)
    {
        if (damage > 0)
        {
            PlaySfx(damageSfx, damageSfxVolume);
        }

        currentHealth -= damage;
        if (healthUI != null) healthUI.UpdateHealth(currentHealth);

        if (currentHealth <= 0)
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }

    void Update()
    {
        HandleMovement();
        HandleFacingInput();     // << add: flips on A/D
        UpdateBodyFlipSmoothing();
        HandleBeakRotation();
        HandlePickupDropInput();
    }

    void FixedUpdate()
    {
        ApplyHorizontalSweepMove();

        if (useCustomGravity)
        {
            rb.AddForce(Vector3.down * gravity, ForceMode.Acceleration);
        }
    }

    void LateUpdate()
    {
        if (holdSocket != null && beakTip != null)
        {
            ApplyInverseScaleToHoldSocket();
        }
    }

    // ---------------- MOVEMENT ----------------
    void HandleMovement()
    {
        _moveInput = 0f;
        if (Keyboard.current.aKey.isPressed) _moveInput = -1f;
        if (Keyboard.current.dKey.isPressed) _moveInput =  1f;
    }

    // ---------------- FACING INPUT & SMOOTHING ----------------
    void HandleFacingInput()
    {
        // Flip when A/D are PRESSED (not just held)
        if (Keyboard.current.aKey.wasPressedThisFrame) SetFacingRight(false);
        if (Keyboard.current.dKey.wasPressedThisFrame) SetFacingRight(true);

        // Optional: if you want auto-flip when holding a direction (uncomment):
        // if (_moveInput < -0.2f) SetFacingRight(false);
        // if (_moveInput >  0.2f) SetFacingRight(true);
    }

    public void SetFacingRight(bool right)
    {
        if (body == null) return;
        if (_facingRight == right) return;

        _facingRight = right;
        _targetBodyYaw = right ? bodyYawRight : bodyYawLeft;
        _targetBodyX   = right ? bodyXRight   : bodyXLeft;
    }

    void UpdateBodyFlipSmoothing()
    {
        if (body == null) return;

        // Smooth rotation (Y only)
        _currentBodyYaw = Mathf.SmoothDampAngle(_currentBodyYaw, _targetBodyYaw, ref _bodyYawVel, Mathf.Max(0.0001f, bodyYawSmoothTime));
        var e = body.localEulerAngles;
        e.y = _currentBodyYaw;
        body.localEulerAngles = e;

        // Smooth local X
        _currentBodyX = Mathf.SmoothDamp(_currentBodyX, _targetBodyX, ref _bodyXVel, Mathf.Max(0.0001f, bodyXSmoothTime));
        var lp = body.localPosition;
        lp.x = _currentBodyX;
        body.localPosition = lp;
    }

    void ApplyHorizontalSweepMove()
    {
        float desiredDx = _moveInput * moveSpeed * Time.fixedDeltaTime;

        if (Mathf.Approximately(desiredDx, 0f))
        {
            var v0 = rb.linearVelocity;
            v0.x = 0f;
            rb.linearVelocity = v0;
            return;
        }

        if (_cols == null || _cols.Length == 0)
        {
            var v = rb.linearVelocity;
            v.x = (_moveInput * moveSpeed);
            rb.linearVelocity = v;
            return;
        }

        float dirSign = Mathf.Sign(desiredDx);
        Vector3 dir = dirSign > 0f ? Vector3.right : Vector3.left;
        float targetDist = Mathf.Abs(desiredDx) + Mathf.Max(0f, wallSkin);
        float allowedDist = targetDist;

        for (int i = 0; i < _cols.Length; i++)
        {
            var c = _cols[i];
            if (c == null || !c.enabled || c.isTrigger) continue;

            Bounds b = c.bounds;
            float insetY = Mathf.Min(0.05f, b.extents.y * 0.25f);
            Vector3 top = new Vector3(b.center.x, b.max.y - insetY, b.center.z);
            Vector3 bottom = new Vector3(b.center.x, b.min.y + insetY, b.center.z);

            float radius = Mathf.Max(
                0.02f,
                Mathf.Min(b.extents.x, b.extents.y) - Mathf.Max(0f, wallSweepPadding)
            );

            if (Physics.CapsuleCast(
                    bottom, top, radius, dir,
                    out RaycastHit hit, allowedDist,
                    groundMask, QueryTriggerInteraction.Ignore))
            {
                float thisAllowed = Mathf.Max(0f, hit.distance - wallSkin);
                if (thisAllowed < allowedDist) allowedDist = thisAllowed;
                if (allowedDist <= 1e-6f) break;
            }
        }

        float clampedDx = dirSign * allowedDist;
        var vFinal = rb.linearVelocity;
        vFinal.x = clampedDx / Mathf.Max(Time.fixedDeltaTime, 1e-6f);
        rb.linearVelocity = vFinal;
    }

    // ---------------- BEAK ROTATION ----------------
    void HandleBeakRotation()
    {
        if (head == null || beak == null || beakTip == null) return;

        float rotateInput = 0f;
        if (Keyboard.current.jKey.isPressed) rotateInput = 1f;  // CCW
        if (Keyboard.current.lKey.isPressed) rotateInput = -1f; // CW
        if (Mathf.Approximately(rotateInput, 0f)) return;

        float desiredDelta = rotateInput * beakAngularSpeed * Time.deltaTime;

        bool tipTouchingGround = Physics.CheckSphere(
            beakTip.position,
            Mathf.Max(0.05f, beakTipRadius * 0.8f),
            groundMask
        );

        if (tipTouchingGround && !isAnchored)
        {
            Vector3 dir = (beakTip.position - head.position).normalized;
            Vector3 tangent = Vector3.Cross(Vector3.forward, dir) * rotateInput;
            rb.AddForce(tangent * vaultForce, ForceMode.Acceleration);
            SweptRotateBeak(desiredDelta, allowPenetratingDirection: false);
        }
        else
        {
            SweptRotateBeak(desiredDelta, allowPenetratingDirection: false);
        }
    }

    void SweptRotateBeak(float totalDeltaDeg, bool allowPenetratingDirection)
    {
        if (Mathf.Approximately(totalDeltaDeg, 0f)) return;

        int steps = Mathf.CeilToInt(Mathf.Abs(totalDeltaDeg) / Mathf.Max(0.5f, maxRotationStepDeg));
        float stepDeg = totalDeltaDeg / steps;

        for (int i = 0; i < steps; i++)
        {
            Vector3 currTip = beakTip.position;
            Vector3 nextTip = RotatePointAroundPivot(currTip, head.position, new Vector3(0f, 0f, stepDeg));

            // 1) Tip sweep
            Vector3 move = nextTip - currTip;
            float moveDist = move.magnitude;
            bool blocked = false;
            float allowedFrac = 1f;
            RaycastHit hitInfo;

            if (moveDist > 1e-5f)
            {
                if (Physics.SphereCast(currTip, beakTipRadius, move.normalized, out hitInfo, moveDist, groundMask, QueryTriggerInteraction.Ignore))
                {
                    blocked = true;
                    allowedFrac = Mathf.Clamp01((hitInfo.distance - surfaceEpsilon) / Mathf.Max(moveDist, 1e-5f));
                }
            }

            // 2) Shaft sweep
            Vector3 currBase = head.position;
            float shaftCheckFrac = allowedFrac;

            if (!blocked || allowedFrac > 0f)
            {
                Vector3 partialNextTip = currTip + move * shaftCheckFrac;
                Vector3 p0 = currBase;
                Vector3 p1 = partialNextTip;
                Collider[] overlaps = Physics.OverlapCapsule(p0, p1, beakShaftRadius, groundMask, QueryTriggerInteraction.Ignore);
                if (overlaps != null && overlaps.Length > 0)
                {
                    blocked = true;
                    shaftCheckFrac = Mathf.Max(0f, shaftCheckFrac - 0.05f);
                }
            }

            float finalFrac = blocked ? Mathf.Min(allowedFrac, shaftCheckFrac) : 1f;
            if (finalFrac <= 0f) break;

            float applyDeg = stepDeg * finalFrac;
            beak.RotateAround(head.position, Vector3.forward, applyDeg);

            if (blocked) break;
        }
    }

    static Vector3 RotatePointAroundPivot(Vector3 point, Vector3 pivot, Vector3 anglesDeg)
    {
        Vector3 dir = point - pivot;
        dir = Quaternion.Euler(anglesDeg) * dir;
        return pivot + dir;
    }

    // ---------------- PICKUP / DROP ----------------
    void HandlePickupDropInput()
    {
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            if (heldItemRB == null)
                TryPickupItem();
        }

        if (Keyboard.current.leftShiftKey.wasPressedThisFrame)
        {
            if (heldItemRB != null)
                DropHeldItem();
        }
    }

    void TryPickupItem()
    {
        if (beakTip == null) return;

        Collider[] hits = Physics.OverlapSphere(beakTip.position, grabRadius, itemMask, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0) return;

        Rigidbody bestRB = null;
        float bestDistSqr = float.MaxValue;

        foreach (var col in hits)
        {
            var rbCandidate = col.attachedRigidbody;
            if (rbCandidate == null) continue;
            if (rbCandidate.isKinematic) continue;

            float d2 = (rbCandidate.worldCenterOfMass - beakTip.position).sqrMagnitude;
            if (d2 < bestDistSqr)
            {
                bestDistSqr = d2;
                bestRB = rbCandidate;
            }
        }

        if (bestRB == null) return;
        AttachHeldItem(bestRB);
    }

    void AttachHeldItem(Rigidbody itemRB)
    {
        heldItemRB = itemRB;
        heldItemOriginalLayer = itemRB.gameObject.layer;
        heldItemPrevInterpolation = itemRB.interpolation;
        heldItemOriginalLocalScale = itemRB.transform.localScale;

        // Kinematic while held so it doesn't push/pull the player
        heldItemRB.linearVelocity = rb.linearVelocity;
        heldItemRB.angularVelocity = Vector3.zero;
        heldItemRB.isKinematic = true;

        // Disable interpolation to avoid trailing while we manipulate parent transforms in Update
        heldItemRB.interpolation = RigidbodyInterpolation.None;

        // Ensure hold socket exists and cancels parent (beakTip) scale
        EnsureHoldSocket();
        ApplyInverseScaleToHoldSocket();

        // Parent to the socket so it keeps a true shape (no stretching)
        heldItemRB.transform.SetParent(holdSocket, worldPositionStays: false);
        heldItemRB.transform.localPosition = Vector3.zero;
        heldItemRB.transform.localRotation = Quaternion.identity;

        // Keep its visual size by restoring the original local scale (socket neutralizes parent scale)
        heldItemRB.transform.localScale = heldItemOriginalLocalScale;

        // OPTIONAL: ignore collisions with player (harmless even if triggers)
        ignoredPairs.Clear();
        var itemCols = heldItemRB.GetComponentsInChildren<Collider>(includeInactive: false);
        foreach (var ic in itemCols)
        {
            if (!ic.enabled) continue;
            foreach (var pc in playerColliders)
            {
                if (!pc.enabled) continue;
                Physics.IgnoreCollision(ic, pc, true);
                ignoredPairs.Add((ic, pc));
            }
        }

        // Make it ghost through world but still fire triggers (optional)
        heldItemColliderFlags.Clear();
        {
            var itemCols2 = heldItemRB.GetComponentsInChildren<Collider>(includeInactive: false);
            foreach (var c in itemCols2)
            {
                heldItemColliderFlags.Add((c, c.isTrigger));
                if (setHeldCollidersAsTrigger)
                {
                    c.isTrigger = true;
                }
            }
        }

        // Move to a "HeldItem" layer if present
        if (!string.IsNullOrEmpty(heldItemLayerName))
        {
            int layerIdx = LayerMask.NameToLayer(heldItemLayerName);
            if (layerIdx != -1)
            {
                SetLayerRecursively(heldItemRB.gameObject, layerIdx);
            }
            else
            {
                Debug.LogWarning($"[PlayerController] HeldItem layer '{heldItemLayerName}' not found. Skipping layer swap.");
            }
        }

        isAnchored = true;

        // --- Audio: grab sound
        PlaySfx(grabSfx, grabSfxVolume);
    }

    void DropHeldItem()
    {
        if (heldItemRB == null) return;

        foreach (var pair in ignoredPairs)
        {
            if (pair.itemCol != null && pair.playerCol != null)
                Physics.IgnoreCollision(pair.itemCol, pair.playerCol, false);
        }
        ignoredPairs.Clear();

        foreach (var (col, wasTrigger) in heldItemColliderFlags)
        {
            if (col != null) col.isTrigger = wasTrigger;
        }
        heldItemColliderFlags.Clear();

        if (heldItemOriginalLayer >= 0)
            SetLayerRecursively(heldItemRB.gameObject, heldItemOriginalLayer);

        heldItemRB.transform.SetParent(null, worldPositionStays: true);
        heldItemRB.isKinematic = false;

        heldItemRB.interpolation = heldItemPrevInterpolation;

        heldItemRB.linearVelocity = rb.linearVelocity;

        heldItemRB = null;
        heldItemOriginalLayer = -1;
        heldItemPrevInterpolation = RigidbodyInterpolation.None;
        heldItemOriginalLocalScale = Vector3.one;

        isAnchored = false;
    }

    // --- Hold socket helpers ---
    void EnsureHoldSocket()
    {
        if (holdSocket == null)
        {
            var go = new GameObject("HoldSocket");
            holdSocket = go.transform;
        }

        holdSocket.SetParent(beakTip, worldPositionStays: false);
        holdSocket.localPosition = Vector3.zero;
        holdSocket.localRotation = Quaternion.identity;
    }

    void ApplyInverseScaleToHoldSocket()
    {
        Vector3 s = beakTip.lossyScale;
        holdSocket.localScale = new Vector3(
            s.x != 0f ? 1f / s.x : 1f,
            s.y != 0f ? 1f / s.y : 1f,
            s.z != 0f ? 1f / s.z : 1f
        );
    }

    // --- Utils ---
    static void SetLayerRecursively(GameObject go, int layer)
    {
        if (go == null) return;
        go.layer = layer;
        foreach (Transform t in go.transform)
        {
            if (t != null) SetLayerRecursively(t.gameObject, layer);
        }
    }

    void OnDrawGizmosSelected()
    {
        if (beakTip != null)
        {
            Gizmos.color = isAnchored ? Color.green : Color.yellow;
            Gizmos.DrawWireSphere(beakTip.position, grabRadius);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(beakTip.position, beakTipRadius);
        }

        if (head != null && beakTip != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(head.position, beakTip.position);
        }
    }

    // ---------------- AUDIO HELPERS ----------------
    void EnsureAudioSources()
    {
        // SFX
        if (sfxSource == null)
        {
            sfxSource = gameObject.GetComponent<AudioSource>();
            if (sfxSource == null || (musicSource != null && sfxSource == musicSource))
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
            }
            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
            sfxSource.spatialBlend = 0f; // 2D SFX for UI-like feedback
        }

        // Music
        if (musicSource == null)
        {
            // Try not to reuse the SFX source
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.loop = true;
            musicSource.spatialBlend = 0f; // 2D music
            musicSource.ignoreListenerPause = true;
        }
    }

    void TryStartMusic()
    {
        if (musicLoop == null || musicSource == null) return;

        // Only (re)assign if needed; don't restart if it's already playing the same clip
        if (musicSource.clip != musicLoop)
        {
            musicSource.clip = musicLoop;
        }

        musicSource.volume = musicVolume;

        if (!musicSource.isPlaying)
        {
            musicSource.Play();
        }
    }

    void PlaySfx(AudioClip clip, float volume)
    {
        if (clip == null || sfxSource == null) return;
        sfxSource.PlayOneShot(clip, Mathf.Clamp01(volume));
    }
}
