using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class TrainingManager : MonoBehaviour
{
    private static TrainingManager instance;
    public static TrainingManager Instance => instance;

    private const int MaxIndividualSlots = 2;
    private const int MaxPlayerBonus = 4;
    private const int MaxUnitBonus = 6;
    private const int MaxChemistryBonus = 20;

    [Header("References")]
    [SerializeField] private TeamManager teamManager;

    [Header("Progress")]
    [SerializeField] private int developmentPoints = 0;

    [Tooltip("Internal game clock starts at Day 1 - 08:00.")]
    [SerializeField] private int currentGameHour = 8;

    [Tooltip("For testing, 5 means one game-hour passes every five real seconds. " +
             "Individual training = 20 sec, Unit = 40 sec, Team = 60 sec.")]
    [Min(0.25f)]
    [SerializeField] private float realSecondsPerGameHour = 5f;

    [SerializeField] private string currentFormation = "4-3-3";

    [Header("Saved Development")]
    [SerializeField] private List<PlayerTrainingState> playerStates =
        new List<PlayerTrainingState>();

    [Tooltip("Migrates bonuses earned with the older bonus-only training version into PlayerData once.")]
    [SerializeField] private bool legacyBonusMigrationDone;

    [SerializeField] private TeamTrainingState teamTrainingState =
        new TeamTrainingState();

    [Header("Active Training")]
    [SerializeField] private List<TrainingJob> individualJobs =
        new List<TrainingJob>();

    [SerializeField] private TrainingJob collectiveJob;

    private float realTimeAccumulator;

    public event Action OnTrainingStateChanged;

    public int DevelopmentPoints => developmentPoints;
    public int CurrentGameHour => currentGameHour;
    public string CurrentFormation => currentFormation;
    public TeamTrainingState TeamState => teamTrainingState;

    public bool HasActiveTraining =>
        individualJobs.Count > 0 || collectiveJob != null;

    public bool HasCollectiveTraining => collectiveJob != null;

    // currentGameHour itself still ticks exactly the same way and still
    // drives every training duration/cooldown - only the "Day X" calendar
    // framing is gone from the displayed label.
    public string CurrentTimeLabel
    {
        get
        {
            int hour = currentGameHour % 24;
            return hour.ToString("00") + ":00";
        }
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

    ResolveTeamManager();
    ResetActiveTrainingForNewPlay();
    RestoreFromSave();
}
[RuntimeInitializeOnLoadMethod(
    RuntimeInitializeLoadType.BeforeSceneLoad
)]
private static void ResetOldTrainingManagersBeforePlay()
{
    TrainingManager[] managers =
        FindObjectsByType<TrainingManager>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

    foreach (TrainingManager manager in managers)
    {
        manager.ResetActiveTrainingForNewPlay();
    }
}

private void ResetActiveTrainingForNewPlay()
{
    currentGameHour = 8;
    realTimeAccumulator = 0f;

    if (individualJobs == null)
    {
        individualJobs = new List<TrainingJob>();
    }
    else
    {
        individualJobs.Clear();
    }

    collectiveJob = null;
}

    private void Start()
    {
        EnsurePlayerStates();
        MigrateLegacyBonusesIntoPlayerData();
        NotifyTrainingStateChanged();
    }

    private void Update()
    {
        if (!HasActiveTraining)
        {
            realTimeAccumulator = 0f;
            return;
        }

        realTimeAccumulator += Time.deltaTime;

        float secondsPerHour = Mathf.Max(0.25f, realSecondsPerGameHour);

        while (realTimeAccumulator >= secondsPerHour)
        {
            realTimeAccumulator -= secondsPerHour;
            currentGameHour++;

            CompleteFinishedTraining();
            NotifyTrainingStateChanged();

            if (!HasActiveTraining)
            {
                realTimeAccumulator = 0f;
                break;
            }
        }
    }

    // Older versions stored trained values only in PlayerTrainingState.
    // Run once so no previous completed session is lost after this update.
    private void MigrateLegacyBonusesIntoPlayerData()
    {
        if (legacyBonusMigrationDone)
        {
            return;
        }

        foreach (PlayerTrainingState state in playerStates)
        {
            if (state == null || state.player == null)
            {
                continue;
            }

            state.player.speed += state.speedBonus;
            state.player.shoot += state.shootBonus;
            state.player.defense += state.defenseBonus;
        }

        legacyBonusMigrationDone = true;
    }

    private void ResolveTeamManager()
    {
        if (teamManager == null)
        {
            teamManager = GetComponent<TeamManager>();
        }

        if (teamManager == null)
        {
            teamManager = FindFirstObjectByType<TeamManager>();
        }
    }

    public void AddDevelopmentPoints(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        developmentPoints += amount;
        NotifyTrainingStateChanged();

        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.SaveCurrentState();
        }
    }

    // Restores permanent training progress from the shared save snapshot.
    // In-flight active jobs are intentionally not restored - they are
    // session-only, same as ResetActiveTrainingForNewPlay already assumes.
    private void RestoreFromSave()
    {
        GameSaveData save = SaveManager.PendingLoadData;

        if (save == null)
        {
            return;
        }

        TrainingSaveData data = save.training;

        developmentPoints = data.developmentPoints;
        currentGameHour = data.currentGameHour > 0 ? data.currentGameHour : currentGameHour;
        currentFormation = string.IsNullOrWhiteSpace(data.currentFormation)
            ? currentFormation
            : data.currentFormation;
        legacyBonusMigrationDone = data.legacyBonusMigrationDone;
        teamTrainingState = data.teamTrainingState ?? new TeamTrainingState();

        playerStates.Clear();

        foreach (PlayerTrainingStateSave savedState in data.playerStates)
        {
            PlayerData player = ResolvePlayerByName(savedState.playerName);

            if (player == null)
            {
                continue;
            }

            playerStates.Add(new PlayerTrainingState
            {
                player = player,
                speedBonus = savedState.speedBonus,
                shootBonus = savedState.shootBonus,
                defenseBonus = savedState.defenseBonus,
                finishingSessions = savedState.finishingSessions,
                speedSessions = savedState.speedSessions,
                defenseSessions = savedState.defenseSessions,
                goalkeeperSessions = savedState.goalkeeperSessions
            });
        }

        NotifyTrainingStateChanged();
    }

    private PlayerData ResolvePlayerByName(string playerName)
    {
        if (teamManager == null || teamManager.database == null)
        {
            return null;
        }

        foreach (PlayerData player in teamManager.database.players)
        {
            if (player != null && player.name == playerName)
            {
                return player;
            }
        }

        return null;
    }

    // Called by SaveManager to fill in this manager's section of the save.
    public void WriteSaveData(TrainingSaveData data)
    {
        data.developmentPoints = developmentPoints;
        data.currentGameHour = currentGameHour;
        data.currentFormation = currentFormation;
        data.legacyBonusMigrationDone = legacyBonusMigrationDone;
        data.teamTrainingState = teamTrainingState;

        data.playerStates.Clear();

        foreach (PlayerTrainingState state in playerStates)
        {
            if (state.player == null)
            {
                continue;
            }

            data.playerStates.Add(new PlayerTrainingStateSave
            {
                playerName = state.player.name,
                speedBonus = state.speedBonus,
                shootBonus = state.shootBonus,
                defenseBonus = state.defenseBonus,
                finishingSessions = state.finishingSessions,
                speedSessions = state.speedSessions,
                defenseSessions = state.defenseSessions,
                goalkeeperSessions = state.goalkeeperSessions
            });
        }
    }

    public void SetCurrentFormation(string formation)
    {
        if (!string.IsNullOrWhiteSpace(formation))
        {
            currentFormation = formation;
            NotifyTrainingStateChanged();
        }
    }

    // Call this only when the player confirms a NEW 26-player squad.
    // It removes jobs left from an older test/session, but keeps all
    // permanent player-development values and team bonuses.
    public void ClearActiveTrainingForNewSquad()
    {
        currentGameHour = 8;
        realTimeAccumulator = 0f;

        if (individualJobs == null)
        {
            individualJobs = new List<TrainingJob>();
        }
        else
        {
            individualJobs.Clear();
        }

        collectiveJob = null;

        Debug.Log("Old training jobs cleared for the new squad.");
        NotifyTrainingStateChanged();
    }

    public TrainingPreview GetTrainingPreview(
        PlayerData player,
        TrainingType type
    )
    {
        ResolveTeamManager();
        CompleteFinishedTraining();
        EnsurePlayerStates();

        TrainingPreview preview = new TrainingPreview
        {
            trainingType = type,
            scope = GetScope(type),
            unit = GetUnit(type),
            targetPlayer = player,
            cost = GetCost(type),
            durationHours = GetDuration(type)
        };

        if (preview.scope == TrainingScope.Individual)
        {
            BuildIndividualPreview(preview);
        }
        else if (preview.scope == TrainingScope.Unit)
        {
            BuildUnitPreview(preview);
        }
        else
        {
            BuildTeamPreview(preview);
        }

        return preview;
    }

    public bool StartTraining(TrainingPreview preview)
    {
        if (preview == null)
        {
            return false;
        }

        TrainingPreview validatedPreview = GetTrainingPreview(
            preview.targetPlayer,
            preview.trainingType
        );

        if (!validatedPreview.canStart)
        {
            Debug.Log(validatedPreview.message);
            return false;
        }

        developmentPoints -= validatedPreview.cost;

        TrainingJob job = new TrainingJob
        {
            type = validatedPreview.trainingType,
            scope = validatedPreview.scope,
            targetUnit = validatedPreview.unit,
            targetPlayer = validatedPreview.targetPlayer,
            lockedPlayers = new List<PlayerData>(
                validatedPreview.affectedPlayers
            ),
            formationAtStart = currentFormation,
            startHour = currentGameHour,
            endHour = currentGameHour + validatedPreview.durationHours,
            cost = validatedPreview.cost
        };

        if (job.scope == TrainingScope.Individual)
        {
            individualJobs.Add(job);
        }
        else
        {
            collectiveJob = job;
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayTraining();
        }

        NotifyTrainingStateChanged();
        return true;
    }

    public bool IsPlayerBusy(PlayerData player)
    {
        return GetActiveJobForPlayer(player) != null;
    }

    public bool IsIndividualTrainingActive(PlayerData player)
    {
        TrainingJob job = GetActiveJobForPlayer(player);

        return job != null &&
               job.scope == TrainingScope.Individual;
    }

    public bool CanUseManualReplacement(PlayerData player)
    {
        return player != null &&
               !HasCollectiveTraining &&
               IsIndividualTrainingActive(player);
    }

    public string GetPlayerAvailabilityText(PlayerData player)
    {
        TrainingJob job = GetActiveJobForPlayer(player);

        if (job == null)
        {
            return "Available";
        }

        return "Training: " + GetDisplayName(job.type) +
               " (" + job.HoursRemaining(currentGameHour) +
               "h left)";
    }

    public string GetActiveTrainingSummary()
    {
        if (!HasActiveTraining)
        {
            return "NO ACTIVE TRAINING";
        }

        string summary = "";

        foreach (TrainingJob job in individualJobs)
        {
            if (job.targetPlayer != null)
            {
                summary += job.targetPlayer.playerName +
                           " - " + GetDisplayName(job.type) +
                           " (" + job.HoursRemaining(currentGameHour) +
                           "h left)\n";
            }
        }

        if (collectiveJob != null)
        {
            summary += collectiveJob.scope.ToString().ToUpper() +
                       ": " + GetDisplayName(collectiveJob.type) +
                       " (" +
                       collectiveJob.HoursRemaining(currentGameHour) +
                       "h left)";
        }

        return summary.Trim();
    }

    public bool CanStartMatch(out string reason)
    {
        if (collectiveJob != null)
        {
            reason =
                "Unit or full-team training is active. Wait until it ends.";
            return false;
        }

        ResolveTeamManager();

        if (teamManager == null || teamManager.startingEleven == null)
        {
            reason = "Starting eleven is not ready.";
            return false;
        }

        foreach (PlayerData player in teamManager.startingEleven)
        {
            if (IsPlayerBusy(player))
            {
                reason =
                    player.playerName +
                    " is in individual training. Replace this player " +
                    "with an available bench player before the match.";
                return false;
            }
        }

        reason = "Team is available.";
        return true;
    }

    // Training is permanent: completed sessions write directly into PlayerData.
    // All other systems (formation, bench, match) now read the upgraded stats.
    public int GetEffectiveSpeed(PlayerData player)
    {
        return player != null ? player.speed : 0;
    }

    public int GetEffectiveShoot(PlayerData player)
    {
        return player != null ? player.shoot : 0;
    }

    public int GetEffectiveDefense(PlayerData player)
    {
        return player != null ? player.defense : 0;
    }

    public int GetEffectiveOverall(PlayerData player)
    {
        if (player == null)
        {
            return 0;
        }

        return (
            player.speed +
            player.shoot +
            player.defense
        ) / 3;
    }

    private void BuildIndividualPreview(TrainingPreview preview)
    {
        PlayerData player = preview.targetPlayer;

        if (player == null)
        {
            Fail(preview, "Select a player first.");
            return;
        }

        if (collectiveJob != null)
        {
            Fail(
                preview,
                "A unit or full-team program is already active. Wait for it."
            );
            return;
        }

        if (individualJobs.Count >= MaxIndividualSlots)
        {
            Fail(
                preview,
                "Both individual training slots are busy."
            );
            return;
        }

        if (IsPlayerBusy(player))
        {
            Fail(
                preview,
                player.playerName + " is already in training."
            );
            return;
        }

        if (preview.trainingType == TrainingType.GoalkeeperReflexes &&
            NormalizePosition(player.position) != "GK")
        {
            Fail(
                preview,
                "Goalkeeper Reflexes is for goalkeepers only."
            );
            return;
        }

        PlayerTrainingState state = GetPlayerState(player);

        int speedBefore = GetEffectiveSpeed(player);
        int shootBefore = GetEffectiveShoot(player);
        int defenseBefore = GetEffectiveDefense(player);

        int speedAfter = speedBefore;
        int shootAfter = shootBefore;
        int defenseAfter = defenseBefore;

        switch (preview.trainingType)
        {
            case TrainingType.FinishingDrill:
                shootAfter += GetGain(
                    state.finishingSessions,
                    state.shootBonus
                );
                break;

            case TrainingType.SpeedSprint:
                speedAfter += GetGain(
                    state.speedSessions,
                    state.speedBonus
                );
                break;

            case TrainingType.DefensiveTechnique:
                defenseAfter += GetGain(
                    state.defenseSessions,
                    state.defenseBonus
                );
                break;

            case TrainingType.GoalkeeperReflexes:
                defenseAfter += GetGain(
                    state.goalkeeperSessions,
                    state.defenseBonus
                );
                break;

            case TrainingType.AllRoundSession:
                speedAfter += state.speedBonus < MaxPlayerBonus ? 1 : 0;
                shootAfter += state.shootBonus < MaxPlayerBonus ? 1 : 0;
                defenseAfter += state.defenseBonus < MaxPlayerBonus ? 1 : 0;
                break;
        }

        if (speedBefore == speedAfter &&
            shootBefore == shootAfter &&
            defenseBefore == defenseAfter)
        {
            Fail(
                preview,
                "Maximum development reached for this player."
            );
            return;
        }

        if (developmentPoints < preview.cost)
        {
            Fail(
                preview,
                "Not enough DP. Need " + preview.cost +
                ", have " + developmentPoints + "."
            );
            return;
        }

        preview.affectedPlayers.Add(player);
        preview.canStart = true;
        preview.message = "Ready to start.";

        preview.description =
            player.playerName +
            "\n\nSpeed: " + speedBefore + " -> " + speedAfter +
            " (" + GainText(speedAfter - speedBefore) + ")" +
            "\nShoot: " + shootBefore + " -> " + shootAfter +
            " (" + GainText(shootAfter - shootBefore) + ")" +
            "\nDefense: " + defenseBefore + " -> " + defenseAfter +
            " (" + GainText(defenseAfter - defenseBefore) + ")" +
            "\n\nCost: " + preview.cost + " DP" +
            "\nDuration: " + preview.durationHours + " game hours" +
            "\n\nThe player is unavailable while training." +
            "\nYou may manually replace only this player with a free bench player.";
    }

    private void BuildUnitPreview(TrainingPreview preview)
    {
        if (collectiveJob != null)
        {
            Fail(
                preview,
                "A unit or full-team program is already active. Wait for it."
            );
            return;
        }

        preview.affectedPlayers = GetStartingPlayers(preview.unit);

        if (preview.affectedPlayers.Count == 0)
        {
            Fail(
                preview,
                "No matching starters were found."
            );
            return;
        }

        foreach (PlayerData player in preview.affectedPlayers)
        {
            if (IsPlayerBusy(player))
            {
                Fail(
                    preview,
                    player.playerName +
                    " is unavailable, so this unit cannot train."
                );
                return;
            }
        }

        string effect = "";
        bool canImprove = true;

        switch (preview.trainingType)
        {
            case TrainingType.AttackUnitTraining:
                canImprove =
                    teamTrainingState.attackUnitBonus < MaxUnitBonus;
                effect =
                    "Attack Unit Bonus: +" +
                    teamTrainingState.attackUnitBonus +
                    " -> +" +
                    Mathf.Min(
                        teamTrainingState.attackUnitBonus + 3,
                        MaxUnitBonus
                    );
                break;

            case TrainingType.MidfieldControl:
                canImprove =
                    teamTrainingState.midfieldUnitBonus < MaxUnitBonus;
                effect =
                    "Midfield Unit Bonus: +" +
                    teamTrainingState.midfieldUnitBonus +
                    " -> +" +
                    Mathf.Min(
                        teamTrainingState.midfieldUnitBonus + 3,
                        MaxUnitBonus
                    );
                break;

            case TrainingType.DefensiveShape:
                canImprove =
                    teamTrainingState.defenseUnitBonus < MaxUnitBonus;
                effect =
                    "Defense Unit Bonus: +" +
                    teamTrainingState.defenseUnitBonus +
                    " -> +" +
                    Mathf.Min(
                        teamTrainingState.defenseUnitBonus + 3,
                        MaxUnitBonus
                    );
                break;

            case TrainingType.GoalkeeperBacklineDrill:
                canImprove =
                    teamTrainingState.goalkeeperUnitBonus < MaxUnitBonus ||
                    teamTrainingState.defenseUnitBonus < MaxUnitBonus;
                effect =
                    "Goalkeeper Bonus: +" +
                    teamTrainingState.goalkeeperUnitBonus +
                    " -> +" +
                    Mathf.Min(
                        teamTrainingState.goalkeeperUnitBonus + 2,
                        MaxUnitBonus
                    ) +
                    "\nDefense Bonus: +" +
                    teamTrainingState.defenseUnitBonus +
                    " -> +" +
                    Mathf.Min(
                        teamTrainingState.defenseUnitBonus + 2,
                        MaxUnitBonus
                    );
                break;
        }

        if (!canImprove)
        {
            Fail(preview, "This unit bonus is already at maximum.");
            return;
        }

        if (developmentPoints < preview.cost)
        {
            Fail(
                preview,
                "Not enough DP. Need " + preview.cost +
                ", have " + developmentPoints + "."
            );
            return;
        }

        preview.canStart = true;
        preview.message = "Ready to start.";

        preview.description =
            "Affected starters: " +
            preview.affectedPlayers.Count +
            "\n\n" + effect +
            "\n\nCost: " + preview.cost + " DP" +
            "\nDuration: " + preview.durationHours + " game hours" +
            "\n\nNo bench replacements are allowed for Unit Training." +
            "\nMatches stay locked until all affected players finish.";
    }

    private void BuildTeamPreview(TrainingPreview preview)
    {
        if (collectiveJob != null)
        {
            Fail(
                preview,
                "A unit or full-team program is already active. Wait for it."
            );
            return;
        }

        if (individualJobs.Count > 0)
        {
            Fail(
                preview,
                "Finish individual training before starting full-team training."
            );
            return;
        }

        preview.affectedPlayers = GetSquadPlayers();

        if (preview.affectedPlayers.Count == 0)
        {
            Fail(preview, "No confirmed squad found.");
            return;
        }

        string effect = "";
        bool canImprove = true;

        switch (preview.trainingType)
        {
            case TrainingType.TeamBonding:
                canImprove =
                    teamTrainingState.chemistryBonus < MaxChemistryBonus;
                effect =
                    "Chemistry Bonus: +" +
                    teamTrainingState.chemistryBonus +
                    " -> +" +
                    Mathf.Min(
                        teamTrainingState.chemistryBonus + 6,
                        MaxChemistryBonus
                    );
                break;

            case TrainingType.FormationRehearsal:
                int familiarity =
                    teamTrainingState.GetFormationFamiliarity(
                        currentFormation
                    );

                canImprove = familiarity < 20;
                effect =
                    "Formation Familiarity (" +
                    currentFormation + "): " +
                    familiarity +
                    " -> " +
                    Mathf.Min(familiarity + 8, 20);
                break;

            case TrainingType.SetPieceTraining:
                canImprove =
                    teamTrainingState.setPieceBonus < MaxUnitBonus;
                effect =
                    "Set Piece Bonus: +" +
                    teamTrainingState.setPieceBonus +
                    " -> +" +
                    Mathf.Min(
                        teamTrainingState.setPieceBonus + 2,
                        MaxUnitBonus
                    );
                break;

            case TrainingType.HighPressSystem:
                canImprove =
                    teamTrainingState.highPressBonus < MaxUnitBonus;
                effect =
                    "High Press Bonus: +" +
                    teamTrainingState.highPressBonus +
                    " -> +" +
                    Mathf.Min(
                        teamTrainingState.highPressBonus + 2,
                        MaxUnitBonus
                    );
                break;

            case TrainingType.DefensiveTransition:
                canImprove =
                    teamTrainingState.defensiveTransitionBonus < MaxUnitBonus;
                effect =
                    "Defensive Transition Bonus: +" +
                    teamTrainingState.defensiveTransitionBonus +
                    " -> +" +
                    Mathf.Min(
                        teamTrainingState.defensiveTransitionBonus + 2,
                        MaxUnitBonus
                    );
                break;

            case TrainingType.VideoAnalysis:
                canImprove =
                    teamTrainingState.videoAnalysisBonus < MaxUnitBonus;
                effect =
                    "Video Analysis Bonus: +" +
                    teamTrainingState.videoAnalysisBonus +
                    " -> +" +
                    Mathf.Min(
                        teamTrainingState.videoAnalysisBonus + 2,
                        MaxUnitBonus
                    );
                break;
        }

        if (!canImprove)
        {
            Fail(preview, "This team bonus is already at maximum.");
            return;
        }

        if (developmentPoints < preview.cost)
        {
            Fail(
                preview,
                "Not enough DP. Need " + preview.cost +
                ", have " + developmentPoints + "."
            );
            return;
        }

        preview.canStart = true;
        preview.message = "Ready to start.";

        preview.description =
            "The whole squad will be unavailable." +
            "\n\n" + effect +
            "\n\nCost: " + preview.cost + " DP" +
            "\nDuration: " + preview.durationHours + " game hours" +
            "\n\nNo bench replacements are allowed for Full Team Training." +
            "\nMatches stay locked until training ends.";
    }

    private void CompleteFinishedTraining()
    {
        bool completedAnyJob = false;

        for (int i = individualJobs.Count - 1; i >= 0; i--)
        {
            if (!individualJobs[i].IsCompleted(currentGameHour))
            {
                continue;
            }

            ApplyEffect(individualJobs[i]);
            individualJobs.RemoveAt(i);
            completedAnyJob = true;
        }

        if (collectiveJob != null &&
            collectiveJob.IsCompleted(currentGameHour))
        {
            ApplyEffect(collectiveJob);
            collectiveJob = null;
            completedAnyJob = true;
        }

        if (completedAnyJob)
        {
            Debug.Log("Training completed at " + CurrentTimeLabel);

            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.SaveCurrentState();
            }
        }
    }

    private void ApplyEffect(TrainingJob job)
    {
        switch (job.type)
        {
            case TrainingType.FinishingDrill:
                AddShoot(job.targetPlayer);
                break;

            case TrainingType.SpeedSprint:
                AddSpeed(job.targetPlayer);
                break;

            case TrainingType.DefensiveTechnique:
            case TrainingType.GoalkeeperReflexes:
                AddDefense(job.targetPlayer, job.type);
                break;

            case TrainingType.AllRoundSession:
                AddAllRound(job.targetPlayer);
                break;

            case TrainingType.AttackUnitTraining:
                teamTrainingState.attackUnitBonus = Mathf.Clamp(
                    teamTrainingState.attackUnitBonus + 3,
                    0,
                    MaxUnitBonus
                );
                break;

            case TrainingType.MidfieldControl:
                teamTrainingState.midfieldUnitBonus = Mathf.Clamp(
                    teamTrainingState.midfieldUnitBonus + 3,
                    0,
                    MaxUnitBonus
                );
                break;

            case TrainingType.DefensiveShape:
                teamTrainingState.defenseUnitBonus = Mathf.Clamp(
                    teamTrainingState.defenseUnitBonus + 3,
                    0,
                    MaxUnitBonus
                );
                break;

            case TrainingType.GoalkeeperBacklineDrill:
                teamTrainingState.goalkeeperUnitBonus = Mathf.Clamp(
                    teamTrainingState.goalkeeperUnitBonus + 2,
                    0,
                    MaxUnitBonus
                );
                teamTrainingState.defenseUnitBonus = Mathf.Clamp(
                    teamTrainingState.defenseUnitBonus + 2,
                    0,
                    MaxUnitBonus
                );
                break;

            case TrainingType.TeamBonding:
                teamTrainingState.chemistryBonus = Mathf.Clamp(
                    teamTrainingState.chemistryBonus + 6,
                    0,
                    MaxChemistryBonus
                );
                break;

            case TrainingType.FormationRehearsal:
                teamTrainingState.AddFormationFamiliarity(
                    job.formationAtStart,
                    8
                );
                break;

            case TrainingType.SetPieceTraining:
                teamTrainingState.setPieceBonus = Mathf.Clamp(
                    teamTrainingState.setPieceBonus + 2,
                    0,
                    MaxUnitBonus
                );
                break;

            case TrainingType.HighPressSystem:
                teamTrainingState.highPressBonus = Mathf.Clamp(
                    teamTrainingState.highPressBonus + 2,
                    0,
                    MaxUnitBonus
                );
                break;

            case TrainingType.DefensiveTransition:
                teamTrainingState.defensiveTransitionBonus = Mathf.Clamp(
                    teamTrainingState.defensiveTransitionBonus + 2,
                    0,
                    MaxUnitBonus
                );
                break;

            case TrainingType.VideoAnalysis:
                teamTrainingState.videoAnalysisBonus = Mathf.Clamp(
                    teamTrainingState.videoAnalysisBonus + 2,
                    0,
                    MaxUnitBonus
                );
                break;
        }
    }

    private void AddShoot(PlayerData player)
    {
        if (player == null)
        {
            return;
        }

        PlayerTrainingState state = GetPlayerState(player);
        int gain = GetGain(
            state.finishingSessions,
            state.shootBonus
        );

        if (gain <= 0)
        {
            return;
        }

        // Permanent increase: the raw player stat itself changes.
        player.shoot += gain;
        state.shootBonus += gain;
        state.finishingSessions++;
    }

    private void AddSpeed(PlayerData player)
    {
        if (player == null)
        {
            return;
        }

        PlayerTrainingState state = GetPlayerState(player);
        int gain = GetGain(
            state.speedSessions,
            state.speedBonus
        );

        if (gain <= 0)
        {
            return;
        }

        // Permanent increase: the raw player stat itself changes.
        player.speed += gain;
        state.speedBonus += gain;
        state.speedSessions++;
    }

    private void AddDefense(PlayerData player, TrainingType type)
    {
        if (player == null)
        {
            return;
        }

        PlayerTrainingState state = GetPlayerState(player);

        int sessions = type == TrainingType.GoalkeeperReflexes
            ? state.goalkeeperSessions
            : state.defenseSessions;

        int gain = GetGain(sessions, state.defenseBonus);

        if (gain <= 0)
        {
            return;
        }

        // Permanent increase: the raw player stat itself changes.
        player.defense += gain;
        state.defenseBonus += gain;

        if (type == TrainingType.GoalkeeperReflexes)
        {
            state.goalkeeperSessions++;
        }
        else
        {
            state.defenseSessions++;
        }
    }

    private void AddAllRound(PlayerData player)
    {
        if (player == null)
        {
            return;
        }

        PlayerTrainingState state = GetPlayerState(player);

        if (state.speedBonus < MaxPlayerBonus)
        {
            player.speed++;
            state.speedBonus++;
        }

        if (state.shootBonus < MaxPlayerBonus)
        {
            player.shoot++;
            state.shootBonus++;
        }

        if (state.defenseBonus < MaxPlayerBonus)
        {
            player.defense++;
            state.defenseBonus++;
        }
    }

    private int GetGain(int sessions, int currentBonus)
    {
        if (currentBonus >= MaxPlayerBonus)
        {
            return 0;
        }

        int gain = sessions == 0 ? 2 : 1;

        return Mathf.Min(
            gain,
            MaxPlayerBonus - currentBonus
        );
    }

    private TrainingScope GetScope(TrainingType type)
    {
        if (type == TrainingType.FinishingDrill ||
            type == TrainingType.SpeedSprint ||
            type == TrainingType.DefensiveTechnique ||
            type == TrainingType.GoalkeeperReflexes ||
            type == TrainingType.AllRoundSession)
        {
            return TrainingScope.Individual;
        }

        if (type == TrainingType.AttackUnitTraining ||
            type == TrainingType.MidfieldControl ||
            type == TrainingType.DefensiveShape ||
            type == TrainingType.GoalkeeperBacklineDrill)
        {
            return TrainingScope.Unit;
        }

        return TrainingScope.Team;
    }

    private TrainingUnit GetUnit(TrainingType type)
    {
        switch (type)
        {
            case TrainingType.AttackUnitTraining:
                return TrainingUnit.Attack;
            case TrainingType.MidfieldControl:
                return TrainingUnit.Midfield;
            case TrainingType.DefensiveShape:
                return TrainingUnit.Defense;
            case TrainingType.GoalkeeperBacklineDrill:
                return TrainingUnit.GoalkeeperAndDefense;
        }

        return TrainingUnit.None;
    }

    private int GetCost(TrainingType type)
    {
        switch (type)
        {
            case TrainingType.FinishingDrill:
            case TrainingType.SpeedSprint:
            case TrainingType.DefensiveTechnique:
                return 6;
            case TrainingType.GoalkeeperReflexes:
                return 7;
            case TrainingType.AllRoundSession:
                return 10;
            case TrainingType.AttackUnitTraining:
            case TrainingType.MidfieldControl:
            case TrainingType.DefensiveShape:
                return 16;
            case TrainingType.GoalkeeperBacklineDrill:
                return 14;
            case TrainingType.TeamBonding:
                return 8;
            case TrainingType.FormationRehearsal:
                return 12;
            case TrainingType.SetPieceTraining:
                return 10;
            case TrainingType.HighPressSystem:
                return 14;
            case TrainingType.DefensiveTransition:
                return 12;
            case TrainingType.VideoAnalysis:
                return 4;
        }

        return 0;
    }

    private int GetDuration(TrainingType type)
    {
        switch (type)
        {
            case TrainingType.FinishingDrill:
            case TrainingType.SpeedSprint:
            case TrainingType.DefensiveTechnique:
            case TrainingType.GoalkeeperReflexes:
                return 4;

            case TrainingType.AllRoundSession:
                return 8;

            case TrainingType.AttackUnitTraining:
            case TrainingType.MidfieldControl:
            case TrainingType.DefensiveShape:
            case TrainingType.GoalkeeperBacklineDrill:
                return 8;

            case TrainingType.TeamBonding:
            case TrainingType.FormationRehearsal:
            case TrainingType.HighPressSystem:
            case TrainingType.DefensiveTransition:
                return 12;

            case TrainingType.SetPieceTraining:
                return 6;

            case TrainingType.VideoAnalysis:
                return 3;
        }

        return 4;
    }

    private List<PlayerData> GetSquadPlayers()
    {
        List<PlayerData> squad = new List<PlayerData>();

        if (teamManager == null ||
            teamManager.selectedPlayers == null)
        {
            return squad;
        }

        foreach (PlayerData player in teamManager.selectedPlayers)
        {
            if (player != null && !squad.Contains(player))
            {
                squad.Add(player);
            }
        }

        return squad;
    }

    private List<PlayerData> GetStartingPlayers(TrainingUnit unit)
    {
        List<PlayerData> players = new List<PlayerData>();

        if (teamManager == null ||
            teamManager.startingEleven == null)
        {
            return players;
        }

        foreach (PlayerData player in teamManager.startingEleven)
        {
            if (player == null)
            {
                continue;
            }

            string category = GetPositionCategory(player.position);

            bool include =
                (unit == TrainingUnit.Attack && category == "ATT") ||
                (unit == TrainingUnit.Midfield && category == "MID") ||
                (unit == TrainingUnit.Defense && category == "DEF") ||
                (unit == TrainingUnit.GoalkeeperAndDefense &&
                 (category == "GK" || category == "DEF"));

            if (include && !players.Contains(player))
            {
                players.Add(player);
            }
        }

        return players;
    }

    private TrainingJob GetActiveJobForPlayer(PlayerData player)
    {
        if (player == null)
        {
            return null;
        }

        foreach (TrainingJob job in individualJobs)
        {
            if (job.lockedPlayers.Contains(player))
            {
                return job;
            }
        }

        if (collectiveJob != null &&
            collectiveJob.lockedPlayers.Contains(player))
        {
            return collectiveJob;
        }

        return null;
    }

    private bool HasFullTeamTraining()
    {
        return collectiveJob != null &&
               collectiveJob.scope == TrainingScope.Team;
    }

    private void EnsurePlayerStates()
    {
        foreach (PlayerData player in GetSquadPlayers())
        {
            GetPlayerState(player);
        }
    }

    private PlayerTrainingState GetPlayerState(PlayerData player)
    {
        foreach (PlayerTrainingState state in playerStates)
        {
            if (state.player == player)
            {
                return state;
            }
        }

        PlayerTrainingState newState = new PlayerTrainingState
        {
            player = player
        };

        playerStates.Add(newState);
        return newState;
    }

    private string GetPositionCategory(string position)
    {
        switch (NormalizePosition(position))
        {
            case "GK":
                return "GK";
            case "LB":
            case "RB":
            case "CB":
            case "LWB":
            case "RWB":
                return "DEF";
            case "LM":
            case "RM":
            case "DM":
            case "CM":
            case "CAM":
            case "AM":
            case "LDM":
            case "RDM":
                return "MID";
            case "LW":
            case "RW":
            case "ST":
            case "LS":
            case "RS":
                return "ATT";
        }

        return "MID";
    }

    private string NormalizePosition(string position)
    {
        return string.IsNullOrWhiteSpace(position)
            ? ""
            : position.Trim().ToUpperInvariant();
    }

    private void Fail(TrainingPreview preview, string reason)
    {
        preview.canStart = false;
        preview.message = reason;
        preview.description = reason;
    }

    private string GainText(int gain)
    {
        return gain > 0 ? "+" + gain : gain.ToString();
    }

    private string GetDisplayName(TrainingType type)
    {
        switch (type)
        {
            case TrainingType.FinishingDrill:
                return "Finishing Drill";
            case TrainingType.SpeedSprint:
                return "Speed & Sprint";
            case TrainingType.DefensiveTechnique:
                return "Defensive Technique";
            case TrainingType.GoalkeeperReflexes:
                return "Goalkeeper Reflexes";
            case TrainingType.AllRoundSession:
                return "All-Round Session";
            case TrainingType.AttackUnitTraining:
                return "Attack Unit Training";
            case TrainingType.MidfieldControl:
                return "Midfield Control";
            case TrainingType.DefensiveShape:
                return "Defensive Shape";
            case TrainingType.GoalkeeperBacklineDrill:
                return "Goalkeeper + Backline Drill";
            case TrainingType.TeamBonding:
                return "Team Bonding";
            case TrainingType.FormationRehearsal:
                return "Formation Rehearsal";
            case TrainingType.SetPieceTraining:
                return "Set Piece Training";
            case TrainingType.HighPressSystem:
                return "High Press System";
            case TrainingType.DefensiveTransition:
                return "Defensive Transition";
            case TrainingType.VideoAnalysis:
                return "Video Analysis";
        }

        return type.ToString();
    }

    private void NotifyTrainingStateChanged()
    {
        OnTrainingStateChanged?.Invoke();
    }

    [ContextMenu("DEBUG/Add 30 Development Points")]
    private void DebugAddDevelopmentPoints()
    {
        AddDevelopmentPoints(30);
    }
}
