using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class DirectionalCameraZone : MonoBehaviour
{
    [Header("Reference")]
    [Tooltip("If null, uses this object's Transform.forward as the reference travel direction.")]
    public Transform referenceDirection;

    [Header("Yaw Settings")]
    [Tooltip("Applied when player moves WITH the reference direction (dot > 0).")]
    public float yawWithRef = -15f;
    [Tooltip("Applied when player moves AGAINST the reference direction (dot < 0).")]
    public float yawAgainstRef = 15f;

    [Header("Detection")]
    [Tooltip("Minimum speed before we trust the direction (units/sec).")]
    public float minSpeed = 0.1f;
    [Tooltip("Cooldown to avoid rapid re-triggers (seconds).")]
    public float retriggerCooldown = 0.3f;

    [Header("Tuning")]
    [SerializeField, Tooltip("Flip the dot test without editing code.")]
    private bool invert = false;

    private Collider _col;
    private readonly Dictionary<Collider, float> _lastApplied = new Dictionary<Collider, float>();
    private readonly Dictionary<Collider, Vector3> _lastPos = new Dictionary<Collider, Vector3>();

    void Reset()
    {
        _col = GetComponent<Collider>();
        if (_col) _col.isTrigger = true;
    }

    void Awake()
    {
        _col = GetComponent<Collider>();
        if (_col && !_col.isTrigger) _col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        _lastPos[other] = other.transform.position;
        TryApply(other); // often works immediately if velocity is available
    }

    void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        // If we couldn't decide on enter (e.g., zero velocity), try again while inside.
        TryApply(other);
        _lastPos[other] = other.transform.position;
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        _lastPos.Remove(other);
        _lastApplied.Remove(other);
    }

    void TryApply(Collider player)
    {
        if (CameraFollowFlip.Instance == null) return;

        float now = Time.time;
        if (_lastApplied.TryGetValue(player, out float t) && now - t < retriggerCooldown)
            return;

        // 1) Get motion direction
        Vector3 vel = Vector3.zero;

        // Prefer Rigidbody velocity if available and meaningful
        var rb = player.attachedRigidbody;
        if (rb != null) vel = rb.linearVelocity; // <-- fixed

        // Fallback: position delta while inside the trigger
        if (vel.sqrMagnitude < minSpeed * minSpeed && _lastPos.TryGetValue(player, out var last))
        {
            Vector3 delta = player.transform.position - last;
            // Ignore out-of-plane noise for 2.5D XY setup (zero out Z if needed)
            delta.z = 0f;
            vel = delta / Mathf.Max(Time.deltaTime, 0.0001f);
        }

        // Still not moving? Bail until we have motion.
        if (vel.sqrMagnitude < minSpeed * minSpeed) return;

        // 2) Compare with reference direction
        Vector3 refFwd = (referenceDirection ? referenceDirection.forward : transform.forward);
        refFwd.z = 0f; // keep in XY plane for 2.5D
        if (refFwd.sqrMagnitude < 1e-6f) refFwd = Vector3.right;
        refFwd.Normalize();

        Vector3 moveDir = vel;
        moveDir.z = 0f;
        moveDir.Normalize();

        float dot = Vector3.Dot(moveDir, refFwd);
        if (invert) dot = -dot; // optional per-trigger flip

        // 3) Apply yaw based on dot sign (flipped from your original)
        if (dot >= 0f)
            CameraFollowFlip.Instance.SetYaw(yawAgainstRef);
        else
            CameraFollowFlip.Instance.SetYaw(yawWithRef);

        _lastApplied[player] = now;
    }
}
