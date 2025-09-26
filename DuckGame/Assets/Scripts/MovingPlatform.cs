using UnityEngine;

public class MovingPlatform : MonoBehaviour
{
    [Header("Movement Settings")]
    public Vector3 moveDirection = Vector3.right;
    public float moveDistance = 5f;
    public float moveSpeed = 2f;

    private Vector3 startPosition;

    void Start()
    {
        moveDirection = moveDirection.normalized;
        startPosition = transform.position;
    }

    void Update()
    {
        float pingPong = Mathf.PingPong(Time.time * moveSpeed, moveDistance);
        transform.position = startPosition + moveDirection * pingPong;
    }
}
