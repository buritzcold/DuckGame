using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class FlyingEnemy : MonoBehaviour
{
    [Header("Chase Settings")]
    public float speed = 5f;
    public float chaseDuration = 5f;

    [Header("Detection")]
    public SphereCollider detectionZone;  // Should be trigger
    public string playerTag = "Player";

    private Rigidbody rb;
    private Transform targetPlayer;
    private float chaseTimer = 0f;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ;
    }

    void FixedUpdate()
    {
        if (targetPlayer != null)
        {
            chaseTimer -= Time.fixedDeltaTime;
            if (chaseTimer <= 0f)
            {
                targetPlayer = null;
                rb.linearVelocity = Vector3.zero;
                return;
            }

            Vector3 direction = (targetPlayer.position - transform.position).normalized;
            rb.linearVelocity = direction * speed;
        }
        else
        {
            rb.linearVelocity = Vector3.zero;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (targetPlayer == null && other.CompareTag(playerTag))
        {
            targetPlayer = other.transform;
            chaseTimer = chaseDuration;
        }
    }

    void OnTriggerStay(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            // Refresh the chase timer while player is within range
            chaseTimer = chaseDuration;
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(playerTag) && other.transform == targetPlayer)
        {
            // Start countdown to stop chasing
            chaseTimer = chaseDuration;
        }
    }
}
