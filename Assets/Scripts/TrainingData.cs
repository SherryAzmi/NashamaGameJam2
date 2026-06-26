using System;
using System.Collections.Generic;
using UnityEngine;

public enum TrainingType
{
    // Individual
    FinishingDrill,
    SpeedSprint,
    DefensiveTechnique,
    GoalkeeperReflexes,
    AllRoundSession,

    // Unit
    AttackUnitTraining,
    MidfieldControl,
    DefensiveShape,
    GoalkeeperBacklineDrill,

    // Full team
    TeamBonding,
    FormationRehearsal,
    SetPieceTraining,
    HighPressSystem,
    DefensiveTransition,
    VideoAnalysis
}

public enum TrainingScope
{
    Individual,
    Unit,
    Team
}

public enum TrainingUnit
{
    None,
    Attack,
    Midfield,
    Defense,
    GoalkeeperAndDefense
}

[Serializable]
public class PlayerTrainingState
{
    public PlayerData player;

    public int speedBonus;
    public int shootBonus;
    public int defenseBonus;

    public int finishingSessions;
    public int speedSessions;
    public int defenseSessions;
    public int goalkeeperSessions;
}

[Serializable]
public class FormationFamiliarityEntry
{
    public string formation;
    public int value;
}

[Serializable]
public class TeamTrainingState
{
    public int attackUnitBonus;
    public int midfieldUnitBonus;
    public int defenseUnitBonus;
    public int goalkeeperUnitBonus;

    public int chemistryBonus;
    public int setPieceBonus;
    public int highPressBonus;
    public int defensiveTransitionBonus;
    public int videoAnalysisBonus;

    public List<FormationFamiliarityEntry> formationFamiliarities =
        new List<FormationFamiliarityEntry>();

    public int GetFormationFamiliarity(string formation)
    {
        foreach (FormationFamiliarityEntry entry in formationFamiliarities)
        {
            if (entry.formation == formation)
            {
                return entry.value;
            }
        }

        return 0;
    }

    public void AddFormationFamiliarity(string formation, int amount)
    {
        foreach (FormationFamiliarityEntry entry in formationFamiliarities)
        {
            if (entry.formation == formation)
            {
                entry.value = Mathf.Clamp(entry.value + amount, 0, 20);
                return;
            }
        }

        formationFamiliarities.Add(
            new FormationFamiliarityEntry
            {
                formation = formation,
                value = Mathf.Clamp(amount, 0, 20)
            }
        );
    }
}

[Serializable]
public class TrainingJob
{
    public TrainingType type;
    public TrainingScope scope;
    public TrainingUnit targetUnit;

    public PlayerData targetPlayer;

    public List<PlayerData> lockedPlayers =
        new List<PlayerData>();

    public string formationAtStart;

    public int startHour;
    public int endHour;
    public int cost;

    public int HoursRemaining(int currentHour)
    {
        return Mathf.Max(0, endHour - currentHour);
    }

    public bool IsCompleted(int currentHour)
    {
        return currentHour >= endHour;
    }
}

[Serializable]
public class TrainingPreview
{
    public bool canStart;

    public string message;
    public string description;

    public TrainingType trainingType;
    public TrainingScope scope;
    public TrainingUnit unit;

    public PlayerData targetPlayer;

    public List<PlayerData> affectedPlayers =
        new List<PlayerData>();

    public int cost;
    public int durationHours;
}
