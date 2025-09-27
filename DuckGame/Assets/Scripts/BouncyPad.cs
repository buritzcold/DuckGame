using UnityEngine;

public class BouncyPad : MonoBehaviour
{
    public float bounceForce = 15f;
    public LayerMask playerLayer;

    private void OnCollisionEnter(Collision collision)
    {
        if (((1 << collision.gameObject.layer) & playerLayer) != 0)
        {
            Rigidbody rb = collision.rigidbody;
            if (rb != null)
            {
                rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, 0f); // Reset vertical velocity
                rb.AddForce(Vector3.up * bounceForce, ForceMode.VelocityChange);
            }
        }
    }
}
