using UnityEngine;

/// If it touches something tagged "Door", that door disappears.
/// Works for both trigger and non-trigger contacts.
public class Key : MonoBehaviour
{
    [Header("Door detection")]
    [Tooltip("Objects with this tag will be treated as doors.")]
    public string doorTag = "Door";

    [Tooltip("If true, disables the root of the door object; if false, only disables the hit object.")]
    public bool affectDoorRoot = true;

    [Header("What to do")]
    [Tooltip("If true, Destroy; if false, SetActive(false).")]
    public bool destroyDoor = false;

    void OnTriggerEnter(Collider other)     { TryHandle(other.gameObject); }
    void OnCollisionEnter(Collision other)  { TryHandle(other.collider.gameObject); }

    void TryHandle(GameObject hit)
    {
        if (hit == null) return;

        if (!string.IsNullOrEmpty(doorTag) && hit.CompareTag(doorTag))
        {
            GameObject doorGO = affectDoorRoot ? hit.transform.root.gameObject : hit;

            if (destroyDoor) Destroy(doorGO);
            else doorGO.SetActive(false);
        }
    }
}
