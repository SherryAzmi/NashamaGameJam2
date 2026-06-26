using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Drives the "Decisive Moment" mini-game from the design doc: every so
// often the match pauses (slow motion), shows a context-appropriate panel
// (Shoot/Pass/Dribble when your team has the ball, Tackle/Block/Press when
// defending), and the player's choice - resolved against real stats -
// decides what happens, including whether a goal is scored. This is the
// only way goals happen now; MatchSimulator no longer auto-resolves them.
public class MatchDecisiveMomentController : MonoBehaviour
{
    public enum MatchDifficulty
    {
        Easy,
        Medium,
        Hard
    }

    [Header("Difficulty")]
    [Tooltip("Easy = forgiving decisions, more time between moments. Hard = tougher opponent stats, less time to react. CampaignManager (Dev A, future) can set this before kickoff.")]
    public MatchDifficulty difficulty = MatchDifficulty.Medium;

    [Header("Timing")]
    public float minIntervalSeconds = 10f;
    public float maxIntervalSeconds = 16f;
    public float slowMotionScale = 0.3f;
    public float resultPopupDuration = 1.6f;

    [Header("Decision panel")]
    public GameObject decisivePanel;
    public TMP_Text contextText;
    public Button option1Button;
    public Button option2Button;
    public Button option3Button;
    public TMP_Text option1Label;
    public TMP_Text option2Label;
    public TMP_Text option3Label;

    [Header("Result popup")]
    public GameObject resultPopup;
    public TMP_Text resultPopupText;

    [Header("Systems")]
    public MatchPlaybackController playbackController;
    public MatchPitchController pitchController;

    public int TotalBonusPoints { get; private set; }
    public bool IsMomentActive => currentMoment != null;

    private MatchSetup setup;
    private float attackBoost = 1f;
    private float nextMomentAt;
    private bool matchActive;
    private int currentMinute;
    private DecisiveMoment currentMoment;

    private void OnEnable()
    {
        MatchEvents.OnMinuteTick += HandleMinuteTick;
    }

    private void OnDisable()
    {
        MatchEvents.OnMinuteTick -= HandleMinuteTick;
    }

    private void HandleMinuteTick(int minute)
    {
        currentMinute = minute;
    }

    public void BeginMatch(MatchSetup matchSetup)
    {
        setup = matchSetup;
        attackBoost = 1f;
        TotalBonusPoints = 0;
        currentMinute = 0;
        currentMoment = null;
        matchActive = true;

        decisivePanel.SetActive(false);
        resultPopup.SetActive(false);

        ScheduleNextMoment();
    }

    public void EndMatch()
    {
        matchActive = false;
        currentMoment = null;
        StopAllCoroutines();

        decisivePanel.SetActive(false);
        resultPopup.SetActive(false);
        Time.timeScale = 1f;
    }

    private void Update()
    {
        if (!matchActive || currentMoment != null)
        {
            return;
        }

        if (Time.time >= nextMomentAt)
        {
            TriggerMoment();
        }
    }

    // Uses scaled time on purpose: at higher game speed, decisive moments
    // arrive sooner in real time too, matching a faster-feeling match.
    // Harder difficulty also shortens the window between moments, per the
    // design doc's "friendlies attack every 90s, final every 45s" curve.
    private void ScheduleNextMoment()
    {
        float intervalScale = GetIntervalMultiplier();
        nextMomentAt = Time.time + Random.Range(minIntervalSeconds, maxIntervalSeconds) * intervalScale;
    }

    private float GetIntervalMultiplier()
    {
        switch (difficulty)
        {
            case MatchDifficulty.Easy: return 1.4f;
            case MatchDifficulty.Hard: return 0.65f;
            default: return 1f;
        }
    }

    private float GetDifficultyMultiplier()
    {
        switch (difficulty)
        {
            case MatchDifficulty.Easy: return 0.85f;
            case MatchDifficulty.Hard: return 1.15f;
            default: return 1f;
        }
    }

    private void TriggerMoment()
    {
        float difficultyMultiplier = GetDifficultyMultiplier();

        // Track who actually has the ball right now on the pitch, instead
        // of rolling a fresh coin flip - the panel must match the real
        // carrier the player can see on screen.
        MatchSide? liveSide = pitchController != null ? pitchController.CurrentPossessingSide : null;
        PlayerData liveCarrier = pitchController != null ? pitchController.CurrentCarrier : null;
        bool attackMoment = liveSide.HasValue ? liveSide.Value == MatchSide.Home : Random.value < 0.5f;

        if (attackMoment)
        {
            PlayerData carrier = liveCarrier != null && liveSide.HasValue
                ? liveCarrier
                : PickWeightedCarrier(setup.home.startingEleven);
            PlayerData opponentGoalkeeper = FindGoalkeeper(setup.away.startingEleven);

            currentMoment = new DecisiveMoment(DecisiveMomentType.Attack, carrier, opponentGoalkeeper, setup.away.defense)
            {
                attackBoost = attackBoost,
                difficultyMultiplier = difficultyMultiplier
            };

            ShowAttackPanel(currentMoment);
        }
        else
        {
            PlayerData threat = liveCarrier != null && liveSide.HasValue
                ? liveCarrier
                : PickWeightedCarrier(setup.away.startingEleven);
            PlayerData defender = PickDefender(setup.home.startingEleven);

            currentMoment = new DecisiveMoment(DecisiveMomentType.Defense, defender, threat, setup.away.defense)
            {
                difficultyMultiplier = difficultyMultiplier
            };

            ShowDefensePanel(currentMoment);
        }

        playbackController.Pause();
        Time.timeScale = MatchSpeed.Normal * slowMotionScale;
    }

    private void ShowAttackPanel(DecisiveMoment moment)
    {
        contextText.text = moment.actor.playerName + " HAS THE BALL!";

        SetButtonAction(option1Button, option1Label, "SHOOT", DecisiveAction.Shoot);
        SetButtonAction(option2Button, option2Label, "PASS", DecisiveAction.Pass);
        SetButtonAction(option3Button, option3Label, "DRIBBLE", DecisiveAction.Dribble);

        resultPopup.SetActive(false);
        decisivePanel.SetActive(true);
    }

    private void ShowDefensePanel(DecisiveMoment moment)
    {
        contextText.text = moment.opponent.playerName + " IS ATTACKING!";

        SetButtonAction(option1Button, option1Label, "TACKLE", DecisiveAction.Tackle);
        SetButtonAction(option2Button, option2Label, "BLOCK", DecisiveAction.Block);
        SetButtonAction(option3Button, option3Label, "PRESS", DecisiveAction.Press);

        resultPopup.SetActive(false);
        decisivePanel.SetActive(true);
    }

    private void SetButtonAction(Button button, TMP_Text label, string text, DecisiveAction action)
    {
        label.text = text;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => ResolveChoice(action));
    }

    private void ResolveChoice(DecisiveAction action)
    {
        DecisiveMomentOutcome outcome = MatchDecisiveMomentResolver.Resolve(currentMoment, action);

        TotalBonusPoints += outcome.bonusPoints;
        attackBoost = currentMoment.type == DecisiveMomentType.Attack ? outcome.nextAttackBoost : 1f;

        if (outcome.isGoal)
        {
            MatchSide scoringSide = currentMoment.type == DecisiveMomentType.Attack ? MatchSide.Home : MatchSide.Away;
            PlayerData scorer = currentMoment.type == DecisiveMomentType.Attack ? currentMoment.actor : currentMoment.opponent;

            MatchEvents.RaiseGoal(new GoalEvent(currentMinute, scoringSide, scorer, null));
        }

        ShowResultPopup(outcome.message);
    }

    private void ShowResultPopup(string message)
    {
        decisivePanel.SetActive(false);
        resultPopup.SetActive(true);
        resultPopupText.text = message;

        StartCoroutine(ResumeAfterPopup());
    }

    private IEnumerator ResumeAfterPopup()
    {
        yield return new WaitForSecondsRealtime(resultPopupDuration);

        resultPopup.SetActive(false);
        Time.timeScale = MatchSpeed.Normal;
        currentMoment = null;

        if (matchActive)
        {
            ScheduleNextMoment();
            playbackController.Resume();
        }
    }

    private PlayerData FindGoalkeeper(List<PlayerData> players)
    {
        foreach (PlayerData player in players)
        {
            if (MatchPitchLayout.GetCategory(player.position) == "GK")
            {
                return player;
            }
        }

        return players[0];
    }

    private PlayerData PickDefender(List<PlayerData> players)
    {
        List<PlayerData> defenders = new List<PlayerData>();

        foreach (PlayerData player in players)
        {
            if (MatchPitchLayout.GetCategory(player.position) == "DEF")
            {
                defenders.Add(player);
            }
        }

        if (defenders.Count == 0)
        {
            return players[Random.Range(0, players.Count)];
        }

        return defenders[Random.Range(0, defenders.Count)];
    }

    private PlayerData PickWeightedCarrier(List<PlayerData> players)
    {
        float totalWeight = 0f;
        float[] weights = new float[players.Count];

        for (int i = 0; i < players.Count; i++)
        {
            weights[i] = GetCarrierWeight(MatchPitchLayout.GetCategory(players[i].position));
            totalWeight += weights[i];
        }

        float roll = Random.Range(0f, totalWeight);
        float cumulative = 0f;

        for (int i = 0; i < weights.Length; i++)
        {
            cumulative += weights[i];

            if (roll < cumulative)
            {
                return players[i];
            }
        }

        return players[players.Count - 1];
    }

    private float GetCarrierWeight(string category)
    {
        switch (category)
        {
            case "GK": return 0.2f;
            case "DEF": return 1f;
            case "MID": return 3f;
            case "ATT": return 3.5f;
        }

        return 1f;
    }
}
