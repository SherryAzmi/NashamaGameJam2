using System;
using System.Collections.Generic;

// Pure data DTOs for the JSON save file. JsonUtility cannot round-trip
// UnityEngine.Object references (PlayerData/NationalTeamData) across a
// session, so every reference to one of those ScriptableObject assets is
// stored here as its stable asset name string and re-resolved against the
// relevant database at load time.

[Serializable]
public class SquadSaveData
{
    public List<string> selectedPlayerNames = new List<string>();
    public List<string> startingElevenNames = new List<string>();
    public List<string> benchPlayerNames = new List<string>();
    public bool formationInitialized;
    public bool squadConfirmed;
    public int substitutionsUsed;
}

[Serializable]
public class PlayerTrainingStateSave
{
    public string playerName;

    public int speedBonus;
    public int shootBonus;
    public int defenseBonus;

    public int finishingSessions;
    public int speedSessions;
    public int defenseSessions;
    public int goalkeeperSessions;
}

[Serializable]
public class TrainingSaveData
{
    public int developmentPoints;
    public int currentGameHour;
    public string currentFormation = "4-3-3";
    public bool legacyBonusMigrationDone;

    public List<PlayerTrainingStateSave> playerStates = new List<PlayerTrainingStateSave>();

    // TeamTrainingState has no ScriptableObject references (only ints and
    // a list of plain FormationFamiliarityEntry), so it serializes as-is.
    public TeamTrainingState teamTrainingState = new TeamTrainingState();
}

[Serializable]
public class FixtureRecordSave
{
    public string opponentName;
    public bool played;
    public int homeScore;
    public int awayScore;
}

[Serializable]
public class BracketMatchSave
{
    public string teamAName;
    public string teamBName;
    public bool isJordanMatch;
    public bool played;
    public int scoreA;
    public int scoreB;
    public bool hasPenalties;
    public int penaltyScoreA;
    public int penaltyScoreB;
}

[Serializable]
public class BracketRoundSave
{
    public string roundName;
    public List<BracketMatchSave> matches = new List<BracketMatchSave>();
}

[Serializable]
public class CampaignSaveData
{
    public string stage;
    public List<FixtureRecordSave> friendlies = new List<FixtureRecordSave>();
    public List<BracketRoundSave> bracketRounds = new List<BracketRoundSave>();
    public List<string> worldCupFieldNames = new List<string>();
    public int currentBracketRoundIndex;
    public bool wcDrawRevealShown;
    public bool bracketRecapPending;
    public string completionMessage = "";
}

[Serializable]
public class GameSaveData
{
    public SquadSaveData squad = new SquadSaveData();
    public TrainingSaveData training = new TrainingSaveData();
    public CampaignSaveData campaign = new CampaignSaveData();
}
