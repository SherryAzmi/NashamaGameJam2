using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Lives in CampaignScene. Shows the 6 friendly fixtures first; once all 6
// are played, a one-time World Cup Draw reveal screen shows the 16-team
// knockout bracket, then this same screen switches to showing only the
// current round's Jordan match (Round of 16 -> Quarterfinal -> Semifinal ->
// Final) until the campaign ends.
public class CampaignHubController : MonoBehaviour
{
    [Header("Common")]
    public TMP_Text titleText;

    [Header("World Cup draw reveal / bracket recap")]
    public WorldCupDrawController worldCupDrawController;
    public WorldCupStandingsController worldCupStandingsController;

    [Tooltip("Permanent buttons visible once the World Cup has started, to reopen the bracket or standings panel on demand.")]
    public GameObject viewBracketButton;
    public GameObject viewStandingsButton;

    [Header("Fixture rows (reused for friendlies or the current knockout round)")]
    public GameObject[] fixtureRows;
    public TMP_Text[] fixtureNameTexts;
    public TMP_Text[] fixtureStatusTexts;
    public Button[] fixturePlayButtons;
    public Image[] fixtureFlagImages;

    [Header("Completion")]
    public GameObject completionPanel;
    public TMP_Text completionText;

    private void Start()
    {
        if (viewBracketButton != null)
        {
            GetComponentForButton(viewBracketButton).onClick.AddListener(ShowBracketOnDemand);
        }

        if (viewStandingsButton != null)
        {
            GetComponentForButton(viewStandingsButton).onClick.AddListener(ShowStandingsOnDemand);
        }

        Render();
    }

    private Button GetComponentForButton(GameObject buttonObject)
    {
        return buttonObject.GetComponent<Button>();
    }

    private void ShowBracketOnDemand()
    {
        CampaignState state = CampaignState.Instance;

        if (state == null || worldCupDrawController == null)
        {
            return;
        }

        SetFixtureRowsActive(false);
        titleText.text = "";
        worldCupDrawController.gameObject.SetActive(true);
        worldCupDrawController.Show(state, this);
    }

    private void ShowStandingsOnDemand()
    {
        CampaignState state = CampaignState.Instance;

        if (state == null || worldCupStandingsController == null)
        {
            return;
        }

        SetFixtureRowsActive(false);
        titleText.text = "";
        worldCupStandingsController.Show(state, this);
    }

    public void Render()
    {
        CampaignState state = CampaignState.Instance;

        if (state == null)
        {
            Debug.LogError("CampaignHubController: no CampaignState found in scene.");
            return;
        }

        completionPanel.SetActive(false);

        bool worldCupStarted = state.Stage != CampaignStage.Friendlies;

        if (viewBracketButton != null)
        {
            viewBracketButton.SetActive(worldCupStarted);
        }

        if (viewStandingsButton != null)
        {
            viewStandingsButton.SetActive(worldCupStarted);
        }

        // Auto-show the bracket recap right after the initial draw or any
        // round just finishing (including elimination/championship) -
        // dismissing it (its own Continue button) re-renders into the
        // normal view below.
        bool showRecap = state.Stage == CampaignStage.WorldCupDrawPending ||
            (state.BracketRecapPending && state.Bracket.Count > 0);

        if (showRecap)
        {
            SetFixtureRowsActive(false);
            titleText.text = "";
            worldCupDrawController.gameObject.SetActive(true);
            worldCupDrawController.Show(state, this);
            return;
        }

        worldCupDrawController.gameObject.SetActive(false);

        if (state.Stage == CampaignStage.Completed)
        {
            ShowCompletion(state);
            return;
        }

        if (state.Stage == CampaignStage.Friendlies)
        {
            titleText.text = "FRIENDLY MATCHES";
            RenderFriendlies(state);
        }
        else
        {
            titleText.text = "WORLD CUP - " + state.GetCurrentRound().roundName.ToUpperInvariant();
            RenderKnockoutRound(state);
        }
    }

    private void RenderFriendlies(CampaignState state)
    {
        List<FixtureRecord> fixtures = state.Friendlies;
        int nextPlayableIndex = -1;

        for (int i = 0; i < fixtures.Count; i++)
        {
            if (!fixtures[i].played)
            {
                nextPlayableIndex = i;
                break;
            }
        }

        for (int i = 0; i < fixtureRows.Length; i++)
        {
            if (i >= fixtures.Count)
            {
                fixtureRows[i].SetActive(false);
                continue;
            }

            fixtureRows[i].SetActive(true);

            FixtureRecord fixture = fixtures[i];
            SetRowContent(i, fixture.opponent, fixture.played, fixture.homeScore, fixture.awayScore);

            bool isPlayable = i == nextPlayableIndex;
            fixturePlayButtons[i].gameObject.SetActive(isPlayable);

            if (isPlayable)
            {
                int capturedIndex = i;

                fixturePlayButtons[i].onClick.RemoveAllListeners();
                fixturePlayButtons[i].onClick.AddListener(() => state.LaunchFixture(CampaignStage.Friendlies, capturedIndex));
            }
        }
    }

    // Knockout rounds only ever have one playable match (Jordan's) - every
    // other match in the round is simulated instantly once Jordan's match
    // is recorded, so there is never a "next fixture" to pick from. Reuses
    // fixture row 0 as the single "your match this round" row.
    private void RenderKnockoutRound(CampaignState state)
    {
        for (int i = 1; i < fixtureRows.Length; i++)
        {
            fixtureRows[i].SetActive(false);
        }

        BracketMatch jordanMatch = state.GetJordanMatchInCurrentRound();

        if (jordanMatch == null)
        {
            fixtureRows[0].SetActive(false);
            return;
        }

        fixtureRows[0].SetActive(true);

        NationalTeamData opponent = jordanMatch.teamA != null && jordanMatch.teamA.name == "Jordan"
            ? jordanMatch.teamB
            : jordanMatch.teamA;

        SetRowContent(0, opponent, jordanMatch.played, jordanMatch.scoreA, jordanMatch.scoreB);

        fixturePlayButtons[0].gameObject.SetActive(!jordanMatch.played);

        if (!jordanMatch.played)
        {
            fixturePlayButtons[0].onClick.RemoveAllListeners();
            fixturePlayButtons[0].onClick.AddListener(state.LaunchBracketMatch);
        }
    }

    private void SetRowContent(int rowIndex, NationalTeamData opponent, bool played, int homeScore, int awayScore)
    {
        string opponentName = GetOpponentDisplayName(opponent);

        fixtureNameTexts[rowIndex].text = "VS " + opponentName.ToUpperInvariant();
        fixtureStatusTexts[rowIndex].text = played ? ResultLabel(homeScore, awayScore) : "NOT PLAYED";

        if (fixtureFlagImages != null && rowIndex < fixtureFlagImages.Length && fixtureFlagImages[rowIndex] != null)
        {
            Sprite flag = opponent != null ? opponent.flag : null;
            fixtureFlagImages[rowIndex].sprite = flag;
            fixtureFlagImages[rowIndex].enabled = flag != null;
        }
    }

    private void SetFixtureRowsActive(bool active)
    {
        for (int i = 0; i < fixtureRows.Length; i++)
        {
            fixtureRows[i].SetActive(active);
        }
    }

    private string GetOpponentDisplayName(NationalTeamData opponent)
    {
        if (opponent == null)
        {
            return "?";
        }

        return string.IsNullOrWhiteSpace(opponent.teamName) ? opponent.name : opponent.teamName;
    }

    private string ResultLabel(int homeScore, int awayScore)
    {
        if (homeScore > awayScore)
        {
            return "WON " + homeScore + "-" + awayScore;
        }

        if (homeScore < awayScore)
        {
            return "LOST " + homeScore + "-" + awayScore;
        }

        return "DRAW " + homeScore + "-" + awayScore;
    }

    private void ShowCompletion(CampaignState state)
    {
        titleText.text = "WORLD CUP";

        SetFixtureRowsActive(false);
        worldCupDrawController.gameObject.SetActive(false);

        completionPanel.SetActive(true);
        completionText.text = state.CompletionMessage;
    }
}
