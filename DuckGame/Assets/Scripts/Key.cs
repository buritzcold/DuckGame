using UnityEngine;

/// If it touches something tagged "Door", that door disappears.
/// Works for both trigger and non-trigger contacts.
/// Also plays a one-shot SFX when breaking the door.
[RequireComponent(typeof(Collider))]
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

    [Header("Audio")]
    [Tooltip("Sound played when the door is broken/disabled.")]
    public AudioClip breakSfx;
    [Range(0f, 1f)] public float breakSfxVolume = 1f;
    [Tooltip("Optional AudioSource to play the break SFX. If missing, one will be added here.")]
    public AudioSource audioSource;
    [Tooltip("0 = 2D, 1 = fully 3D. If you want spatial sound at the key's location, raise this.")]
    [Range(0f, 1f)] public float breakSfxSpatialBlend = 0f;

    void Awake()
    {
        // Ensure an AudioSource exists for the SFX (kept on the key)
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = breakSfxSpatialBlend;
    }

    void OnTriggerEnter(Collider other)     { TryHandle(other.gameObject); }
    void OnCollisionEnter(Collision other)  { TryHandle(other.collider.gameObject); }

    void TryHandle(GameObject hit)
    {
        if (hit == null) return;

        if (!string.IsNullOrEmpty(doorTag) && hit.CompareTag(doorTag))
        {
            // Play SFX from the key BEFORE destroying/disabling the door
            if (breakSfx != null && audioSource != null)
            {
                audioSource.PlayOneShot(breakSfx, Mathf.Clamp01(breakSfxVolume));
            }

            GameObject doorGO = affectDoorRoot ? hit.transform.root.gameObject : hit;

            if (destroyDoor) Destroy(doorGO);
            else doorGO.SetActive(false);
        }
    }
}
