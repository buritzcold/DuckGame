using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class DuckController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 6f;

    [Header("Beak Rotation")]
    public Transform head;
    public Transform beak;
    public Transform beakTip;
    public Rigidbody beakTipRb; // assign in Inspector
    public float beakAngularSpeed = 180f;
    public float vaultForce = 15f;

    [Header("Beak Grab")]
    public LayerMask groundMask;
    public float grabRadius = 0.2f;

    private Rigidbody rb;
    private FixedJoint grabJoint;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();

        // lock to XY plane
        rb.constraints = RigidbodyConstraints.FreezeRotationZ |
                         RigidbodyConstraints.FreezeRotationX |
                         RigidbodyConstraints.FreezeRotationY |
                         RigidbodyConstraints.FreezePositionZ;

        // prevent tunneling
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        if (beakTipRb == null && beakTip != null)
        {
            // safety: auto-add if missing
            beakTipRb = beakTip.GetComponent<Rigidbody>();
            if (beakTipRb == null)
            {
                beakTipRb = beakTip.gameObject.AddComponent<Rigidbody>();
                beakTipRb.isKinematic = true;
                beakTipRb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            }
        }
    }

    void Update()
    {
        HandleMovement();
        HandleBeakRotation();
        HandleGrab();
    }

    void HandleMovement()
    {
        float move = 0f;
        if (Keyboard.current.aKey.isPressed) move = -1f;
        if (Keyboard.current.dKey.isPressed) move = 1f;

        Vector3 velocity = new Vector3(move * moveSpeed, rb.linearVelocity.y, 0f);
        rb.linearVelocity = velocity;
    }

    void HandleBeakRotation()
    {
        float rotateInput = 0f;
        if (Keyboard.current.jKey.isPressed) rotateInput = 1f;
        if (Keyboard.current.lKey.isPressed) rotateInput = -1f;

        if (rotateInput == 0f) return;

        bool tipTouchingGround = Physics.CheckSphere(beakTip.position, 0.1f, groundMask);

        if (tipTouchingGround)
        {
            // Instead of rotating into ground, apply tangential vault force
            Vector3 dir = (beakTip.position - head.position).normalized;
            Vector3 tangent = Vector3.Cross(Vector3.forward, dir) * rotateInput; // CW/CCW
            rb.AddForce(tangent * vaultForce, ForceMode.Acceleration);
        }
        else
        {
            // Free rotation
            beak.RotateAround(head.position, Vector3.forward,
                rotateInput * beakAngularSpeed * Time.deltaTime);
        }
    }

    void HandleGrab()
    {
        if (Keyboard.current.spaceKey.isPressed)
        {
            if (grabJoint == null && beakTipRb != null)
            {
                Collider[] hits = Physics.OverlapSphere(beakTip.position, grabRadius, groundMask);
                if (hits.Length > 0)
                {
                    grabJoint = beakTipRb.gameObject.AddComponent<FixedJoint>();
                    grabJoint.connectedBody = null; // pin to world
                }
            }
        }
        else
        {
            if (grabJoint != null)
            {
                Destroy(grabJoint);
                grabJoint = null;
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        if (beakTip != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(beakTip.position, grabRadius);
        }
    }
}
