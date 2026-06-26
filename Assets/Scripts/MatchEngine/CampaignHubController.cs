using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Lives in CampaignScene. Shows the 3 friendly fixtures first; once all 3
// are played, the same screen switches to showing the 3 World Cup group
// fixtures plus a live 4-team standings table instead. Only the next
// unplayed fixture is playable - matches are strictly sequential, like a
// real schedule.
public class CampaignHubController : MonoBehaviour
{
    [Header("Common")]
    public TMP_Text titleText;

    [Header("Fixture rows (reused for friendlies or World Cup)")]
    public GameObject[] fixtureRows;
    public TMP_Text[] fixtureNameTexts;
    public TMP_Text[] fixtureStatusTexts;
    public Button[] fixturePlayButtons;

    [Header("Group standings (World Cup stage only)")]
    public GameObject standingsPanel;
    public TMP_Text[] standingsRowTexts;

    [Header("Completion")]
    public GameObject completionPanel;
    public TMP_Text completionText;

    private void Start()
    {
        Render();
    }

    private void Render()
    {
        CampaignState state = CampaignState.Instance;

        if (state == null)
        {
            Debug.LogError("CampaignHubController: no CampaignState found in scene.");
            return;
        }

        completionPanel.SetActive(false);

        if (state.Stage == CampaignStage.Completed)
        {
            ShowCompletion(state);
            return;
        }

        List<FixtureRecord> fixtures = state.GetCurrentStageFixtures();
        bool isWorldCup = state.Stage == CampaignStage.WorldCup;
        bool isFinal = state.Stage == CampaignStage.Final;

        if (isFinal)
        {
            titleText.text = "WORLD CUP - FINAL";
        }
        else
        {
            titleText.text = isWorldCup ? "WORLD CUP - GROUP STAGE" : "FRIENDLY MATCHES";
        }

        standingsPanel.SetActive(isWorldCup);

        if (isWorldCup)
        {
            RenderStandings(state);
        }

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
            string opponentName = GetOpponentDisplayName(fixture.opponent);

            fixtureNameTexts[i].text = "VS " + opponentName.ToUpperInvariant();
            fixtureStatusTexts[i].text = fixture.played ? ResultLabel(fixture) : "NOT PLAYED";

            bool isPlayable = i == nextPlayableIndex;
            fixturePlayButtons[i].gameObject.SetActive(isPlayable);

            if (isPlayable)
            {
                int capturedIndex = i;
                CampaignStage capturedStage = state.Stage;

                fixturePlayButtons[i].onClick.RemoveAllListeners();
                fixturePlayButtons[i].onClick.AddListener(() => state.LaunchFixture(capturedStage, capturedIndex));
            }
        }
    }

    private void RenderStandings(CampaignState state)
    {
        List<GroupStandingRow> standings = state.GetGroupStandings();

        for (int i = 0; i < standingsRowTexts.Length; i++)
        {
            if (i >= standings.Count)
            {
                standingsRowTexts[i].text = "";
                continue;
            }

            GroupStandingRow row = standings[i];
            string gdLabel = row.GoalDifference > 0 ? "+" + row.GoalDifference : row.GoalDifference.ToString();

            standingsRowTexts[i].text =
                $"{i + 1}. {row.teamName}   P{row.played}  {row.wins}W {row.draws}D {row.losses}L  GD{gdLabel}  PTS {row.Points}";
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

    private string ResultLabel(FixtureRecord fixture)
    {
        if (fixture.homeScore > fixture.awayScore)
        {
            return "WON " + fixture.homeScore + "-" + fixture.awayScore;
        }

        if (fixture.homeScore < fixture.awayScore)
        {
            return "LOST " + fixture.homeScore + "-" + fixture.awayScore;
        }

        return "DRAW " + fixture.homeScore + "-" + fixture.awayScore;
    }

    private void ShowCompletion(CampaignState state)
    {
        titleText.text = "WORLD CUP";

        for (int i = 0; i < fixtureRows.Length; i++)
        {
            fixtureRows[i].SetActive(false);
        }

        standingsPanel.SetActive(true);
        RenderStandings(state);

        completionPanel.SetActive(true);
        completionText.text = state.CompletionMessage;
    }
}
