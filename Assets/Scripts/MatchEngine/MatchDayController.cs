using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Lives in MatchDayScene. Reads the MatchSetup handed off by MatchLauncher,
// shows a pre-kickoff comparison screen, then on "Kick Off" runs the
// simulation and plays it back live on the pitch. Pauses automatically at
// half-time, where the manager can either continue straight to the 2nd
// half or open the real formation editor (loaded additively on top, so
// nothing about this match's progress is lost) before continuing.
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
    public Image homeFlagImage;
    public Image awayFlagImage;
    public Button kickOffButton;

    [Header("Match HUD")]
    public GameObject matchHud;
    public TMP_Text scoreText;
    public TMP_Text minuteText;
    public TMP_Text eventLogText;
    public int maxLogLines = 8;

    [Header("Half-time panel")]
    public GameObject halfTimePanel;
    public TMP_Text halfTimeScoreText;
    public Button editFormationButton;
    public Button startSecondHalfButton;

    [Header("Scene roots (disabled during the additive formation edit)")]
    public Canvas matchCanvas;
    public Camera matchCamera;
    public GameObject matchEventSystem;

    [Header("Result panel")]
    public GameObject resultPanel;
    public TMP_Text resultText;
    public Button continueButton;
    public string campaignSceneName = "CampaignScene";

    [Header("Lose / Retry")]
    [Tooltip("Shown only right after a loss, and only if the retry for this match hasn't been used yet.")]
    public Button retryButton;
    [Tooltip("Shown only after losing the SAME match a second time (the one retry already used) - wipes the whole campaign and sends the player back to squad selection.")]
    public Button restartCampaignButton;
    public TMP_Text retryInfoText;
    public int retryConsolationPoints = 20;

    [Header("Systems")]
    public MatchPitchController pitchController;
    public MatchPlaybackController playbackController;
    public MatchDecisiveMomentController decisiveMomentController;
    public MatchPenaltyController penaltyController;

    [Header("Knockout")]
    [Tooltip("Overridden by MatchSession.PendingIsKnockout when launched from the campaign hub's Final fixture. This Inspector value is only used as a fallback when testing this scene directly.")]
    public bool isKnockoutMatch;

    [Header("Speed")]
    [Tooltip("Overall match speed multiplier. 1 = normal. Drag this live during play to speed up or slow down the whole match (movement, minute ticks, decisive moment pacing).")]
    [Range(0.25f, 4f)]
    public float gameSpeed = 1f;

    private MatchSetup setup;
    private int homeScore;
    private int awayScore;
    private bool? penaltyHomeWon;
    private int? penaltyHomeScore;
    private int? penaltyAwayScore;
    private readonly System.Collections.Generic.List<string> logLines = new System.Collections.Generic.List<string>();
    private readonly System.Collections.Generic.List<GoalEvent> liveGoals = new System.Collections.Generic.List<GoalEvent>();

    // Resets once per scene load (i.e. once per fixture) - one retry is
    // allowed per match. A second loss on the same match forces a full
    // campaign restart instead of a normal "continue".
    private int lossAttemptsThisMatch;

    private void Start()
    {
        setup = MatchSession.GetOrCreate().ConsumePendingSetup();

        if (setup == null)
        {
            Debug.LogError("MatchDayController: no MatchSetup found. Launch the match from FormationScene via MatchLauncher.");
            return;
        }

        isKnockoutMatch = MatchSession.GetOrCreate().ConsumePendingIsKnockout() || isKnockoutMatch;
        lossAttemptsThisMatch = 0;

        ShowPreview();

        kickOffButton.onClick.RemoveAllListeners();
        kickOffButton.onClick.AddListener(KickOff);

        continueButton.onClick.RemoveAllListeners();
        continueButton.onClick.AddListener(ReturnToCampaign);

        if (retryButton != null)
        {
            retryButton.onClick.RemoveAllListeners();
            retryButton.onClick.AddListener(RetryMatch);
        }

        if (restartCampaignButton != null)
        {
            restartCampaignButton.onClick.RemoveAllListeners();
            restartCampaignButton.onClick.AddListener(() => CampaignRestartService.RestartWholeCampaign());
        }

        editFormationButton.onClick.RemoveAllListeners();
        editFormationButton.onClick.AddListener(EditFormationAtHalftime);

        startSecondHalfButton.onClick.RemoveAllListeners();
        startSecondHalfButton.onClick.AddListener(StartSecondHalf);

        halfTimePanel.SetActive(false);
    }

    private void ReturnToCampaign()
    {
        if (CampaignState.Instance != null)
        {
            bool homeWon = penaltyHomeWon ?? (homeScore > awayScore);
            CampaignState.Instance.RecordResult(homeScore, awayScore, homeWon, penaltyHomeScore, penaltyAwayScore);
        }

        SceneManager.LoadScene(campaignSceneName);
    }

    private void OnEnable()
    {
        MatchEvents.OnGoal += HandleGoal;
        MatchEvents.OnCard += HandleCard;
        MatchEvents.OnInjury += HandleInjury;
        MatchEvents.OnMinuteTick += HandleMinuteTick;
        MatchEvents.OnMatchEnd += HandleMatchEnd;
        MatchEvents.OnHalfTime += HandleHalfTime;
        MatchEvents.OnHalftimeEditComplete += HandleHalftimeEditComplete;
    }

    private void OnDisable()
    {
        MatchEvents.OnGoal -= HandleGoal;
        MatchEvents.OnCard -= HandleCard;
        MatchEvents.OnInjury -= HandleInjury;
        MatchEvents.OnMinuteTick -= HandleMinuteTick;
        MatchEvents.OnMatchEnd -= HandleMatchEnd;
        MatchEvents.OnHalfTime -= HandleHalfTime;
        MatchEvents.OnHalftimeEditComplete -= HandleHalftimeEditComplete;
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

        homeFormationText.text = "JORDAN\n" + setup.home.formation;
        awayFormationText.text = setup.away.teamName.ToUpperInvariant() + "\n" + setup.away.formation;

        homeStatsText.text = FormatStats(setup.home);
        awayStatsText.text = FormatStats(setup.away);

        homePlayerListText.text = FormatPlayerList(setup.home.startingEleven);
        awayPlayerListText.text = FormatPlayerList(setup.away.startingEleven);

        homePreviewDiagram.Render(setup.home.startingEleven, setup.home.formation, false);
        awayPreviewDiagram.Render(setup.away.startingEleven, "4-3-3", true);

        SetFlag(homeFlagImage, setup.home.flag);
        SetFlag(awayFlagImage, setup.away.flag);
    }

    private void SetFlag(Image image, Sprite flag)
    {
        if (image == null)
        {
            return;
        }

        image.sprite = flag;
        image.enabled = flag != null;
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
        penaltyHomeWon = null;
        penaltyHomeScore = null;
        penaltyAwayScore = null;
        logLines.Clear();
        liveGoals.Clear();
        UpdateScoreText();

        pitchController.Setup(setup);

        MatchSimulator simulator = new MatchSimulator();
        MatchResult result = simulator.SimulateMatch(setup);

        playbackController.PlayMatch(result);
        decisiveMomentController.BeginMatch(setup);
    }

    private void HandleHalfTime()
    {
        decisiveMomentController.PauseForHalftime();

        halfTimeScoreText.text = $"HALF TIME\n\nJORDAN {homeScore} - {awayScore} {setup.away.teamName.ToUpperInvariant()}";
        halfTimePanel.SetActive(true);
    }

    private void StartSecondHalf()
    {
        halfTimePanel.SetActive(false);
        FlipSidesAndResume();
    }

    private void EditFormationAtHalftime()
    {
        matchCanvas.enabled = false;
        matchCamera.enabled = false;
        matchEventSystem.SetActive(false);

        MatchSession.GetOrCreate().SetHalftimeEditing(true);
        SceneManager.LoadScene("FormationScene", LoadSceneMode.Additive);
    }

    private void HandleHalftimeEditComplete()
    {
        matchCanvas.enabled = true;
        matchCamera.enabled = true;
        matchEventSystem.SetActive(true);

        halfTimePanel.SetActive(false);
        FlipSidesAndResume();
    }

    private void FlipSidesAndResume()
    {
        TeamManager teamManager = FindFirstObjectByType<TeamManager>();

        if (teamManager != null)
        {
            // Read fresh in case the manager changed formation during the
            // halftime edit - not the formation the 1st half started with.
            string formation = GameProgressManager.Instance != null && !string.IsNullOrWhiteSpace(GameProgressManager.Instance.CurrentFormation)
                ? GameProgressManager.Instance.CurrentFormation
                : setup.home.formation;
            TeamTrainingState trainingState = TrainingManager.Instance != null ? TrainingManager.Instance.TeamState : null;

            setup.home = MatchSetupBuilder.BuildRatings("Jordan", teamManager.startingEleven, formation, trainingState);
        }

        // Whatever lineup is about to play the 2nd half (possibly just
        // edited at halftime) gets locked in now, so it survives a crash
        // or an early quit at the 45-minute mark.
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.SaveCurrentState();
        }

        pitchController.SetSecondHalf();
        pitchController.Setup(setup);

        decisiveMomentController.ResumeFromHalftime();
        playbackController.Resume();
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

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayGoal(goalEvent.side);
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

    private void ShowFullTimeResultAfterPenalties(int penaltyHomeScoreResult, int penaltyAwayScoreResult)
    {
        penaltyHomeWon = penaltyHomeScoreResult > penaltyAwayScoreResult;
        penaltyHomeScore = penaltyHomeScoreResult;
        penaltyAwayScore = penaltyAwayScoreResult;
        ShowFullTimeResult($"\nPENALTIES: {penaltyHomeScoreResult} - {penaltyAwayScoreResult}");
    }

    private void ShowFullTimeResult(string penaltyLine)
    {
        resultPanel.SetActive(true);

        string motmName = FindManOfTheMatch();

        // Bonus points are tracked (decisiveMomentController.TotalBonusPoints)
        // but intentionally not shown here - the points system is being
        // rebuilt separately and will hook into that value once ready.
        resultText.text =
            $"FULL TIME\n\nJORDAN {homeScore} - {awayScore} {setup.away.teamName.ToUpperInvariant()}{penaltyLine}\n\n" +
            $"MAN OF THE MATCH: {motmName}";

        UpdateLoseRetryUi();
    }

    // A draw only counts as a loss in a knockout match (it always resolves
    // to a winner via penalties before this is called) - a drawn friendly
    // is not a loss and gets no retry/restart prompt.
    private bool DidJordanLose()
    {
        bool homeWon = penaltyHomeWon ?? (homeScore > awayScore);
        bool isDraw = !isKnockoutMatch && homeScore == awayScore;

        return !isDraw && !homeWon;
    }

    private void UpdateLoseRetryUi()
    {
        bool jordanLost = DidJordanLose();
        bool canRetry = jordanLost && lossAttemptsThisMatch == 0;
        bool mustRestartCampaign = jordanLost && lossAttemptsThisMatch >= 1;

        if (retryButton != null)
        {
            retryButton.gameObject.SetActive(canRetry);
        }

        if (restartCampaignButton != null)
        {
            restartCampaignButton.gameObject.SetActive(mustRestartCampaign);
        }

        if (continueButton != null)
        {
            continueButton.gameObject.SetActive(!mustRestartCampaign);
        }

        if (retryInfoText != null)
        {
            if (mustRestartCampaign)
            {
                retryInfoText.text = "YOU LOST THIS MATCH TWICE - THE WHOLE CAMPAIGN MUST RESTART.";
            }
            else if (canRetry)
            {
                retryInfoText.text = $"YOU LOST. RETRY THIS MATCH FOR +{retryConsolationPoints} DEVELOPMENT POINTS, OR CONTINUE TO ACCEPT THE RESULT.";
            }
            else
            {
                retryInfoText.text = "";
            }
        }
    }

    // Replays this exact fixture from kickoff - same opponent, no campaign
    // result recorded yet. Costs the player's one retry for this match and
    // grants a small consolation reward toward training.
    private void RetryMatch()
    {
        lossAttemptsThisMatch++;

        if (TrainingManager.Instance != null)
        {
            TrainingManager.Instance.AddDevelopmentPoints(retryConsolationPoints);
        }

        if (retryButton != null)
        {
            retryButton.gameObject.SetActive(false);
        }

        KickOff();
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
