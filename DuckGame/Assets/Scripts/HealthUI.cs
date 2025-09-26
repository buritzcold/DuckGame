using UnityEngine;

public class HealthUI : MonoBehaviour
{
    [SerializeField] private GameObject heartPrefab;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void UpdateHealth(int health)
    {
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }

        for (int i = 0; i < health; i++)
        {
            GameObject heart = Instantiate(heartPrefab, transform.position, Quaternion.identity);
            heart.transform.SetParent(transform);
        }
    }
}
