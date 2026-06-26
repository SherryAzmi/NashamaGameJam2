using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class SceneAccessGuard : MonoBehaviour
{
    [SerializeField] private bool requiresSelectedTeam = true;
    [SerializeField] private string fallbackSceneName = "HomeScene";

    private void Start()
    {
        if (!requiresSelectedTeam)
        {
            return;
        }

        bool hasAccess =
            GameProgressManager.Instance != null &&
            GameProgressManager.Instance.HasSelectedTeam;

        if (!hasAccess)
        {
            SceneManager.LoadScene(fallbackSceneName);
        }
    }
}
