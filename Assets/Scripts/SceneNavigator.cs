using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneNavigator : MonoBehaviour
{
    public void OpenTrainingScene()
    {
        SceneManager.LoadScene("TrainingScene");
    }

    public void ReturnToFormationScene()
    {
        SceneManager.LoadScene("FormationScene");
    }
}