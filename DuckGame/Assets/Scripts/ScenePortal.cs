using UnityEngine;
using UnityEngine.SceneManagement;

public class ScenePortal : MonoBehaviour
{
    [Tooltip("Name of the scene to load (must be in Build Settings).")]
    public string sceneToLoad;

    [Tooltip("Only the player can trigger this. Leave empty to allow anything.")]
    public string requiredTag = "Player";

    [Tooltip("Optional: SpawnPoint id in the destination scene to place the player at.")]
    public string targetSpawnId;

    bool _fired;

    void OnTriggerEnter(Collider other)
    {
        if (_fired) return;
        if (!string.IsNullOrEmpty(requiredTag) && !other.CompareTag(requiredTag)) return;
        if (string.IsNullOrEmpty(sceneToLoad)) return;

        _fired = true;

        // Tell the PlayerController which spawn to use in the next scene.
        PlayerController.NextSpawnId = targetSpawnId;

        SceneManager.LoadScene(sceneToLoad);
    }
}
