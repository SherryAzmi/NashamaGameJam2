using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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

    [Tooltip("Repurposed into the \"Continue 2nd Half\" button during a half-time formation edit, since there is no separate continue button authored in the scene - you can't leave to train mid-match anyway.")]
    public Button trainingButton;

    [Tooltip("Hidden during a half-time formation edit - you can't leave to the home screen mid-match.")]
    public GameObject homeButton;

    private void Start()
    {
        bool isHalftimeEditing = MatchSession.GetOrCreate().IsHalftimeEditing;

        if (playMatchButton != null)
        {
            playMatchButton.SetActive(!isHalftimeEditing);
        }

        if (homeButton != null)
        {
            homeButton.SetActive(!isHalftimeEditing);
        }

        if (continueSecondHalfButton != null)
        {
            continueSecondHalfButton.SetActive(isHalftimeEditing);
        }

        if (trainingButton != null && isHalftimeEditing)
        {
            // Replace the whole UnityEvent (not just AddListener) so the
            // Inspector-wired "open Training scene" persistent call is
            // dropped too - otherwise both it and ContinueSecondHalf would
            // fire on click. This only ever runs for this additive,
            // halftime-only load of the scene; a normal single-scene load
            // of FormationScene starts with the original persistent call
            // intact, since that change never gets serialized back to disk.
            trainingButton.onClick = new Button.ButtonClickedEvent();
            trainingButton.onClick.AddListener(ContinueSecondHalf);

            TMP_Text label = trainingButton.GetComponentInChildren<TMP_Text>();

            if (label != null)
            {
                label.text = "CONTINUE";
            }
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

        string formation = GameProgressManager.Instance != null && !string.IsNullOrWhiteSpace(GameProgressManager.Instance.CurrentFormation)
            ? GameProgressManager.Instance.CurrentFormation
            : "4-3-3";
        TeamTrainingState trainingState = TrainingManager.Instance != null ? TrainingManager.Instance.TeamState : null;

        TeamMatchRatings home = MatchSetupBuilder.BuildRatings("Jordan", teamManager.startingEleven, formation, trainingState);
        TeamMatchRatings away = NationalTeamOpponentBuilder.BuildOpponentRatings(opponentTeam);

        MatchSetup setup = new MatchSetup(home, away);

        MatchSession.GetOrCreate().SetPendingSetup(setup);

        // Lock in the lineup/formation on disk right before kickoff - if the
        // app closes mid-match, the squad the player actually picked is what
        // comes back, not whatever was last saved at squad confirmation.
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.SaveCurrentState();
        }

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
