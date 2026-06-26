using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Penalty shootout, used when a knockout/final match is drawn after full
// time: 5 kicks each, sudden death after that if still level. You choose
// the angle (Left/Center/Right) for Jordan's kicks; the opponent's kicks
// resolve automatically. Each kick's success chance comes from the
// shooter's shoot stat vs the keeper's defense stat, with a bonus if the
// shooter picks a different angle than the keeper's guess.
public class MatchPenaltyController : MonoBehaviour
{
    [Header("UI")]
    public GameObject panel;
    public TMP_Text scoreText;
    public TMP_Text contextText;
    public Button leftButton;
    public Button centerButton;
    public Button rightButton;
    public GameObject resultPopup;
    public TMP_Text resultPopupText;

    public float resultPopupDuration = 1.2f;

    private MatchSetup setup;
    private int homeScore;
    private int awayScore;
    private int round;
    private const int RegulationRounds = 5;
    private System.Action<int, int> onComplete;

    public void BeginShootout(MatchSetup matchSetup, System.Action<int, int> onShootoutComplete)
    {
        setup = matchSetup;
        onComplete = onShootoutComplete;
        homeScore = 0;
        awayScore = 0;
        round = 1;

        panel.SetActive(true);
        resultPopup.SetActive(false);

        StartHomeKick();
    }

    private void StartHomeKick()
    {
        UpdateScoreText();
        contextText.text = "ROUND " + round + " - CHOOSE YOUR PENALTY ANGLE";

        SetButtonAction(leftButton, () => TakeHomeKick(0));
        SetButtonAction(centerButton, () => TakeHomeKick(1));
        SetButtonAction(rightButton, () => TakeHomeKick(2));

        panel.SetActive(true);
    }

    private void SetButtonAction(Button button, UnityEngine.Events.UnityAction action)
    {
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    private void TakeHomeKick(int angleIndex)
    {
        PlayerData shooter = PickShooter(setup.home.startingEleven);
        PlayerData keeper = FindGoalkeeper(setup.away.startingEleven);

        int keeperGuess = Random.Range(0, 3);
        bool scored = ResolveKick(shooter.shoot, keeper.defense, angleIndex == keeperGuess);

        if (scored)
        {
            homeScore++;
        }

        ShowResult(scored ? shooter.playerName + " SCORES!" : keeper.playerName + " SAVES IT!", StartAwayKick);
    }

    private void StartAwayKick()
    {
        PlayerData shooter = PickShooter(setup.away.startingEleven);
        PlayerData keeper = FindGoalkeeper(setup.home.startingEleven);

        int shooterAngle = Random.Range(0, 3);
        int keeperGuess = Random.Range(0, 3);
        bool scored = ResolveKick(shooter.shoot, keeper.defense, shooterAngle == keeperGuess);

        if (scored)
        {
            awayScore++;
        }

        ShowResult(scored ? shooter.playerName + " SCORES!" : keeper.playerName + " SAVES IT!", AfterRound);
    }

    // After round 5, this is effectively sudden death: one kick each per
    // round, stopping as soon as the scores differ.
    private void AfterRound()
    {
        bool decided = round >= RegulationRounds && homeScore != awayScore;

        if (decided)
        {
            FinishShootout();
            return;
        }

        round++;
        StartHomeKick();
    }

    private void FinishShootout()
    {
        panel.SetActive(false);
        resultPopup.SetActive(false);
        onComplete?.Invoke(homeScore, awayScore);
    }

    private bool ResolveKick(int shooterShoot, int keeperDefense, bool guessedRight)
    {
        float chance = 0.72f + (shooterShoot - keeperDefense) * 0.004f;
        chance *= guessedRight ? 0.55f : 1.1f;
        chance = Mathf.Clamp(chance, 0.25f, 0.95f);

        return Random.value < chance;
    }

    private void ShowResult(string message, System.Action onContinue)
    {
        panel.SetActive(false);
        resultPopup.SetActive(true);
        resultPopupText.text = message;
        UpdateScoreText();

        StartCoroutine(ContinueAfterPopup(onContinue));
    }

    private IEnumerator ContinueAfterPopup(System.Action onContinue)
    {
        yield return new WaitForSecondsRealtime(resultPopupDuration);

        resultPopup.SetActive(false);
        onContinue?.Invoke();
    }

    private void UpdateScoreText()
    {
        if (scoreText != null)
        {
            scoreText.text = "PENALTIES: JORDAN " + homeScore + " - " + awayScore + " " + setup.away.teamName.ToUpperInvariant();
        }
    }

    private PlayerData PickShooter(List<PlayerData> players)
    {
        List<PlayerData> candidates = new List<PlayerData>();

        foreach (PlayerData player in players)
        {
            if (MatchPitchLayout.GetCategory(player.position) != "GK")
            {
                candidates.Add(player);
            }
        }

        if (candidates.Count == 0)
        {
            candidates = players;
        }

        return candidates[Random.Range(0, candidates.Count)];
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
}
