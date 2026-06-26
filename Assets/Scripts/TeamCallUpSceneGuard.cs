using UnityEngine;
using UnityEngine.SceneManagement;

// Put this on an empty GameObject in:
// National team call-up system
//
// It stops the player from opening the call-up scene again
// after the 26-player squad is confirmed.
[DisallowMultipleComponent]
public class TeamCallUpSceneGuard : MonoBehaviour
{
    [SerializeField] private string homeSceneName = "HomeScene";

    private void Start()
    {
        if (GameProgressManager.Instance != null &&
            GameProgressManager.Instance.HasSelectedTeam)
        {
            SceneManager.LoadScene(homeSceneName);
        }
    }
}
