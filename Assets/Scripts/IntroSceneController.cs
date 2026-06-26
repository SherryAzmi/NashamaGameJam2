using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class IntroSceneController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_InputField coachNameInput;
    [SerializeField] private TMP_Text warningText;

    [Header("Scene")]
    [SerializeField] private string homeSceneName = "HomeScene";

    private void Start()
    {
        if (GameProgressManager.Instance != null &&
            !string.IsNullOrWhiteSpace(GameProgressManager.Instance.CoachName) &&
            coachNameInput != null)
        {
            coachNameInput.text = GameProgressManager.Instance.CoachName;
        }

        if (warningText != null)
        {
            warningText.gameObject.SetActive(false);
        }
    }

    public void StartGame()
    {
        string coachName = coachNameInput != null
            ? coachNameInput.text
            : "";

        if (string.IsNullOrWhiteSpace(coachName))
        {
            ShowWarning("ENTER YOUR NAME FIRST.");
            return;
        }

        if (GameProgressManager.Instance == null)
        {
            Debug.LogError(
                "GameProgressManager is missing. Add it to IntroScene."
            );
            return;
        }

        GameProgressManager.Instance.SetCoachName(coachName);
        SceneManager.LoadScene(homeSceneName);
    }

    private void ShowWarning(string message)
    {
        if (warningText == null)
        {
            Debug.LogWarning(message);
            return;
        }

        warningText.gameObject.SetActive(true);
        warningText.text = message;
    }
}
