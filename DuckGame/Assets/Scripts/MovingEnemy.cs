using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class MovingEnemy : MonoBehaviour
{
    [Header("Movement")]
    public float normalSpeed = 2f;
    public float chaseSpeed = 4f;

    [Header("Detection")]
    public Collider detectionZone; // Looks around itself
    public GroundCheck leftCheck; // these might be backwards since we are facing +z
    public GroundCheck rightCheck;

    private bool playerInSight = false;
    private bool facingRight = false;
    private Vector3 moveDirection = Vector3.right;

    private Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ;
    }

    void FixedUpdate()
    {
        float speed = playerInSight ? chaseSpeed : normalSpeed;

        // Move in current direction
        rb.linearVelocity = new Vector3(moveDirection.x * speed, rb.linearVelocity.y, 0f);

        // If wall (2 colliders, ground + other ground) or air detected in current direction, flip
        if ((facingRight && rightCheck.ShouldTurnAround()) ||
            (!facingRight && leftCheck.ShouldTurnAround()))
        {
            Flip();
        }
    }

    void Flip()
{
    facingRight = !facingRight;
    moveDirection = facingRight ? Vector3.right : Vector3.left;

    // Flip visuals
    Transform visuals = transform.Find("Visuals");
    if (visuals != null)
    {
        Vector3 scale = visuals.localScale;
        scale.x = Mathf.Abs(scale.x) * (facingRight ? 1 : -1);
        visuals.localScale = scale;
    }

    // Flip detection zone offset (also at runtime)
    if (detectionZone != null)
    {
        Vector3 localPos = detectionZone.transform.localPosition;
        localPos.x = -localPos.x;
        detectionZone.transform.localPosition = localPos;
    }
}


    // checks for objects tagged player, moves faster towards those
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            playerInSight = true;
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
            playerInSight = false;
    }
}
