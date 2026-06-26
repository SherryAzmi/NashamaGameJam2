using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private TMP_InputField playerNameInput;

    public void OnPlayClicked()
    {
        string playerName = playerNameInput != null ? playerNameInput.text : string.Empty;
        PlayerPrefs.SetString("PlayerName", playerName);
        PlayerPrefs.Save();

        SceneManager.LoadScene("ChooseScene");
    }
}
