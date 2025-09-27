using UnityEngine;

public class Enemy : MonoBehaviour
{
    [Header("Damage Settings")]
    public int DamageValue = 1;

    [Tooltip("Layers that should take damage")]
    public LayerMask damageLayers;

    void OnCollisionEnter(Collision collision)
    {
        foreach (var contact in collision.contacts)
        {
            // Collider on the player that got hit
            Collider hit = contact.otherCollider;
            int hitLayer = hit.gameObject.layer;

            // If that collider's layer is in our damage mask
            if ((damageLayers.value & (1 << hitLayer)) != 0)
            {
                PlayerScript.Instance.DamagePlayer(DamageValue);
                break; // stop after first valid hit
            }
        }
    }
}
