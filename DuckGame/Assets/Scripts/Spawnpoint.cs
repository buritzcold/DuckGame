using UnityEngine;

public class SpawnPoint : MonoBehaviour
{
    [Tooltip("Unique id used by portals to select this spawn point.")]
    public string spawnId;

    [Tooltip("If true and no specific spawn id is requested, this will be used as the default.")]
    public bool defaultIfUnspecified = false;

    [Tooltip("Optional facing direction. True = face right, False = face left. Leave null to keep current facing.")]
    public bool? faceRight = null;

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(transform.position + Vector3.up * 0.5f, new Vector3(0.5f, 1f, 0.5f));
        if (!string.IsNullOrEmpty(spawnId))
        {
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position + Vector3.up * 1.2f, $"Spawn: {spawnId}");
            #endif
        }
    }
}
