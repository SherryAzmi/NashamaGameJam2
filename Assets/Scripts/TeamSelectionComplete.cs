using UnityEngine;
using UnityEngine.SceneManagement;

// Add this component to the GameManager in:
// National team call-up system
//
// Call CompleteTeamSelection() only AFTER your TeamManager has
// validated that 26 players are selected and created the team.
public class TeamSelectionComplete : MonoBehaviour
{
    [SerializeField] private string homeSceneName = "HomeScene";

    public void CompleteTeamSelection()
    {
        if (GameProgressManager.Instance == null)
        {
            Debug.LogError(
                "GameProgressManager is missing. Start the game from IntroScene."
            );
            return;
        }

        GameProgressManager.Instance.MarkTeamSelected();
        SceneManager.LoadScene(homeSceneName);
    }
}
