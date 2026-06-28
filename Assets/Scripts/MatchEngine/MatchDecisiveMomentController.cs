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

    [Header("Field-position trigger")]
    [Tooltip("Once a ball carrier's progress toward the goal they're attacking crosses this (0-1), a decisive moment can fire immediately instead of waiting for the random timer - the closer to goal, the more it matters.")]
    public float dangerZoneProgress = 0.78f;
    [Tooltip("Hard floor between any two moments, even a proximity-triggered one, so the player always has breathing room.")]
    public float minimumMomentCooldown = 4f;

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
    private bool isHalftimePaused;
    private int currentMinute;
    private DecisiveMoment currentMoment;
    private float lastMomentEndTime = -999f;

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
        isHalftimePaused = false;

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

    // Used for the half-time break: stop new moments from triggering while
    // the score panel/formation editor is up, without ending the match.
    public void PauseForHalftime()
    {
        isHalftimePaused = true;
    }

    public void ResumeFromHalftime()
    {
        isHalftimePaused = false;
        ScheduleNextMoment();
    }

    private void Update()
    {
        if (!matchActive || isHalftimePaused || currentMoment != null)
        {
            return;
        }

        if (Time.time - lastMomentEndTime < minimumMomentCooldown)
        {
            return;
        }

        if (IsInDangerZone() || Time.time >= nextMomentAt)
        {
            TriggerMoment();
        }
    }

    // The decision should appear because something is actually happening on
    // the pitch (a team closing in on a goal), not on a context-free timer.
    // Whichever side currently has the ball pushing deep into the other
    // team's third fires the moment early - same check works for both "my
    // team is attacking" and "the opponent is attacking me" since it just
    // reads whoever is the live possessing side.
    private bool IsInDangerZone()
    {
        if (pitchController == null || !pitchController.CurrentPossessingSide.HasValue)
        {
            return false;
        }

        return pitchController.CurrentAttackProgress01 >= dangerZoneProgress;
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

        float fieldProgress = pitchController != null ? pitchController.CurrentAttackProgress01 : 0.5f;
        int nearbyDefenders = pitchController != null ? pitchController.CurrentNearbyDefenderCount : 0;

        if (attackMoment)
        {
            PlayerData carrier = liveCarrier != null && liveSide.HasValue
                ? liveCarrier
                : PickWeightedCarrier(setup.home.startingEleven);
            PlayerData opponentGoalkeeper = FindGoalkeeper(setup.away.startingEleven, "4-3-3");

            currentMoment = new DecisiveMoment(DecisiveMomentType.Attack, carrier, opponentGoalkeeper, setup.away.attack, setup.away.midfield, setup.away.defense)
            {
                attackBoost = attackBoost,
                difficultyMultiplier = difficultyMultiplier,
                fieldProgress = fieldProgress,
                nearbyDefenderCount = nearbyDefenders
            };

            ShowAttackPanel(currentMoment);
        }
        else
        {
            PlayerData threat = liveCarrier != null && liveSide.HasValue
                ? liveCarrier
                : PickWeightedCarrier(setup.away.startingEleven);
            PlayerData defender = PickDefender(setup.home.startingEleven, setup.home.formation);

            currentMoment = new DecisiveMoment(DecisiveMomentType.Defense, defender, threat, setup.away.attack, setup.away.midfield, setup.away.defense)
            {
                difficultyMultiplier = difficultyMultiplier,
                fieldProgress = fieldProgress,
                nearbyDefenderCount = nearbyDefenders
            };

            ShowDefensePanel(currentMoment);
        }

        playbackController.Pause();
        Time.timeScale = MatchSpeed.Normal * slowMotionScale;
    }

    private void ShowAttackPanel(DecisiveMoment moment)
    {
        contextText.text = BuildAttackSituationText(moment);

        DecisiveAction[] options = BuildAttackOptions(moment);
        SetButtonAction(option1Button, option1Label, moment, options[0]);
        SetButtonAction(option2Button, option2Label, moment, options[1]);
        SetButtonAction(option3Button, option3Label, moment, options[2]);

        resultPopup.SetActive(false);
        decisivePanel.SetActive(true);
    }

    private void ShowDefensePanel(DecisiveMoment moment)
    {
        contextText.text = BuildDefenseSituationText(moment);

        DecisiveAction[] options = BuildDefenseOptions(moment);
        SetButtonAction(option1Button, option1Label, moment, options[0]);
        SetButtonAction(option2Button, option2Label, moment, options[1]);
        SetButtonAction(option3Button, option3Label, moment, options[2]);

        resultPopup.SetActive(false);
        decisivePanel.SetActive(true);
    }

    // Which 3 actions actually make sense changes with the situation - a
    // clear chance right in front of goal offers a genuinely different menu
    // than a deep build-up where shooting isn't realistic, instead of
    // always presenting the same fixed Shoot/Pass/Dribble trio regardless
    // of context.
    private DecisiveAction[] BuildAttackOptions(DecisiveMoment moment)
    {
        if (moment.fieldProgress >= 0.78f && moment.nearbyDefenderCount == 0)
        {
            return new[] { DecisiveAction.Shoot, DecisiveAction.ThroughBall, DecisiveAction.Dribble };
        }

        if (moment.fieldProgress >= 0.78f)
        {
            return new[] { DecisiveAction.Shoot, DecisiveAction.Pass, DecisiveAction.Dribble };
        }

        if (moment.fieldProgress >= 0.45f)
        {
            return new[] { DecisiveAction.Shoot, DecisiveAction.Pass, DecisiveAction.ThroughBall };
        }

        return new[] { DecisiveAction.LongBall, DecisiveAction.Pass, DecisiveAction.Dribble };
    }

    private DecisiveAction[] BuildDefenseOptions(DecisiveMoment moment)
    {
        if (moment.fieldProgress >= 0.78f)
        {
            return new[] { DecisiveAction.Block, DecisiveAction.Tackle, DecisiveAction.Press };
        }

        if (moment.nearbyDefenderCount <= 1)
        {
            return new[] { DecisiveAction.Cover, DecisiveAction.Tackle, DecisiveAction.Press };
        }

        return new[] { DecisiveAction.Tackle, DecisiveAction.Press, DecisiveAction.Cover };
    }

    // Narrates what's actually happening (zone, defenders in the way) and
    // gives a plain read on the situation, instead of a generic "has the
    // ball" line that never changes no matter how dangerous the moment is.
    private string BuildAttackSituationText(DecisiveMoment moment)
    {
        string zone = GetZoneLabel(moment.fieldProgress);
        string defenderLine = GetDefenderLine(moment.nearbyDefenderCount);
        string advice = moment.nearbyDefenderCount >= 2
            ? "SHOOTING LOOKS RISKY HERE - A PASS MAY BE SAFER."
            : moment.nearbyDefenderCount == 0 && moment.fieldProgress >= 0.6f
                ? "THIS IS A CLEAR CHANCE TO SHOOT!"
                : "READ THE DEFENSE BEFORE YOU DECIDE.";

        return moment.actor.playerName.ToUpperInvariant() + " " + zone +
            "\n" + defenderLine +
            "\n" + advice;
    }

    private string BuildDefenseSituationText(DecisiveMoment moment)
    {
        string zone = GetZoneLabel(moment.fieldProgress);
        string supportLine = moment.nearbyDefenderCount <= 1
            ? "YOU'RE ISOLATED - LITTLE SUPPORT NEARBY."
            : moment.nearbyDefenderCount + " DEFENDERS ARE CLOSE ENOUGH TO HELP.";

        return moment.opponent.playerName.ToUpperInvariant() + " " + zone +
            "\n" + supportLine;
    }

    private string GetZoneLabel(float fieldProgress)
    {
        if (fieldProgress >= 0.78f)
        {
            return "IS IN THE BOX!";
        }

        if (fieldProgress >= 0.5f)
        {
            return "IS IN THE FINAL THIRD";
        }

        return "IS BUILDING UP PLAY";
    }

    private string GetDefenderLine(int defenderCount)
    {
        if (defenderCount <= 0)
        {
            return "NO DEFENDERS IN THE WAY.";
        }

        if (defenderCount == 1)
        {
            return "1 DEFENDER IN FRONT OF HIM.";
        }

        return defenderCount + " DEFENDERS IN FRONT OF HIM.";
    }

    private void SetButtonAction(Button button, TMP_Text label, DecisiveMoment moment, DecisiveAction action)
    {
        int oddsPercent = Mathf.RoundToInt(MatchDecisiveMomentResolver.PreviewChance(moment, action) * 100f);
        label.text = GetActionLabel(action) + " (" + oddsPercent + "%)";
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => ResolveChoice(action));
    }

    private string GetActionLabel(DecisiveAction action)
    {
        switch (action)
        {
            case DecisiveAction.Shoot: return "SHOOT";
            case DecisiveAction.Pass: return "PASS";
            case DecisiveAction.Dribble: return "DRIBBLE";
            case DecisiveAction.ThroughBall: return "THROUGH BALL";
            case DecisiveAction.LongBall: return "LONG BALL";
            case DecisiveAction.Tackle: return "TACKLE";
            case DecisiveAction.Block: return "BLOCK";
            case DecisiveAction.Press: return "PRESS";
            case DecisiveAction.Cover: return "COVER";
        }

        return action.ToString().ToUpperInvariant();
    }

    private void ResolveChoice(DecisiveAction action)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayDecisiveAction(action);
        }

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
        lastMomentEndTime = Time.time;

        if (matchActive)
        {
            ScheduleNextMoment();
            playbackController.Resume();
        }
    }

    private PlayerData FindGoalkeeper(List<PlayerData> players, string formation)
    {
        for (int i = 0; i < players.Count; i++)
        {
            if (MatchPitchLayout.GetCategoryForIndex(players[i].position, formation, i) == "GK")
            {
                return players[i];
            }
        }

        return players[0];
    }

    private PlayerData PickDefender(List<PlayerData> players, string formation)
    {
        List<PlayerData> defenders = new List<PlayerData>();

        for (int i = 0; i < players.Count; i++)
        {
            if (MatchPitchLayout.GetCategoryForIndex(players[i].position, formation, i) == "DEF")
            {
                defenders.Add(players[i]);
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
