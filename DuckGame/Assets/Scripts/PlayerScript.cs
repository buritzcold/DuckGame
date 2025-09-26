using UnityEngine;
using UnityEngine.InputSystem; // <- NEW input system

[RequireComponent(typeof(Rigidbody))]
public class PlayerScript : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 6f;   // left/right speed
    public float jumpForce = 7f;   // jump impulse

    [Header("Ground Check")]
    public Transform groundCheck;  // empty child at feet
    public float groundRadius = 0.25f;
    public LayerMask groundMask = ~0;

    private Rigidbody rb;
    private bool isGrounded;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();

        // No tipping and no front/back motion
        rb.constraints = RigidbodyConstraints.FreezeRotation
                       | RigidbodyConstraints.FreezePositionZ;
    }

    void Update()
    {
        // Jump input (new Input System)
        bool jumpPressed =
            (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) ||
            (Gamepad.current  != null && Gamepad.current.buttonSouth.wasPressedThisFrame);

        if (isGrounded && jumpPressed)
        {
            // clear vertical speed then jump
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }
    }

    void FixedUpdate()
    {
        // Grounded check
        if (groundCheck != null)
        {
            isGrounded = Physics.CheckSphere(
                groundCheck.position, groundRadius, groundMask, QueryTriggerInteraction.Ignore);
        }

        // Read horizontal with new Input System
        float x = 0f;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)  x -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) x += 1f;
        }

        if (Gamepad.current != null)
        {
            x += Gamepad.current.leftStick.ReadValue().x; // analog add
        }

        x = Mathf.Clamp(x, -1f, 1f);

        // Apply velocity (no forward/back)
        Vector3 v = rb.linearVelocity;
        v.x = x * moveSpeed;
        v.z = 0f; // enforce no Z motion
        rb.linearVelocity = v;
    }

    void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(groundCheck.position, groundRadius);
        }
    }
}
