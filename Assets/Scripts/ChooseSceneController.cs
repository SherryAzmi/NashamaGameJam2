using UnityEngine;
using TMPro;

public class ChooseSceneController : MonoBehaviour
{
    [SerializeField] private TMP_Text userNameText;

    private void Start()
    {
        if (userNameText != null)
        {
            string playerName = PlayerPrefs.GetString("PlayerName", string.Empty);
            userNameText.text = $"{playerName}";
        }
    }
}
