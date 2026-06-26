using TMPro;
using UnityEngine;

public class HomeSceneController : MonoBehaviour
{
    [Header("Optional Coach Name")]
    [SerializeField] private TMP_Text coachNameText;

    [Header("Buttons")]
    [SerializeField] private HomeSceneButton[] homeButtons;

    private void Start()
    {
        if (coachNameText != null)
        {
            string coachName =
                GameProgressManager.Instance != null
                    ? GameProgressManager.Instance.CoachName
                    : "Coach";

            coachNameText.text = "COACH: " + coachName;
        }

        RefreshHome();
    }

    public void RefreshHome()
    {
        if (homeButtons == null)
        {
            return;
        }

        foreach (HomeSceneButton item in homeButtons)
        {
            if (item != null)
            {
                item.RefreshLockState();
            }
        }
    }
}
