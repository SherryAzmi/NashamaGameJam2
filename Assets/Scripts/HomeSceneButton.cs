using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class HomeSceneButton : MonoBehaviour
{
    [Header("Destination")]
    [SerializeField] private string targetSceneName;

    [Tooltip("Use this for Formation, Training, and Match.")]
    [SerializeField] private bool requiresSelectedTeam = true;

    [Tooltip("Use this ONLY for National Team Call-Up. " +
             "It is available before selecting the team and locks after.")]
    [SerializeField] private bool onlyBeforeTeamSelection = false;

    [Header("UI")]
    [SerializeField] private Button button;

    [Tooltip("Drag the LockOverlay Image component here.")]
    [SerializeField] private Image lockOverlay;

    private void Reset()
    {
        button = GetComponent<Button>();
    }

    private void Start()
    {
        RefreshLockState();
    }

    private void OnEnable()
    {
        RefreshLockState();
    }

    public void RefreshLockState()
    {
        bool unlocked = IsUnlocked();

        if (button != null)
        {
            button.interactable = unlocked;
        }

        if (lockOverlay != null)
        {
            lockOverlay.gameObject.SetActive(!unlocked);
        }
    }

    public void OpenDestination()
    {
        if (!IsUnlocked())
        {
            return;
        }

        SceneManager.LoadScene(targetSceneName);
    }

    private bool IsUnlocked()
    {
        bool hasSelectedTeam =
            GameProgressManager.Instance != null &&
            GameProgressManager.Instance.HasSelectedTeam;

        // National Team Call-Up يفتح فقط قبل ما تثبت الفريق.
        if (onlyBeforeTeamSelection)
        {
            return !hasSelectedTeam;
        }

        // Formation / Training / Match يفتحوا بعد تثبيت الفريق.
        if (requiresSelectedTeam)
        {
            return hasSelectedTeam;
        }

        return true;
    }
}