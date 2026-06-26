using UnityEngine;
using UnityEngine.SceneManagement;

// Lives in FormationScene. Hook a "Play Match" button to LaunchMatch():
// builds the MatchSetup from the confirmed starting XI, hands it to the
// persistent MatchSession, then loads MatchDayScene.
public class MatchLauncher : MonoBehaviour
{
    public string matchDaySceneName = "MatchDayScene";
    public int opponentOverall = 75;

    public void LaunchMatch()
    {
        TeamManager teamManager = FindFirstObjectByType<TeamManager>();

        if (teamManager == null || teamManager.startingEleven.Count != 11)
        {
            Debug.LogError("MatchLauncher: need a TeamManager with a confirmed starting XI.");
            return;
        }

        TeamMatchRatings home = MatchSetupBuilder.BuildRatings("Jordan", teamManager.startingEleven);
        TeamMatchRatings away = MatchSetupBuilder.BuildPlaceholderOpponent("Opponent", opponentOverall, teamManager.benchPlayers);

        MatchSetup setup = new MatchSetup(home, away);

        MatchSession.GetOrCreate().SetPendingSetup(setup);

        SceneManager.LoadScene(matchDaySceneName);
    }
}
