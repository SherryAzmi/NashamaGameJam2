using UnityEngine;
using UnityEngine.SceneManagement;

public class ReturnHomeButton : MonoBehaviour
{
    [SerializeField] private string homeSceneName = "HomeScene";

    public void ReturnHome()
    {
        SceneManager.LoadScene(homeSceneName);
    }
}
