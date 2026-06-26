using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

// Lives in FormationScene. Hook a "Play Match" button to LaunchMatch():
// builds the MatchSetup from the confirmed starting XI plus the opponent's
// real NationalTeamData, hands it to the persistent MatchSession, then
// loads MatchDayScene. Also handles the half-time case: when MatchDayScene
// loads this scene additively for a formation edit, "Play Match" is hidden
// in favor of "Continue 2nd Half", which just unloads this scene and lets
// the in-progress match resume instead of starting a new one.
public class MatchLauncher : MonoBehaviour
{
    public string matchDaySceneName = "MatchDayScene";

    [Tooltip("Fallback opponent for testing this scene directly. The campaign hub sets MatchSession.PendingOpponentTeam before loading this scene, which takes priority over this field.")]
    public NationalTeamData fallbackOpponentTeam;

    [Header("Half-time editing")]
    public GameObject playMatchButton;
    public GameObject continueSecondHalfButton;

    private void Start()
    {
        bool isHalftimeEditing = MatchSession.GetOrCreate().IsHalftimeEditing;

        if (playMatchButton != null)
        {
            playMatchButton.SetActive(!isHalftimeEditing);
        }

        if (continueSecondHalfButton != null)
        {
            continueSecondHalfButton.SetActive(isHalftimeEditing);
        }
    }

    public void LaunchMatch()
    {
        TeamManager teamManager = FindFirstObjectByType<TeamManager>();

        if (teamManager == null || teamManager.startingEleven.Count != 11)
        {
            Debug.LogError("MatchLauncher: need a TeamManager with a confirmed starting XI.");
            return;
        }

        NationalTeamData opponentTeam = MatchSession.GetOrCreate().ConsumePendingOpponentTeam();

        if (opponentTeam == null)
        {
            opponentTeam = fallbackOpponentTeam;
        }

        if (opponentTeam == null)
        {
            Debug.LogError("MatchLauncher: no opponent NationalTeamData set (neither from the campaign hub nor the fallbackOpponentTeam field).");
            return;
        }

        teamManager.substitutionsUsed = 0;

        TeamMatchRatings home = MatchSetupBuilder.BuildRatings("Jordan", teamManager.startingEleven);
        TeamMatchRatings away = NationalTeamOpponentBuilder.BuildOpponentRatings(opponentTeam);

        MatchSetup setup = new MatchSetup(home, away);

        MatchSession.GetOrCreate().SetPendingSetup(setup);

        SceneManager.LoadScene(matchDaySceneName);
    }

    public void ContinueSecondHalf()
    {
        // Disable this scene's own EventSystem BEFORE unloading and before
        // MatchDayScene's EventSystem comes back on. Two EventSystems active
        // at once (even briefly) can leave EventSystem.current pointing at
        // whichever one gets destroyed during the async unload, breaking
        // all UI input for the rest of the match.
        EventSystem localEventSystem = FindFirstObjectByType<EventSystem>();

        if (localEventSystem != null)
        {
            localEventSystem.gameObject.SetActive(false);
        }

        MatchSession.GetOrCreate().SetHalftimeEditing(false);
        SceneManager.UnloadSceneAsync("FormationScene");
        MatchEvents.RaiseHalftimeEditComplete();
    }
}
