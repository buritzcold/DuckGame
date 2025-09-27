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
    public Transform head;          // Pivot point for rotation
    public Transform beak;          // Beak object
    public Transform beakTip;       // End of the beak
    public float beakAngularSpeed = 180f;
    public float vaultForce = 15f;

    [Header("Grab / Anchor")]
    public LayerMask groundMask;        // Surfaces that can be grabbed
    public float grabRadius = 0.2f;     // Radius around beak tip to search
    public float attachSpring = 2000f;  // Joint spring strength
    public float attachDamper = 200f;   // Joint damping

    private Rigidbody rb;
    private SpringJoint grabJoint;      // more stable than FixedJoint
    private bool isAnchored;

    private Vector3 spawnPoint;
    
    // Singleton stuff
    public static PlayerController Instance { get; private set; }
    [SerializeField]
    private bool _persistent = true;
    
    void Awake()
    {
        // Check if an instance already exists
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

        // lock to XY plane
        rb.constraints = RigidbodyConstraints.FreezeRotationX |
                         RigidbodyConstraints.FreezeRotationY |
                         RigidbodyConstraints.FreezeRotationZ |
                         RigidbodyConstraints.FreezePositionZ;

        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        // Subscribe to scene load events
        SceneManager.sceneLoaded += OnSceneLoaded;
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
        if (healthUI != null)
            healthUI.UpdateHealth(currentHealth);
        
        rb.position = spawnPoint;
        rb.linearVelocity = Vector3.zero;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // After reload, reset back to spawn with full health
        ResetPlayer();
    }
    
    public void DamagePlayer(int damage = 1)
    {
        currentHealth -= damage;
        if (healthUI != null)
            healthUI.UpdateHealth(currentHealth);

        if (currentHealth <= 0)
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
    
    void Update()
    {
        HandleMovement();
        HandleBeakRotation();
        HandleGrab();
    }

    // ---------------- MOVEMENT ----------------
    void HandleMovement()
    {
        float move = 0f;
        if (Keyboard.current.aKey.isPressed) move = -1f;
        if (Keyboard.current.dKey.isPressed) move = 1f;

        Vector3 velocity = rb.linearVelocity;
        velocity.x = move * moveSpeed;
        rb.linearVelocity = velocity;
    }

    // ---------------- BEAK ROTATION ----------------
    void HandleBeakRotation()
    {
        float rotateInput = 0f;
        if (Keyboard.current.jKey.isPressed) rotateInput = 1f;  // CCW
        if (Keyboard.current.lKey.isPressed) rotateInput = -1f; // CW

        if (rotateInput == 0f) return;

        bool tipTouchingGround = Physics.CheckSphere(beakTip.position, 0.1f, groundMask);

        if (tipTouchingGround && !isAnchored)
        {
            // Vault instead of rotating into ground
            Vector3 dir = (beakTip.position - head.position).normalized;
            Vector3 tangent = Vector3.Cross(Vector3.forward, dir) * rotateInput;
            rb.AddForce(tangent * vaultForce, ForceMode.Acceleration);
        }
        else
        {
            // Normal orbit rotation
            beak.RotateAround(head.position, Vector3.forward,
                rotateInput * beakAngularSpeed * Time.deltaTime);
        }
    }

    // ---------------- GRAB ----------------
    void HandleGrab()
    {
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            TryGrab();
        }
        if (Keyboard.current.spaceKey.wasReleasedThisFrame)
        {
            ReleaseGrab();
        }
    }

    void TryGrab()
    {
        if (beakTip == null || grabJoint != null) return;

        Collider[] hits = Physics.OverlapSphere(beakTip.position, grabRadius, groundMask);
        if (hits.Length > 0)
        {
            Collider hit = hits[0];
            Rigidbody hitRb = hit.attachedRigidbody;

            grabJoint = beakTip.gameObject.AddComponent<SpringJoint>();
            grabJoint.autoConfigureConnectedAnchor = false;
            grabJoint.anchor = Vector3.zero;

            if (hitRb != null)
            {
                grabJoint.connectedBody = hitRb;
                grabJoint.connectedAnchor = hitRb.transform.InverseTransformPoint(beakTip.position);
            }
            else
            {
                grabJoint.connectedBody = null; // world anchor
                grabJoint.connectedAnchor = beakTip.position;
            }

            grabJoint.spring = attachSpring;
            grabJoint.damper = attachDamper;
            grabJoint.maxDistance = 0f;

            isAnchored = true;
        }
    }

    void ReleaseGrab()
    {
        if (grabJoint != null)
        {
            Destroy(grabJoint);
            grabJoint = null;
        }
        isAnchored = false;
    }

    void OnDrawGizmosSelected()
    {
        if (beakTip != null)
        {
            Gizmos.color = isAnchored ? Color.green : Color.yellow;
            Gizmos.DrawWireSphere(beakTip.position, grabRadius);
        }
    }
}
