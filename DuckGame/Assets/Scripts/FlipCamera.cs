using UnityEngine;

[RequireComponent(typeof(Collider))]
public class FlipCameraZone : MonoBehaviour
{
    [Header("When Player ENTERS this trigger")]
    public bool applyOnEnter = true;
    [Tooltip("Yaw to set on enter (e.g., -15 to mirror a +15 default).")]
    public float yawOnEnter = -15f;

    [Header("When Player EXITS this trigger")]
    public bool applyOnExit = false;
    [Tooltip("Yaw to set on exit (e.g., +15 to restore original).")]
    public float yawOnExit = 15f;

    private Collider _col;

    void Reset()
    {
        _col = GetComponent<Collider>();
        if (_col != null) _col.isTrigger = true;
    }

    void Awake()
    {
        _col = GetComponent<Collider>();
        if (_col != null && !_col.isTrigger)
        {
            _col.isTrigger = true; // ensure trigger
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (CameraFollowFlip.Instance == null) return;

        if (applyOnEnter)
        {
            CameraFollowFlip.Instance.SetYaw(yawOnEnter);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (CameraFollowFlip.Instance == null) return;

        if (applyOnExit)
        {
            CameraFollowFlip.Instance.SetYaw(yawOnExit);
        }
    }
}
