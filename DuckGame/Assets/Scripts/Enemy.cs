using UnityEngine;

public class Enemy : MonoBehaviour
{
    public int DamageValue = 1;
    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            PlayerScript.Instance.DamagePlayer(DamageValue);
        }
    }
}
