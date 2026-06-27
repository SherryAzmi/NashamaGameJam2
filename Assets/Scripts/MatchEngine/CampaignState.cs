using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum CampaignStage
{
    Friendlies,
    WorldCupDrawPending,
    RoundOf16,
    Quarterfinal,
    Semifinal,
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

// Lives in CampaignScene, persists across the FormationScene/MatchDayScene
// round trip (same DontDestroyOnLoad singleton pattern as TeamManager and
// MatchSession), and is durable across app restarts via SaveManager. Tracks
// 6 randomly-drawn friendly fixtures, then a 16-team single-elimination
// World Cup knockout (Round of 16 -> Quarterfinal -> Semifinal -> Final).
// Jordan only ever plays its own match each round; every other match in
// that round is simulated instantly the moment Jordan's match is recorded,
// so the bracket fills in round by round.
public class CampaignState : MonoBehaviour
{
    private static CampaignState instance;
    public static CampaignState Instance => instance;

    private const int FriendlyCount = 6;
    private const int KnockoutTeamCount = 16;
    private const string JordanAssetName = "Jordan";

    [Header("Team pool (all 48 national teams)")]
    public NationalTeamDatabase teamDatabase;

    public CampaignStage Stage { get; private set; } = CampaignStage.Friendlies;
    public List<FixtureRecord> Friendlies { get; private set; }
    public List<BracketRound> Bracket { get; private set; } = new List<BracketRound>();
    public List<NationalTeamData> WorldCupField { get; private set; } = new List<NationalTeamData>();
    public int CurrentBracketRoundIndex { get; private set; }
    public bool WorldCupDrawRevealShown { get; private set; }

    // True whenever there's a bracket update (the initial draw, or a round
    // just finished) the player hasn't seen the recap panel for yet. The
    // hub shows the bracket panel automatically while this is true, and a
    // permanent "View Bracket" button can also open it on demand any time.
    public bool BracketRecapPending { get; private set; }

    public string CompletionMessage { get; private set; } = "";

    private CampaignStage launchedStage;
    private int launchedIndex = -1;

    // Set right before loading FormationScene from the campaign hub, and
    // consumed (reset to false) by FormationScene on read so it only ever
    // reflects the most recent scene load.
    public static bool EnteredFormationFromCampaign { get; private set; }

    // Reads and clears the flag so only the very next FormationScene load sees it.
    public static bool ConsumeEnteredFromCampaign()
    {
        bool value = EnteredFormationFromCampaign;
        EnteredFormationFromCampaign = false;
        return value;
    }

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
            if (!RestoreFromSave())
            {
                Friendlies = BuildRandomFriendlyOpponents();
            }
        }
    }

    // --- Friendly draw -------------------------------------------------

    private List<FixtureRecord> BuildRandomFriendlyOpponents()
    {
        List<NationalTeamData> pool = teamDatabase.teams
            .Where(team => team != null && team.name != JordanAssetName)
            .ToList();

        Shuffle(pool);

        List<FixtureRecord> fixtures = new List<FixtureRecord>();

        for (int i = 0; i < FriendlyCount && i < pool.Count; i++)
        {
            fixtures.Add(new FixtureRecord { opponent = pool[i] });
        }

        return fixtures;
    }

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    public List<FixtureRecord> GetCurrentStageFixtures()
    {
        return Stage == CampaignStage.Friendlies ? Friendlies : null;
    }

    // --- World Cup draw (16 teams, seeded pots, random pairing) --------

    private void GenerateWorldCupDraw()
    {
        NationalTeamData jordan = teamDatabase.teams.Find(team => team != null && team.name == JordanAssetName);

        HashSet<string> usedNames = Friendlies
            .Select(fixture => fixture.opponent != null ? fixture.opponent.name : null)
            .Where(name => name != null)
            .ToHashSet();

        usedNames.Add(JordanAssetName);

        List<NationalTeamData> candidates = teamDatabase.teams
            .Where(team => team != null && !usedNames.Contains(team.name))
            .ToList();

        List<NationalTeamData> drawnOpponents = PickSeededOpponents(candidates, KnockoutTeamCount - 1);

        List<NationalTeamData> fieldOf16 = new List<NationalTeamData> { jordan };
        fieldOf16.AddRange(drawnOpponents);
        fieldOf16 = fieldOf16.OrderByDescending(team => team.Overall).ToList();

        List<NationalTeamData> pot1 = fieldOf16.GetRange(0, 4);
        List<NationalTeamData> pot2 = fieldOf16.GetRange(4, 4);
        List<NationalTeamData> pot3 = fieldOf16.GetRange(8, 4);
        List<NationalTeamData> pot4 = fieldOf16.GetRange(12, 4);

        List<BracketMatch> matches = new List<BracketMatch>();
        matches.AddRange(PairPots(pot1, pot4));
        matches.AddRange(PairPots(pot2, pot3));
        Shuffle(matches);

        foreach (BracketMatch match in matches)
        {
            match.isJordanMatch = match.teamA == jordan || match.teamB == jordan;
        }

        Bracket = new List<BracketRound>
        {
            new BracketRound { roundName = "Round of 16", matches = matches }
        };

        WorldCupField = fieldOf16;
        CurrentBracketRoundIndex = 0;
        WorldCupDrawRevealShown = false;
        BracketRecapPending = true;
        Stage = CampaignStage.WorldCupDrawPending;

        SaveManager.Instance?.SaveCurrentState();
    }

    // Picks `count` opponents from candidates, biased toward spreading
    // across the full strength range (4 roughly-even tiers by Overall) so
    // Jordan's World Cup field is not just the globally strongest teams
    // every campaign, while still being a genuinely random draw each run.
    private List<NationalTeamData> PickSeededOpponents(List<NationalTeamData> candidates, int count)
    {
        List<NationalTeamData> sorted = candidates.OrderByDescending(team => team.Overall).ToList();
        int tierSize = Mathf.CeilToInt(sorted.Count / 4f);

        List<List<NationalTeamData>> tiers = new List<List<NationalTeamData>>();

        for (int i = 0; i < 4; i++)
        {
            int start = Mathf.Min(i * tierSize, sorted.Count);
            int length = Mathf.Min(tierSize, sorted.Count - start);
            List<NationalTeamData> tier = length > 0 ? sorted.GetRange(start, length) : new List<NationalTeamData>();
            Shuffle(tier);
            tiers.Add(tier);
        }

        int baseTarget = count / 4;
        int remainder = count % 4;

        List<NationalTeamData> picked = new List<NationalTeamData>();

        for (int i = 0; i < 4; i++)
        {
            int target = baseTarget + (i < remainder ? 1 : 0);
            int take = Mathf.Min(target, tiers[i].Count);
            picked.AddRange(tiers[i].Take(take));
        }

        // If some tiers were too small to hit their target (small pool),
        // top up from whatever candidates are left, anywhere in the pool.
        if (picked.Count < count)
        {
            List<NationalTeamData> remaining = sorted.Except(picked).ToList();
            Shuffle(remaining);
            picked.AddRange(remaining.Take(count - picked.Count));
        }

        return picked;
    }

    // Random pairing within two same-size pots: pot4[i] faces pot1[i] in
    // mirrored order, mirroring the World Cup convention that a team's
    // first opponent comes from a different (lower) seeding pot, while the
    // exact pairing is randomized every campaign.
    private List<BracketMatch> PairPots(List<NationalTeamData> potA, List<NationalTeamData> potB)
    {
        List<NationalTeamData> shuffledA = new List<NationalTeamData>(potA);
        List<NationalTeamData> shuffledB = new List<NationalTeamData>(potB);
        Shuffle(shuffledA);
        Shuffle(shuffledB);

        List<BracketMatch> pairs = new List<BracketMatch>();

        for (int i = 0; i < shuffledA.Count && i < shuffledB.Count; i++)
        {
            bool swapSides = Random.value < 0.5f;

            pairs.Add(new BracketMatch
            {
                teamA = swapSides ? shuffledB[i] : shuffledA[i],
                teamB = swapSides ? shuffledA[i] : shuffledB[i]
            });
        }

        return pairs;
    }

    // --- Bracket queries -------------------------------------------------

    public BracketRound GetCurrentRound()
    {
        return CurrentBracketRoundIndex >= 0 && CurrentBracketRoundIndex < Bracket.Count
            ? Bracket[CurrentBracketRoundIndex]
            : null;
    }

    public BracketMatch GetJordanMatchInCurrentRound()
    {
        BracketRound round = GetCurrentRound();
        return round?.matches.Find(match => match.isJordanMatch);
    }

    // Used by MatchSetupBuilder to thread Jordan's flag sprite into the
    // MatchDay preview without every caller needing a database reference.
    public Sprite GetJordanFlag()
    {
        return teamDatabase?.teams.Find(team => team != null && team.name == JordanAssetName)?.flag;
    }

    // --- Launching matches -----------------------------------------------

    public void LaunchFixture(CampaignStage stage, int index)
    {
        launchedStage = stage;
        launchedIndex = index;

        List<FixtureRecord> fixtures = GetCurrentStageFixtures();
        NationalTeamData opponent = fixtures[index].opponent;

        MatchSession session = MatchSession.GetOrCreate();
        session.SetPendingOpponentTeam(opponent);
        session.SetPendingIsKnockout(false);

        EnteredFormationFromCampaign = true;
        SceneManager.LoadScene("FormationScene");
    }

    public void LaunchBracketMatch()
    {
        BracketMatch jordanMatch = GetJordanMatchInCurrentRound();

        if (jordanMatch == null)
        {
            return;
        }

        launchedStage = Stage;
        launchedIndex = 0;

        NationalTeamData jordan = teamDatabase.teams.Find(team => team != null && team.name == JordanAssetName);
        NationalTeamData opponent = jordanMatch.teamA == jordan ? jordanMatch.teamB : jordanMatch.teamA;

        MatchSession session = MatchSession.GetOrCreate();
        session.SetPendingOpponentTeam(opponent);
        session.SetPendingIsKnockout(true);

        EnteredFormationFromCampaign = true;
        SceneManager.LoadScene("FormationScene");
    }

    // --- Recording results -------------------------------------------------

    public void RecordResult(int homeScore, int awayScore, bool homeWonOverall, int? penaltyHomeScore = null, int? penaltyAwayScore = null)
    {
        if (launchedIndex < 0)
        {
            return;
        }

        CampaignStage stagePlayed = launchedStage;
        launchedIndex = -1;

        if (stagePlayed == CampaignStage.Friendlies)
        {
            RecordFriendlyResult(homeScore, awayScore, homeWonOverall);
        }
        else
        {
            RecordBracketResult(stagePlayed, homeScore, awayScore, homeWonOverall, penaltyHomeScore, penaltyAwayScore);
        }

        SaveManager.Instance?.SaveCurrentState();
    }

    private void RecordFriendlyResult(int homeScore, int awayScore, bool homeWonOverall)
    {
        FixtureRecord fixture = Friendlies.Find(f => !f.played);

        if (fixture == null)
        {
            return;
        }

        fixture.played = true;
        fixture.homeScore = homeScore;
        fixture.awayScore = awayScore;

        AwardPoints(CampaignStage.Friendlies, homeScore, awayScore, homeWonOverall);

        if (Friendlies.TrueForAll(f => f.played))
        {
            GenerateWorldCupDraw();
        }
    }

    private void RecordBracketResult(CampaignStage stagePlayed, int homeScore, int awayScore, bool homeWonOverall, int? penaltyHomeScore, int? penaltyAwayScore)
    {
        BracketRound round = GetCurrentRound();
        BracketMatch jordanMatch = round?.matches.Find(m => m.isJordanMatch);

        if (jordanMatch == null)
        {
            return;
        }

        NationalTeamData jordan = teamDatabase.teams.Find(team => team != null && team.name == JordanAssetName);
        bool jordanIsTeamA = jordanMatch.teamA == jordan;

        jordanMatch.scoreA = jordanIsTeamA ? homeScore : awayScore;
        jordanMatch.scoreB = jordanIsTeamA ? awayScore : homeScore;

        // Knockout matches never end level - if regulation/extra time was
        // level, the actual winner (decided by the live penalty shootout)
        // is homeWonOverall. Bump the winning side's recorded score by one
        // so BracketMatch.Winner reflects the real outcome.
        if (jordanMatch.scoreA == jordanMatch.scoreB)
        {
            bool jordanWonShootout = homeWonOverall;
            bool teamAWon = jordanIsTeamA ? jordanWonShootout : !jordanWonShootout;

            if (teamAWon)
            {
                jordanMatch.scoreA++;
            }
            else
            {
                jordanMatch.scoreB++;
            }
        }

        jordanMatch.played = true;

        if (penaltyHomeScore.HasValue && penaltyAwayScore.HasValue)
        {
            jordanMatch.hasPenalties = true;
            jordanMatch.penaltyScoreA = jordanIsTeamA ? penaltyHomeScore.Value : penaltyAwayScore.Value;
            jordanMatch.penaltyScoreB = jordanIsTeamA ? penaltyAwayScore.Value : penaltyHomeScore.Value;
        }

        AwardPoints(stagePlayed, homeScore, awayScore, homeWonOverall);

        SimulateRoundOtherMatches(round);

        bool jordanAdvanced = jordanMatch.Winner == jordan;

        if (!jordanAdvanced)
        {
            CompletionMessage = "ELIMINATED - LOST IN " + round.roundName.ToUpperInvariant();
            Stage = CampaignStage.Completed;
            BracketRecapPending = true;
            return;
        }

        if (!round.AllPlayed())
        {
            return;
        }

        if (stagePlayed == CampaignStage.Final)
        {
            CompletionMessage = "CHAMPIONS! JORDAN WINS THE WORLD CUP FINAL";
            Stage = CampaignStage.Completed;
            BracketRecapPending = true;
            return;
        }

        BuildNextRound(round);
        BracketRecapPending = true;
    }

    // Simulates every match in this round that is not Jordan's own match,
    // using the same rating-based instant-sim formula as before. Ties are
    // broken by a rating-weighted coin flip so every knockout match has a
    // clear winner.
    private void SimulateRoundOtherMatches(BracketRound round)
    {
        foreach (BracketMatch match in round.matches)
        {
            if (match.isJordanMatch || match.played || match.teamA == null || match.teamB == null)
            {
                continue;
            }

            match.scoreA = SimulateExpectedGoals(match.teamA, match.teamB);
            match.scoreB = SimulateExpectedGoals(match.teamB, match.teamA);

            if (match.scoreA == match.scoreB)
            {
                float oddsForA = match.teamA.Overall / (float)(match.teamA.Overall + match.teamB.Overall);

                if (Random.value < oddsForA)
                {
                    match.scoreA++;
                }
                else
                {
                    match.scoreB++;
                }
            }

            match.played = true;
        }
    }

    private static readonly string[] NextRoundNames =
    {
        "Round of 16", "Quarterfinal", "Semifinal", "Final"
    };

    private void BuildNextRound(BracketRound completedRound)
    {
        List<BracketMatch> nextMatches = new List<BracketMatch>();

        for (int i = 0; i < completedRound.matches.Count; i += 2)
        {
            NationalTeamData winnerA = completedRound.matches[i].Winner;
            NationalTeamData winnerB = completedRound.matches[i + 1].Winner;

            nextMatches.Add(new BracketMatch
            {
                teamA = winnerA,
                teamB = winnerB,
                isJordanMatch = winnerA != null && winnerA.name == JordanAssetName ||
                                winnerB != null && winnerB.name == JordanAssetName
            });
        }

        CurrentBracketRoundIndex++;

        string nextRoundName = CurrentBracketRoundIndex < NextRoundNames.Length
            ? NextRoundNames[CurrentBracketRoundIndex]
            : "Final";

        Bracket.Add(new BracketRound { roundName = nextRoundName, matches = nextMatches });

        Stage = CurrentBracketRoundIndex switch
        {
            1 => CampaignStage.Quarterfinal,
            2 => CampaignStage.Semifinal,
            _ => CampaignStage.Final
        };
    }

    public void MarkDrawRevealShown()
    {
        WorldCupDrawRevealShown = true;
        Stage = CampaignStage.RoundOf16;
        BracketRecapPending = false;
        SaveManager.Instance?.SaveCurrentState();
    }

    public void MarkBracketRecapShown()
    {
        BracketRecapPending = false;
    }

    // Knockout has no group table, so "standings" here means each of the
    // 16 drawn teams' status: still alive, eliminated in a given round, or
    // champion - read against the bracket rounds built so far.
    public string GetTeamStatus(NationalTeamData team)
    {
        foreach (BracketRound round in Bracket)
        {
            BracketMatch match = round.matches.Find(m => m.teamA == team || m.teamB == team);

            if (match == null)
            {
                continue;
            }

            if (!match.played)
            {
                return "STILL IN";
            }

            if (match.Winner != team)
            {
                return "ELIMINATED - " + round.roundName.ToUpperInvariant();
            }

            if (round.roundName == "Final")
            {
                return "CHAMPION";
            }
        }

        return "STILL IN";
    }

    // --- Points -------------------------------------------------------

    private static int BasePointsForStage(CampaignStage stage)
    {
        switch (stage)
        {
            case CampaignStage.Friendlies: return 5;
            case CampaignStage.RoundOf16: return 15;
            case CampaignStage.Quarterfinal: return 25;
            case CampaignStage.Semifinal: return 40;
            case CampaignStage.Final: return 60;
            default: return 5;
        }
    }

    private void AwardPoints(CampaignStage stagePlayed, int homeScore, int awayScore, bool homeWonOverall)
    {
        if (TrainingManager.Instance == null)
        {
            return;
        }

        int basePoints = BasePointsForStage(stagePlayed);

        bool isFriendly = stagePlayed == CampaignStage.Friendlies;
        bool isDraw = isFriendly && homeScore == awayScore;
        bool isWin = isFriendly ? homeScore > awayScore : homeWonOverall;

        int total;

        if (isWin)
        {
            int marginBonus = Mathf.Min(10, Mathf.Abs(homeScore - awayScore) * 2);
            int cleanSheetBonus = awayScore == 0 ? 5 : 0;
            total = basePoints + marginBonus + cleanSheetBonus;
        }
        else if (isDraw)
        {
            total = Mathf.RoundToInt(basePoints * 0.4f);
        }
        else
        {
            total = Mathf.RoundToInt(basePoints * 0.25f);
        }

        TrainingManager.Instance.AddDevelopmentPoints(total);
    }

    private int SimulateExpectedGoals(NationalTeamData attackingTeam, NationalTeamData defendingTeam)
    {
        float attackStrength = (attackingTeam.attack + attackingTeam.midfield) / 2f;
        float expectedGoals = Mathf.Clamp((attackStrength - defendingTeam.defense) * 0.04f + 1.2f, 0.2f, 4f);

        return Mathf.Clamp(Mathf.RoundToInt(Random.Range(0f, expectedGoals * 2f)), 0, 6);
    }

    // --- Save / restore -------------------------------------------------

    // Returns true if campaign data was found and restored from the save
    // snapshot, false if there was nothing to restore (brand-new campaign).
    private bool RestoreFromSave()
    {
        GameSaveData save = SaveManager.PendingLoadData;

        if (save == null || string.IsNullOrEmpty(save.campaign.stage))
        {
            return false;
        }

        CampaignSaveData data = save.campaign;

        Stage = System.Enum.TryParse(data.stage, out CampaignStage parsedStage) ? parsedStage : CampaignStage.Friendlies;
        CurrentBracketRoundIndex = data.currentBracketRoundIndex;
        WorldCupDrawRevealShown = data.wcDrawRevealShown;
        BracketRecapPending = data.bracketRecapPending;
        CompletionMessage = data.completionMessage;

        Friendlies = data.friendlies.Select(ResolveFixture).ToList();
        Bracket = data.bracketRounds.Select(ResolveRound).ToList();
        WorldCupField = data.worldCupFieldNames
            .Select(teamName => teamDatabase.teams.Find(team => team != null && team.name == teamName))
            .Where(team => team != null)
            .ToList();

        // Saves written before WorldCupField existed have no field-of-16
        // snapshot - rebuild it from the Round of 16 matches so the
        // standings panel still works for an in-progress campaign.
        if (WorldCupField.Count == 0 && Bracket.Count > 0)
        {
            WorldCupField = Bracket[0].matches
                .SelectMany(match => new[] { match.teamA, match.teamB })
                .Where(team => team != null)
                .ToList();
        }

        return true;
    }

    private FixtureRecord ResolveFixture(FixtureRecordSave saved)
    {
        return new FixtureRecord
        {
            opponent = teamDatabase.teams.Find(team => team != null && team.name == saved.opponentName),
            played = saved.played,
            homeScore = saved.homeScore,
            awayScore = saved.awayScore
        };
    }

    private BracketRound ResolveRound(BracketRoundSave saved)
    {
        return new BracketRound
        {
            roundName = saved.roundName,
            matches = saved.matches.Select(ResolveMatch).ToList()
        };
    }

    private BracketMatch ResolveMatch(BracketMatchSave saved)
    {
        return new BracketMatch
        {
            teamA = teamDatabase.teams.Find(team => team != null && team.name == saved.teamAName),
            teamB = teamDatabase.teams.Find(team => team != null && team.name == saved.teamBName),
            isJordanMatch = saved.isJordanMatch,
            played = saved.played,
            scoreA = saved.scoreA,
            scoreB = saved.scoreB,
            hasPenalties = saved.hasPenalties,
            penaltyScoreA = saved.penaltyScoreA,
            penaltyScoreB = saved.penaltyScoreB
        };
    }

    // Called by SaveManager to fill in this manager's section of the save.
    public void WriteSaveData(CampaignSaveData data)
    {
        data.stage = Stage.ToString();
        data.currentBracketRoundIndex = CurrentBracketRoundIndex;
        data.wcDrawRevealShown = WorldCupDrawRevealShown;
        data.bracketRecapPending = BracketRecapPending;
        data.completionMessage = CompletionMessage;
        data.worldCupFieldNames = WorldCupField.Select(team => team.name).ToList();

        data.friendlies = Friendlies.Select(f => new FixtureRecordSave
        {
            opponentName = f.opponent != null ? f.opponent.name : "",
            played = f.played,
            homeScore = f.homeScore,
            awayScore = f.awayScore
        }).ToList();

        data.bracketRounds = Bracket.Select(round => new BracketRoundSave
        {
            roundName = round.roundName,
            matches = round.matches.Select(match => new BracketMatchSave
            {
                teamAName = match.teamA != null ? match.teamA.name : "",
                teamBName = match.teamB != null ? match.teamB.name : "",
                isJordanMatch = match.isJordanMatch,
                played = match.played,
                scoreA = match.scoreA,
                scoreB = match.scoreB,
                hasPenalties = match.hasPenalties,
                penaltyScoreA = match.penaltyScoreA,
                penaltyScoreB = match.penaltyScoreB
            }).ToList()
        }).ToList();
    }
}
