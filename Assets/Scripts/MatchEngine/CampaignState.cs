using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum CampaignStage
{
    Friendlies,
    WorldCup,
    Final,
    Completed
}

public class FixtureRecord
{
    public NationalTeamData opponent;
    public bool played;
    public int homeScore;
    public int awayScore;
}

// One of the 3 "background" matches between the two opponents NOT playing
// Jordan that matchday - simulated instantly (no player input) so the
// group table reflects a real 4-team round robin, not just Jordan's record.
public class GroupMatchRecord
{
    public NationalTeamData teamA;
    public NationalTeamData teamB;
    public bool played;
    public int scoreA;
    public int scoreB;
}

public class GroupStandingRow
{
    public string teamName;
    public int played;
    public int wins;
    public int draws;
    public int losses;
    public int goalsFor;
    public int goalsAgainst;

    public int Points => wins * 3 + draws;
    public int GoalDifference => goalsFor - goalsAgainst;
}

// Lives in CampaignScene, persists across the FormationScene/MatchDayScene
// round trip (same DontDestroyOnLoad singleton pattern as TeamManager and
// MatchSession) so fixture progress survives each match. Tracks 3 friendly
// fixtures, then a 4-team World Cup group (Jordan + 3 opponents) played as
// a real round robin. The top 2 in the table advance to a one-off Final;
// if Jordan isn't top 2, the campaign ends in elimination instead. Losses
// during Friendlies/the group stage never block progress - only finishing
// outside the top 2 does.
public class CampaignState : MonoBehaviour
{
    private static CampaignState instance;
    public static CampaignState Instance => instance;

    [Header("Friendly opponents (in order)")]
    public NationalTeamData[] friendlyOpponents = new NationalTeamData[3];

    [Header("World Cup group opponents (in order)")]
    public NationalTeamData[] worldCupOpponents = new NationalTeamData[3];

    public CampaignStage Stage { get; private set; } = CampaignStage.Friendlies;
    public List<FixtureRecord> Friendlies { get; private set; }
    public List<FixtureRecord> WorldCup { get; private set; }
    public List<FixtureRecord> Final { get; private set; }
    public List<GroupMatchRecord> WorldCupBackgroundMatches { get; private set; }
    public string CompletionMessage { get; private set; } = "";

    private CampaignStage launchedStage;
    private int launchedIndex = -1;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        if (Friendlies == null)
        {
            Friendlies = BuildFixtures(friendlyOpponents);
            WorldCup = BuildFixtures(worldCupOpponents);
            WorldCupBackgroundMatches = BuildBackgroundMatches(worldCupOpponents);
        }
    }

    private List<FixtureRecord> BuildFixtures(NationalTeamData[] opponents)
    {
        List<FixtureRecord> list = new List<FixtureRecord>();

        foreach (NationalTeamData opponent in opponents)
        {
            list.Add(new FixtureRecord { opponent = opponent });
        }

        return list;
    }

    // For matchday i (Jordan vs opponents[i]), the background match pairs
    // the two opponents NOT playing Jordan that matchday.
    private List<GroupMatchRecord> BuildBackgroundMatches(NationalTeamData[] opponents)
    {
        List<GroupMatchRecord> list = new List<GroupMatchRecord>();

        for (int i = 0; i < opponents.Length; i++)
        {
            list.Add(new GroupMatchRecord
            {
                teamA = opponents[(i + 1) % opponents.Length],
                teamB = opponents[(i + 2) % opponents.Length]
            });
        }

        return list;
    }

    public List<FixtureRecord> GetCurrentStageFixtures()
    {
        return GetFixtureList(Stage);
    }

    private List<FixtureRecord> GetFixtureList(CampaignStage stage)
    {
        switch (stage)
        {
            case CampaignStage.Friendlies: return Friendlies;
            case CampaignStage.WorldCup: return WorldCup;
            case CampaignStage.Final: return Final;
            default: return null;
        }
    }

    public void LaunchFixture(CampaignStage stage, int index)
    {
        launchedStage = stage;
        launchedIndex = index;

        List<FixtureRecord> fixtures = GetFixtureList(stage);
        NationalTeamData opponent = fixtures[index].opponent;

        MatchSession session = MatchSession.GetOrCreate();
        session.SetPendingOpponentTeam(opponent);
        session.SetPendingIsKnockout(stage == CampaignStage.Final);

        SceneManager.LoadScene("FormationScene");
    }

    public void RecordResult(int homeScore, int awayScore, bool homeWonOverall)
    {
        if (launchedIndex < 0)
        {
            return;
        }

        CampaignStage stagePlayed = launchedStage;
        int indexPlayed = launchedIndex;

        List<FixtureRecord> fixtures = GetFixtureList(stagePlayed);
        FixtureRecord fixture = fixtures[indexPlayed];
        fixture.played = true;
        fixture.homeScore = homeScore;
        fixture.awayScore = awayScore;

        launchedIndex = -1;

        if (stagePlayed == CampaignStage.WorldCup)
        {
            SimulateBackgroundMatchIfNeeded(indexPlayed);
        }

        if (Stage == CampaignStage.Friendlies && AllPlayed(Friendlies))
        {
            Stage = CampaignStage.WorldCup;
        }
        else if (Stage == CampaignStage.WorldCup && AllPlayed(WorldCup))
        {
            AdvanceFromGroupStage();
        }
        else if (Stage == CampaignStage.Final && AllPlayed(Final))
        {
            CompletionMessage = homeWonOverall
                ? "CHAMPIONS! JORDAN WINS THE WORLD CUP FINAL"
                : "RUNNERS-UP - LOST THE FINAL";

            Stage = CampaignStage.Completed;
        }
    }

    private void AdvanceFromGroupStage()
    {
        List<GroupStandingRow> standings = GetGroupStandings();
        bool jordanQualifies = standings.Count >= 2 &&
            (standings[0].teamName == "JORDAN" || standings[1].teamName == "JORDAN");

        if (!jordanQualifies)
        {
            CompletionMessage = "ELIMINATED - DID NOT FINISH TOP 2 IN THE GROUP";
            Stage = CampaignStage.Completed;
            return;
        }

        string otherQualifierName = standings[0].teamName == "JORDAN" ? standings[1].teamName : standings[0].teamName;
        NationalTeamData finalOpponent = FindOpponentByDisplayName(otherQualifierName);

        Final = new List<FixtureRecord> { new FixtureRecord { opponent = finalOpponent } };
        Stage = CampaignStage.Final;
    }

    private NationalTeamData FindOpponentByDisplayName(string displayName)
    {
        foreach (NationalTeamData team in worldCupOpponents)
        {
            if (DisplayName(team) == displayName)
            {
                return team;
            }
        }

        return null;
    }

    private void SimulateBackgroundMatchIfNeeded(int matchdayIndex)
    {
        GroupMatchRecord match = WorldCupBackgroundMatches[matchdayIndex];

        if (match.played || match.teamA == null || match.teamB == null)
        {
            return;
        }

        match.scoreA = SimulateExpectedGoals(match.teamA, match.teamB);
        match.scoreB = SimulateExpectedGoals(match.teamB, match.teamA);
        match.played = true;
    }

    private int SimulateExpectedGoals(NationalTeamData attackingTeam, NationalTeamData defendingTeam)
    {
        float attackStrength = (attackingTeam.attack + attackingTeam.midfield) / 2f;
        float expectedGoals = Mathf.Clamp((attackStrength - defendingTeam.defense) * 0.04f + 1.2f, 0.2f, 4f);

        return Mathf.Clamp(Mathf.RoundToInt(Random.Range(0f, expectedGoals * 2f)), 0, 6);
    }

    public List<GroupStandingRow> GetGroupStandings()
    {
        Dictionary<string, GroupStandingRow> rows = new Dictionary<string, GroupStandingRow>();

        GroupStandingRow jordanRow = GetOrCreateRow(rows, "JORDAN");

        for (int i = 0; i < WorldCup.Count; i++)
        {
            FixtureRecord fixture = WorldCup[i];

            if (!fixture.played)
            {
                continue;
            }

            GroupStandingRow opponentRow = GetOrCreateRow(rows, DisplayName(fixture.opponent));
            ApplyResult(jordanRow, opponentRow, fixture.homeScore, fixture.awayScore);
        }

        foreach (GroupMatchRecord match in WorldCupBackgroundMatches)
        {
            if (!match.played)
            {
                continue;
            }

            GroupStandingRow rowA = GetOrCreateRow(rows, DisplayName(match.teamA));
            GroupStandingRow rowB = GetOrCreateRow(rows, DisplayName(match.teamB));
            ApplyResult(rowA, rowB, match.scoreA, match.scoreB);
        }

        List<GroupStandingRow> standings = new List<GroupStandingRow>(rows.Values);
        standings.Sort((a, b) =>
        {
            if (b.Points != a.Points) return b.Points.CompareTo(a.Points);
            if (b.GoalDifference != a.GoalDifference) return b.GoalDifference.CompareTo(a.GoalDifference);
            return b.goalsFor.CompareTo(a.goalsFor);
        });

        return standings;
    }

    private GroupStandingRow GetOrCreateRow(Dictionary<string, GroupStandingRow> rows, string teamName)
    {
        if (!rows.TryGetValue(teamName, out GroupStandingRow row))
        {
            row = new GroupStandingRow { teamName = teamName };
            rows[teamName] = row;
        }

        return row;
    }

    private void ApplyResult(GroupStandingRow rowA, GroupStandingRow rowB, int scoreA, int scoreB)
    {
        rowA.played++;
        rowB.played++;
        rowA.goalsFor += scoreA;
        rowA.goalsAgainst += scoreB;
        rowB.goalsFor += scoreB;
        rowB.goalsAgainst += scoreA;

        if (scoreA > scoreB)
        {
            rowA.wins++;
            rowB.losses++;
        }
        else if (scoreA < scoreB)
        {
            rowB.wins++;
            rowA.losses++;
        }
        else
        {
            rowA.draws++;
            rowB.draws++;
        }
    }

    private string DisplayName(NationalTeamData team)
    {
        if (team == null)
        {
            return "?";
        }

        return string.IsNullOrWhiteSpace(team.teamName) ? team.name.ToUpperInvariant() : team.teamName.ToUpperInvariant();
    }

    private bool AllPlayed(List<FixtureRecord> fixtures)
    {
        foreach (FixtureRecord fixture in fixtures)
        {
            if (!fixture.played)
            {
                return false;
            }
        }

        return true;
    }
}
