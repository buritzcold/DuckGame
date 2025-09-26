using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class PlayerScript : MonoBehaviour
{
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

    private float beakAngleDeg;
    private Rigidbody rb;
    private Collider col;
    private bool isGrounded;
    private bool jumpQueued;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();

        rb.constraints = RigidbodyConstraints.FreezeRotation
                       | RigidbodyConstraints.FreezePositionZ;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        if (body != null && beak != null)
        {
            Vector3 from = beak.position - body.position;
            from.z = 0f;
            if (from.sqrMagnitude > 1e-6f)
                beakAngleDeg = Mathf.Atan2(from.y, from.x) * Mathf.Rad2Deg;

            if (!beakPivotAtBase && beakHalfLength <= 0.001f)
            {
                var r = beak.GetComponentInChildren<Renderer>();
                if (r != null)
                    beakHalfLength = Mathf.Max(0.05f, r.bounds.extents.x);
                else
                    beakHalfLength = 0.5f;
            }
        }
    }

    void Update()
    {
        isGrounded = ComputeGrounded();

        if (body != null && beak != null && Keyboard.current != null)
        {
            float dir = 0f;
            if (Keyboard.current.jKey.isPressed) dir += 1f; // CCW
            if (Keyboard.current.lKey.isPressed) dir -= 1f; // CW

            if (dir != 0f)
            {
                beakAngleDeg += dir * beakAngularSpeed * Time.deltaTime;
                if (beakAngleDeg > 180f) beakAngleDeg -= 360f;
                if (beakAngleDeg < -180f) beakAngleDeg += 360f;
            }

            float rad = beakAngleDeg * Mathf.Deg2Rad;
            Vector3 dirXY = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f);

            Vector3 baseWorld = body.position + new Vector3(0f, 0f, beakDepthOffset);

            if (beakPivotAtBase)
            {
                beak.position = baseWorld;
                beak.rotation = Quaternion.Euler(0f, 0f, beakAngleDeg + beakVisualOffsetDeg);
            }
            else
            {
                beak.position = baseWorld + dirXY * beakHalfLength;
                beak.rotation = Quaternion.Euler(0f, 0f, beakAngleDeg + beakVisualOffsetDeg);
            }
        }
    }

    void FixedUpdate()
    {
        // Horizontal input (A/D or arrows + gamepad)
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
        v.z = 0f; // lock out forward/back
        rb.linearVelocity = v;
    }

    private bool ComputeGrounded()
    {
        Bounds b = col.bounds;
        Vector3 top = new Vector3(b.center.x, b.min.y + groundCheckRadius + 0.01f, b.center.z);
        Vector3 bottom = new Vector3(b.center.x, b.min.y - groundCheckPadding, b.center.z);
        return Physics.CheckCapsule(top, bottom, groundCheckRadius, groundMask, QueryTriggerInteraction.Ignore);
    }
}
