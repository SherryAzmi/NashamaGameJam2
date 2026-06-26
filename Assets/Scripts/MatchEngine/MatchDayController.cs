using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Lives in MatchDayScene. Reads the MatchSetup handed off by MatchLauncher,
// shows a pre-kickoff comparison screen, then on "Kick Off" runs the
// simulation and plays it back live on the pitch.
public class MatchDayController : MonoBehaviour
{
    [Header("Preview panel")]
    public GameObject previewPanel;
    public TMP_Text homeFormationText;
    public TMP_Text awayFormationText;
    public TMP_Text homeStatsText;
    public TMP_Text awayStatsText;
    public TMP_Text homePlayerListText;
    public TMP_Text awayPlayerListText;
    public MatchPreviewDiagram homePreviewDiagram;
    public MatchPreviewDiagram awayPreviewDiagram;
    public Button kickOffButton;

    [Header("Match HUD")]
    public GameObject matchHud;
    public TMP_Text scoreText;
    public TMP_Text minuteText;
    public TMP_Text eventLogText;
    public int maxLogLines = 8;

    [Header("Result panel")]
    public GameObject resultPanel;
    public TMP_Text resultText;

    [Header("Systems")]
    public MatchPitchController pitchController;
    public MatchPlaybackController playbackController;
    public MatchDecisiveMomentController decisiveMomentController;
    public MatchPenaltyController penaltyController;

    [Header("Knockout")]
    [Tooltip("If true and the score is level at full time, this match goes to a penalty shootout instead of ending in a draw. CampaignManager (Dev A, future) should set this for knockout/final matches.")]
    public bool isKnockoutMatch;

    [Header("Speed")]
    [Tooltip("Overall match speed multiplier. 1 = normal. Drag this live during play to speed up or slow down the whole match (movement, minute ticks, decisive moment pacing).")]
    [Range(0.25f, 4f)]
    public float gameSpeed = 1f;

    private MatchSetup setup;
    private int homeScore;
    private int awayScore;
    private readonly System.Collections.Generic.List<string> logLines = new System.Collections.Generic.List<string>();
    private readonly System.Collections.Generic.List<GoalEvent> liveGoals = new System.Collections.Generic.List<GoalEvent>();

    private void Start()
    {
        setup = MatchSession.GetOrCreate().ConsumePendingSetup();

        if (setup == null)
        {
            Debug.LogError("MatchDayController: no MatchSetup found. Launch the match from FormationScene via MatchLauncher.");
            return;
        }

        ShowPreview();

        kickOffButton.onClick.RemoveAllListeners();
        kickOffButton.onClick.AddListener(KickOff);
    }

    private void OnEnable()
    {
        MatchEvents.OnGoal += HandleGoal;
        MatchEvents.OnCard += HandleCard;
        MatchEvents.OnInjury += HandleInjury;
        MatchEvents.OnMinuteTick += HandleMinuteTick;
        MatchEvents.OnMatchEnd += HandleMatchEnd;
    }

    private void OnDisable()
    {
        MatchEvents.OnGoal -= HandleGoal;
        MatchEvents.OnCard -= HandleCard;
        MatchEvents.OnInjury -= HandleInjury;
        MatchEvents.OnMinuteTick -= HandleMinuteTick;
        MatchEvents.OnMatchEnd -= HandleMatchEnd;
    }

    private void Update()
    {
        MatchSpeed.Normal = gameSpeed;

        if (!decisiveMomentController.IsMomentActive)
        {
            Time.timeScale = gameSpeed;
        }
    }

    private void ShowPreview()
    {
        previewPanel.SetActive(true);
        matchHud.SetActive(false);
        resultPanel.SetActive(false);

        homeFormationText.text = "JORDAN\n" + MatchPitchLayout.InferFormationLabel(setup.home.startingEleven);
        awayFormationText.text = setup.away.teamName.ToUpperInvariant() + "\n" + MatchPitchLayout.InferFormationLabel(setup.away.startingEleven);

        homeStatsText.text = FormatStats(setup.home);
        awayStatsText.text = FormatStats(setup.away);

        homePlayerListText.text = FormatPlayerList(setup.home.startingEleven);
        awayPlayerListText.text = FormatPlayerList(setup.away.startingEleven);

        homePreviewDiagram.Render(setup.home.startingEleven, false);
        awayPreviewDiagram.Render(setup.away.startingEleven, true);
    }

    private string FormatStats(TeamMatchRatings ratings)
    {
        return $"ATT: {ratings.attack}\nMID: {ratings.midfield}\nDEF: {ratings.defense}\nPOWER: {ratings.power}";
    }

    private string FormatPlayerList(System.Collections.Generic.List<PlayerData> players)
    {
        StringBuilder builder = new StringBuilder();

        for (int i = 0; i < players.Count; i++)
        {
            builder.AppendLine($"{i + 1}. {players[i].playerName}");
        }

        return builder.ToString();
    }

    private void KickOff()
    {
        previewPanel.SetActive(false);
        matchHud.SetActive(true);
        resultPanel.SetActive(false);

        homeScore = 0;
        awayScore = 0;
        logLines.Clear();
        liveGoals.Clear();
        UpdateScoreText();

        pitchController.Setup(setup);

        MatchSimulator simulator = new MatchSimulator();
        MatchResult result = simulator.SimulateMatch(setup);

        playbackController.PlayMatch(result);
        decisiveMomentController.BeginMatch(setup);
    }

    private void HandleGoal(GoalEvent goalEvent)
    {
        if (goalEvent.side == MatchSide.Home)
        {
            homeScore++;
        }
        else
        {
            awayScore++;
        }

        UpdateScoreText();
        liveGoals.Add(goalEvent);

        string scorerName = goalEvent.scorer != null ? goalEvent.scorer.playerName : "Unknown";
        AppendLog($"[{goalEvent.minute}'] GOAL - {SideLabel(goalEvent.side)} - {scorerName}");
    }

    private void HandleCard(CardEvent cardEvent)
    {
        string playerName = cardEvent.player != null ? cardEvent.player.playerName : "Unknown";
        AppendLog($"[{cardEvent.minute}'] {cardEvent.cardType.ToString().ToUpperInvariant()} CARD - {SideLabel(cardEvent.side)} - {playerName}");
    }

    private void HandleInjury(InjuryEvent injuryEvent)
    {
        string playerName = injuryEvent.player != null ? injuryEvent.player.playerName : "Unknown";
        AppendLog($"[{injuryEvent.minute}'] INJURY - {SideLabel(injuryEvent.side)} - {playerName}");
    }

    private void HandleMinuteTick(int minute)
    {
        if (minuteText != null)
        {
            minuteText.text = minute + "'";
        }
    }

    private void HandleMatchEnd(MatchResult result)
    {
        decisiveMomentController.EndMatch();
        matchHud.SetActive(false);

        if (isKnockoutMatch && homeScore == awayScore)
        {
            penaltyController.BeginShootout(setup, ShowFullTimeResultAfterPenalties);
            return;
        }

        ShowFullTimeResult(string.Empty);
    }

    private void ShowFullTimeResultAfterPenalties(int penaltyHomeScore, int penaltyAwayScore)
    {
        ShowFullTimeResult($"\nPENALTIES: {penaltyHomeScore} - {penaltyAwayScore}");
    }

    private void ShowFullTimeResult(string penaltyLine)
    {
        resultPanel.SetActive(true);

        string motmName = FindManOfTheMatch();

        resultText.text =
            $"FULL TIME\n\nJORDAN {homeScore} - {awayScore} {setup.away.teamName.ToUpperInvariant()}{penaltyLine}\n\n" +
            $"MAN OF THE MATCH: {motmName}\nBONUS POINTS: {decisiveMomentController.TotalBonusPoints}";
    }

    private string FindManOfTheMatch()
    {
        PlayerData best = null;
        int bestGoals = 0;
        var goalCounts = new System.Collections.Generic.Dictionary<PlayerData, int>();

        foreach (GoalEvent goal in liveGoals)
        {
            if (goal.scorer == null)
            {
                continue;
            }

            goalCounts.TryGetValue(goal.scorer, out int count);
            count++;
            goalCounts[goal.scorer] = count;

            if (count > bestGoals)
            {
                bestGoals = count;
                best = goal.scorer;
            }
        }

        return best != null ? best.playerName : "None";
    }

    private string SideLabel(MatchSide side)
    {
        return side == MatchSide.Home ? "JORDAN" : setup.away.teamName.ToUpperInvariant();
    }

    private void UpdateScoreText()
    {
        if (scoreText != null)
        {
            scoreText.text = $"JORDAN {homeScore} - {awayScore} {setup.away.teamName.ToUpperInvariant()}";
        }
    }

    private void AppendLog(string line)
    {
        logLines.Add(line);

        while (logLines.Count > maxLogLines)
        {
            logLines.RemoveAt(0);
        }

        if (eventLogText != null)
        {
            eventLogText.text = string.Join("\n", logLines);
        }
    }
}
