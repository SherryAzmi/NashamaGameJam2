using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private TMP_InputField playerNameInput;

    public void OnPlayClicked()
    {
        string playerName = playerNameInput != null
            ? playerNameInput.text.Trim()
            : string.Empty;

        if (string.IsNullOrWhiteSpace(playerName))
        {
            Debug.Log("Enter your coach name first.");
            return;
        }

        // يحفظ الاسم بالنظام الجديد للتنقل والهومي.
        if (GameProgressManager.Instance != null)
        {
            GameProgressManager.Instance.SetCoachName(playerName);
        }
        else
        {
            Debug.LogError(
                "GameProgressManager is missing from IntroScene."
            );
            return;
        }

        // نخليه كمان محفوظ بالمفتاح القديم، حتى ما نخرب أي كود سابق.
        PlayerPrefs.SetString("PlayerName", playerName);
        PlayerPrefs.Save();

        SceneManager.LoadScene("HomeScene");
    }
}