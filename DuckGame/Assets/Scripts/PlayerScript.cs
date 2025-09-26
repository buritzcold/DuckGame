using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class PlayerScript : MonoBehaviour
{
    [Header("Health")]
    public int FullHealth = 3;
    public int currentHealth;
    public HealthUI healthUI;
    public GameObject playerBody;
    
    [Header("Movement")]
    public float moveSpeed = 6f;

    [Header("Grounding")]
    public LayerMask groundMask = ~0;
    public float groundCheckPadding = 0.05f;
    public float groundCheckRadius = 0.2f;

    [Header("Beak Orbit")]
    public Transform body;
    public Transform beak;
    public float beakAngularSpeed = 270f;
    public bool beakPivotAtBase = false;
    public float beakHalfLength = 2.5f;
    public float beakDepthOffset = 0f;
    public float beakVisualOffsetDeg = 0f;

    [Header("Beak Tip Anchor")]
    public Transform beakTip;
    public float tipProbeRadius = 0.15f;
    public LayerMask anchorableMask;
    public float repositionSmoothing = 1f;

    private float beakAngleDeg;
    private Rigidbody rb;
    private Collider col;
    private bool isGrounded;

    // Anchor state
    private bool isAnchored;
    private Vector3 anchorPointWorld;
    
    public static PlayerScript Instance { get; private set; }
    [SerializeField]
    private bool _persistent = true;

    void Awake()
    {
        // Check if an instance already exists
        if (Instance != null && Instance != this)
        {
            // If another instance exists, destroy this one
            Destroy(gameObject);
        }
        else
        {
            // Set this instance as the singleton
            Instance = this;

            // If set to persistent, prevent destruction on scene loads
            if (_persistent)
            {
                DontDestroyOnLoad(gameObject);
            }
        }
        
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();

        rb.constraints = RigidbodyConstraints.FreezeRotation
                       | RigidbodyConstraints.FreezePositionZ;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // Initialize beak angle and half-length if needed
        if (body != null && beak != null)
        {
            Vector3 from = beak.position - body.position;
            from.z = 0f;
            if (from.sqrMagnitude > 1e-6f)
                beakAngleDeg = Mathf.Atan2(from.y, from.x) * Mathf.Rad2Deg;

            if (!beakPivotAtBase && beakHalfLength <= 0.001f)
            {
                var r = beak.GetComponentInChildren<Renderer>();
                if (r != null) beakHalfLength = Mathf.Max(0.05f, r.bounds.extents.x);
                else beakHalfLength = 0.5f;
            }
        }

        // Make the beak & tip ignore collisions with the player's main collider
        if (col != null)
        {
            foreach (var c in GetComponentsInChildren<Collider>(includeInactive: true))
            {
                if (c == col) continue;
                Physics.IgnoreCollision(col, c, true);
            }
        }
    }
    
    void Start()
    {
        currentHealth = FullHealth;
        healthUI.UpdateHealth(currentHealth);
    }

    public void DamagePlayer(int damage = 1)
    {
        currentHealth -= damage;
        healthUI.UpdateHealth(currentHealth);

        if (currentHealth <= 0)
        {
            enabled = false;
            playerBody.SetActive(false);
        }
    }

    void Update()
    {
        isGrounded = ComputeGrounded();

        // Handle anchor input (Space)
        HandleAnchorInput();

        // Beak rotation input (J/L)
        float rotDir = 0f;
        if (Keyboard.current != null)
        {
            if (Keyboard.current.jKey.isPressed) rotDir += 1f; // CCW
            if (Keyboard.current.lKey.isPressed) rotDir -= 1f; // CW
        }
        if (rotDir != 0f)
        {
            beakAngleDeg += rotDir * beakAngularSpeed * Time.deltaTime;
            if (beakAngleDeg > 180f) beakAngleDeg -= 360f;
            if (beakAngleDeg < -180f) beakAngleDeg += 360f;
        }

        // Update beak position/rotation (and possibly move the player if anchored)
        UpdateBeakAndPossiblyAnchor();
    }

    void FixedUpdate()
    {
        // If anchored, we don't apply our normal X motion; the body positioning is handled in UpdateBeakAndPossiblyAnchor
        if (isAnchored) return;

        float x = 0f;
        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)  x -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) x += 1f;
        }
        if (Gamepad.current != null) x += Gamepad.current.leftStick.ReadValue().x;
        x = Mathf.Clamp(x, -1f, 1f);

        Vector3 v = rb.linearVelocity;
        v.x = x * moveSpeed;
        v.z = 0f;
        rb.linearVelocity = v;
    }

    private void HandleAnchorInput()
    {
        if (Keyboard.current == null) return;

        // Press to engage anchor
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            TryAcquireAnchor();
        }

        // Release to disengage
        if (Keyboard.current.spaceKey.wasReleasedThisFrame)
        {
            isAnchored = false;
        }
    }

    private void TryAcquireAnchor()
    {
        if (beakTip == null) return;

        Vector3 tipPos = beakTip.position;

        // Search for nearest collider in anchorableMask
        Collider[] hits = Physics.OverlapSphere(tipPos, tipProbeRadius, anchorableMask, QueryTriggerInteraction.Ignore);
        if (hits != null && hits.Length > 0)
        {
            Collider nearest = null;
            float bestDist = float.MaxValue;

            foreach (var h in hits)
            {
                Vector3 p = h.ClosestPoint(tipPos);
                float d = (p - tipPos).sqrMagnitude;
                if (d < bestDist)
                {
                    bestDist = d;
                    nearest = h;
                    anchorPointWorld = p;
                }
            }

            if (nearest != null)
            {
                isAnchored = true;
                return;
            }
        }

        isAnchored = false;
    }

private void UpdateBeakAndPossiblyAnchor()
{
    if (body == null || beak == null || beakTip == null) return;

    float rad = beakAngleDeg * Mathf.Deg2Rad;
    Vector3 dirXY = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f);

    Quaternion beakRot = Quaternion.Euler(0f, 0f, beakAngleDeg + beakVisualOffsetDeg);

    if (isAnchored)
    {
        // Get beak tip offset in world space
        Vector3 tipOffset = Vector3.Scale(beakTip.localPosition, beak.lossyScale);
        Vector3 rotatedOffset = beakRot * tipOffset;

        // Position the beak so that its tip ends up at anchor point
        Vector3 desiredBeakPos = anchorPointWorld - rotatedOffset;
        beak.position = desiredBeakPos;
        beak.rotation = beakRot;

        // Now position the body so that the beak base is where it should be relative to body
        Vector3 baseOffset = beakPivotAtBase ? Vector3.zero : dirXY * beakHalfLength;
        Vector3 targetBodyPos = beak.position - baseOffset;
        targetBodyPos.z -= beakDepthOffset;

        Vector3 delta = targetBodyPos - body.position;
        rb.MovePosition(rb.position + delta);
    }
    else
    {
        // Free movement, orbit around body
        Vector3 baseWorld = body.position + new Vector3(0f, 0f, beakDepthOffset);
        beak.rotation = beakRot;

        if (beakPivotAtBase)
            beak.position = baseWorld;
        else
            beak.position = baseWorld + dirXY * beakHalfLength;
    }
}



    private bool ComputeGrounded()
    {
        Bounds b = col.bounds;
        Vector3 top = new Vector3(b.center.x, b.min.y + groundCheckRadius + 0.01f, b.center.z);
        Vector3 bottom = new Vector3(b.center.x, b.min.y - groundCheckPadding, b.center.z);
        return Physics.CheckCapsule(top, bottom, groundCheckRadius, groundMask, QueryTriggerInteraction.Ignore);
    }

#if UNITY_EDITOR
void OnDrawGizmosSelected()
{
    if (beakTip != null)
    {
        Gizmos.color = isAnchored ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(beakTip.position, tipProbeRadius);
    }
    if (isAnchored)
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(anchorPointWorld, 0.04f);
    }
}
#endif
}
