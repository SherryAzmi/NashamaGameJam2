using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class FormationFieldManager : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject playerTokenPrefab;
    public GameObject benchCardPrefab;

    [Header("Bench UI")]
    public Transform benchContent;
    public TMP_Text selectionText;

    [Header("Team Stats UI")]
    public TMP_Text teamStatsText;

    [Header("Field")]
    public float playerZ = 9f;

    private TeamManager teamManager;
    private PlayerToken selectedStarter;




    private Camera mainCamera;

    private string currentFormation = "4-3-3";
    private FormationSlot[] formationSlots;

    private readonly List<PlayerToken> spawnedTokens =
        new List<PlayerToken>();

    private void Start()
    {
        mainCamera = Camera.main;

        teamManager = FindFirstObjectByType<TeamManager>();

        if (teamManager == null)
        {
            Debug.LogError(
                "TeamManager not found. Start from squad selection scene."
            );
            return;
        }

        if (teamManager.selectedPlayers == null ||
            teamManager.selectedPlayers.Count != 26)
        {
            Debug.LogError(
                "You must confirm 26 players before FormationScene."
            );
            return;
        }

        formationSlots = GetFormationSlots(currentFormation);

        CreateFirstFormationIfNeeded();
        SpawnStartingEleven();
        RefreshBench();
        RefreshTeamStats();

        SetStatus("CHOOSE A STARTER, THEN A BENCH PLAYER");
    }

    private void Update()
    {    if (UnityEngine.SceneManagement.SceneManager
        .GetActiveScene().name == "TrainingScene")
    {
        return;
    }
        if (Mouse.current == null || mainCamera == null)
        {
            return;
        }

        if (!Mouse.current.leftButton.wasPressedThisFrame)
        {
            return;
        }

        if (EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }


        Vector2 mousePosition = Mouse.current.position.ReadValue();

        Vector3 worldPosition = mainCamera.ScreenToWorldPoint(
            new Vector3(
                mousePosition.x,
                mousePosition.y,
                -mainCamera.transform.position.z
            )
        );

        Collider2D clickedCollider = Physics2D.OverlapPoint(
            new Vector2(worldPosition.x, worldPosition.y)
        );

        if (clickedCollider == null)
        {
            return;
        }

        PlayerToken clickedToken =
            clickedCollider.GetComponent<PlayerToken>();

        if (clickedToken != null)
        {
            SelectStarter(clickedToken);
        }
    }

    // Buttons will call these methods.
    public void Set433()
    {
        ChangeFormation("4-3-3");
    }

    public void Set442()
    {
        ChangeFormation("4-4-2");
    }

    public void Set4231()
    {
        ChangeFormation("4-2-3-1");
    }

    public void Set352()
    {
        ChangeFormation("3-5-2");
    }

    private void ChangeFormation(string newFormation)
    {
        currentFormation = newFormation;
        formationSlots = GetFormationSlots(currentFormation);

        if (selectedStarter != null)
        {
            selectedStarter.SetSelected(false);
            selectedStarter = null;
        }

        RefreshStarterTokens();
        RefreshTeamStats();

        SetStatus("FORMATION CHANGED TO " + currentFormation);
    }

    private void CreateFirstFormationIfNeeded()
    {
        bool alreadyCreated =
            teamManager.formationInitialized &&
            teamManager.startingEleven.Count == 11 &&
            teamManager.benchPlayers.Count == 15;

        if (alreadyCreated)
        {
            return;
        }

        List<PlayerData> availablePlayers =
            new List<PlayerData>(teamManager.selectedPlayers);

        teamManager.startingEleven.Clear();
        teamManager.benchPlayers.Clear();

        foreach (FormationSlot slot in formationSlots)
        {
            PlayerData chosenPlayer = PickPlayerForSlot(
                availablePlayers,
                slot.preferredPositions
            );

            teamManager.startingEleven.Add(chosenPlayer);
        }

        teamManager.benchPlayers.AddRange(availablePlayers);
        teamManager.formationInitialized = true;
    }

    private PlayerData PickPlayerForSlot(
        List<PlayerData> availablePlayers,
        params string[] preferredPositions
    )
    {
        List<PlayerData> exactCandidates =
            new List<PlayerData>();

        foreach (PlayerData player in availablePlayers)
        {
            string playerPosition = NormalizePosition(player.position);

            foreach (string preferredPosition in preferredPositions)
            {
                if (playerPosition == NormalizePosition(preferredPosition))
                {
                    exactCandidates.Add(player);
                    break;
                }
            }
        }

        List<PlayerData> candidates = exactCandidates;

        if (candidates.Count == 0)
        {
            candidates = new List<PlayerData>();

            string wantedCategory =
                GetPositionCategory(preferredPositions[0]);

            foreach (PlayerData player in availablePlayers)
            {
                if (GetPositionCategory(player.position) == wantedCategory)
                {
                    candidates.Add(player);
                }
            }
        }

        if (candidates.Count == 0)
        {
            candidates = new List<PlayerData>(availablePlayers);
        }

        PlayerData chosenPlayer =
            candidates[Random.Range(0, candidates.Count)];

        availablePlayers.Remove(chosenPlayer);

        return chosenPlayer;
    }

    private void SpawnStartingEleven()
    {
        ClearOldTokens();

        for (int i = 0; i < formationSlots.Length; i++)
        {
            FormationSlot slot = formationSlots[i];

            GameObject tokenObject = Instantiate(
                playerTokenPrefab,
                transform
            );

            tokenObject.transform.localPosition = new Vector3(
                slot.position.x,
                slot.position.y,
                playerZ
            );

            PlayerToken token =
                tokenObject.GetComponent<PlayerToken>();

            if (token == null)
            {
                Debug.LogError(
                    "PlayerToken Prefab is missing PlayerToken script."
                );

                Destroy(tokenObject);
                continue;
            }

            token.Setup(
                teamManager.startingEleven[i],
                slot.slotName,
                this
            );

            spawnedTokens.Add(token);
        }
    }

    private void RefreshStarterTokens()
    {
        for (int i = 0; i < spawnedTokens.Count; i++)
        {
            if (i >= formationSlots.Length ||
                i >= teamManager.startingEleven.Count)
            {
                continue;
            }

            PlayerToken token = spawnedTokens[i];

            if (token == null)
            {
                continue;
            }

            FormationSlot slot = formationSlots[i];

            token.transform.localPosition = new Vector3(
                slot.position.x,
                slot.position.y,
                playerZ
            );

            token.Setup(
                teamManager.startingEleven[i],
                slot.slotName,
                this
            );
        }
    }

    public void SelectStarter(PlayerToken token)
    {
        if (token == null || token.Player == null)
        {
            return;
        }

        // أول اختيار.
        if (selectedStarter == null)
        {
            selectedStarter = token;
            selectedStarter.SetSelected(true);

            SetStatus(
                "SELECTED: " +
                selectedStarter.Player.playerName +
                " - CHOOSE A STARTER OR BENCH PLAYER"
            );

            return;
        }

        // كبس نفس اللاعب = إلغاء.
        if (selectedStarter == token)
        {
            selectedStarter.SetSelected(false);
            selectedStarter = null;

            SetStatus("CHOOSE A STARTER, THEN A BENCH PLAYER");
            return;
        }

        // تبديل بين لاعبين أساسيين.
        int firstIndex = spawnedTokens.IndexOf(selectedStarter);
        int secondIndex = spawnedTokens.IndexOf(token);

        if (firstIndex < 0 || secondIndex < 0)
        {
            Debug.LogError("Could not swap starters.");
            return;
        }

        PlayerData firstPlayer = teamManager.startingEleven[firstIndex];
        PlayerData secondPlayer = teamManager.startingEleven[secondIndex];

        teamManager.startingEleven[firstIndex] = secondPlayer;
        teamManager.startingEleven[secondIndex] = firstPlayer;

        selectedStarter.Setup(
            teamManager.startingEleven[firstIndex],
            formationSlots[firstIndex].slotName,
            this
        );

        token.Setup(
            teamManager.startingEleven[secondIndex],
            formationSlots[secondIndex].slotName,
            this
        );

        selectedStarter.SetSelected(false);
        token.SetSelected(false);

        selectedStarter = null;

        RefreshTeamStats();

        SetStatus(
            firstPlayer.playerName +
            " SWAPPED WITH " +
            secondPlayer.playerName
        );
    }

    public void SwapWithBench(PlayerData benchPlayer)
    {
        // Bench cards stay visible in TrainingScene, but manual swapping is disabled.
        if (IsTrainingScene())
        {
            return;
        }

        if (selectedStarter == null)
        {
            SetStatus("CHOOSE A STARTER FIRST");
            return;
        }

        int starterIndex = spawnedTokens.IndexOf(selectedStarter);
        int benchIndex = teamManager.benchPlayers.IndexOf(benchPlayer);

        if (starterIndex < 0 || benchIndex < 0)
        {
            Debug.LogError("Could not complete swap.");
            return;
        }

        PlayerData outgoingStarter =
            teamManager.startingEleven[starterIndex];

        teamManager.startingEleven[starterIndex] = benchPlayer;
        teamManager.benchPlayers[benchIndex] = outgoingStarter;

        selectedStarter.Setup(
            benchPlayer,
            formationSlots[starterIndex].slotName,
            this
        );

        selectedStarter.SetSelected(false);
        selectedStarter = null;

        RefreshBench();
        RefreshTeamStats();

        SetStatus("SWAP COMPLETE");
    }

    // Called only by TrainingFieldManager when an individual starter begins training.
    public bool TryReplaceStarterForTraining(
        PlayerData unavailablePlayer,
        out PlayerData replacementPlayer,
        out int starterIndex
    )
    {
        replacementPlayer = null;
        starterIndex = -1;

        if (teamManager == null ||
            unavailablePlayer == null ||
            teamManager.startingEleven == null ||
            teamManager.benchPlayers == null)
        {
            return false;
        }

        starterIndex = teamManager.startingEleven.IndexOf(
            unavailablePlayer
        );

        if (starterIndex < 0 ||
            starterIndex >= formationSlots.Length)
        {
            return false;
        }

        replacementPlayer = FindBestBenchReplacement(
            formationSlots[starterIndex]
        );

        if (replacementPlayer == null)
        {
            return false;
        }

        int benchIndex = teamManager.benchPlayers.IndexOf(
            replacementPlayer
        );

        if (benchIndex < 0)
        {
            return false;
        }

        teamManager.startingEleven[starterIndex] = replacementPlayer;
        teamManager.benchPlayers[benchIndex] = unavailablePlayer;

        RefreshTrainingView();
        return true;
    }

    // Called by TrainingFieldManager after the individual session has finished.
    public void RestoreStarterAfterTraining(
        PlayerData originalPlayer,
        PlayerData replacementPlayer,
        int starterIndex
    )
    {
        if (teamManager == null ||
            originalPlayer == null ||
            replacementPlayer == null ||
            starterIndex < 0 ||
            starterIndex >= teamManager.startingEleven.Count)
        {
            return;
        }

        // Do not overwrite a change the user made later in FormationScene.
        if (teamManager.startingEleven[starterIndex] != replacementPlayer)
        {
            return;
        }

        int originalBenchIndex = teamManager.benchPlayers.IndexOf(
            originalPlayer
        );

        teamManager.startingEleven[starterIndex] = originalPlayer;

        if (originalBenchIndex >= 0)
        {
            teamManager.benchPlayers[originalBenchIndex] = replacementPlayer;
        }
        else if (!teamManager.benchPlayers.Contains(replacementPlayer))
        {
            teamManager.benchPlayers.Add(replacementPlayer);
        }

        RefreshTrainingView();
    }

    public void RefreshTrainingView()
    {
        if (selectedStarter != null)
        {
            selectedStarter.SetSelected(false);
            selectedStarter = null;
        }

        RefreshStarterTokens();
        RefreshBench();
        RefreshTeamStats();
    }

    private PlayerData FindBestBenchReplacement(
        FormationSlot slot
    )
    {
        TrainingManager trainingManager =
            FindFirstObjectByType<TrainingManager>();

        PlayerData bestPlayer = null;
        int bestScore = int.MinValue;

        foreach (PlayerData candidate in teamManager.benchPlayers)
        {
            if (candidate == null)
            {
                continue;
            }

            if (trainingManager != null &&
                trainingManager.IsPlayerBusy(candidate))
            {
                continue;
            }

            int score = GetOverall(candidate);

            if (IsExactFit(candidate, slot))
            {
                score += 1000;
            }
            else if (IsSameUnit(candidate, slot))
            {
                score += 500;
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestPlayer = candidate;
            }
        }

        return bestPlayer;
    }

    private bool IsTrainingScene()
    {
        return UnityEngine.SceneManagement.SceneManager
            .GetActiveScene().name == "TrainingScene";
    }

    private void RefreshBench()
    {
        if (benchContent == null || benchCardPrefab == null)
        {
            Debug.LogError("Bench UI references are missing.");
            return;
        }

        for (int i = benchContent.childCount - 1; i >= 0; i--)
        {
            Destroy(benchContent.GetChild(i).gameObject);
        }

        foreach (PlayerData player in teamManager.benchPlayers)
        {
            GameObject cardObject = Instantiate(
                benchCardPrefab,
                benchContent
            );

            BenchCardUI card =
                cardObject.GetComponent<BenchCardUI>();

            if (card != null)
            {
                card.Setup(player, this);
            }
        }
    }

    private void RefreshTeamStats()
    {
        if (teamStatsText == null)
        {
            return;
        }

        int power = CalculateTeamPower();
        int chemistry = CalculateChemistry();

        int attack = CalculateUnitRating("ATT");
        int midfield = CalculateUnitRating("MID");
        int defense = CalculateUnitRating("DEF");

        teamStatsText.text =
            "FORMATION: " + currentFormation +
            "\nPOWER: " + power +
            "   CHEMISTRY: " + chemistry + "%" +
            "\nATT: " + attack +
            "   MID: " + midfield +
            "   DEF: " + defense;
    }

    private int CalculateTeamPower()
    {
        float totalOverall = 0f;
        float fitBonus = 0f;

        for (int i = 0; i < teamManager.startingEleven.Count; i++)
        {
            PlayerData player = teamManager.startingEleven[i];

            if (player == null)
            {
                continue;
            }

            totalOverall += GetOverall(player);
            fitBonus += GetFitBonus(player, formationSlots[i]);
        }

        float averageOverall = totalOverall / 11f;
        float averageFitBonus = fitBonus / 11f;

        return Mathf.Clamp(
            Mathf.RoundToInt(
                averageOverall +
                averageFitBonus +
                GetFormationPowerBonus()
            ),
            0,
            99
        );
    }

    private int CalculateChemistry()
    {
        int chemistry = 45;

        for (int i = 0; i < teamManager.startingEleven.Count; i++)
        {
            PlayerData player = teamManager.startingEleven[i];

            if (player == null)
            {
                continue;
            }

            if (IsExactFit(player, formationSlots[i]))
            {
                chemistry += 4;
            }
            else if (IsSameUnit(player, formationSlots[i]))
            {
                chemistry += 2;
            }
            else
            {
                chemistry -= 3;
            }
        }

        for (int i = 0; i < teamManager.startingEleven.Count; i++)
        {
            for (int j = i + 1;
                 j < teamManager.startingEleven.Count;
                 j++)
            {
                PlayerData first = teamManager.startingEleven[i];
                PlayerData second = teamManager.startingEleven[j];

                if (first == null || second == null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(first.club) &&
                    first.club == second.club)
                {
                    chemistry += 2;
                }
            }
        }

        chemistry += GetFormationChemistryBonus();

        return Mathf.Clamp(chemistry, 0, 100);
    }

    private int CalculateUnitRating(string unit)
    {
        float total = 0f;
        int count = 0;

        for (int i = 0; i < teamManager.startingEleven.Count; i++)
        {
            if (GetSlotCategory(formationSlots[i]) != unit)
            {
                continue;
            }

            PlayerData player = teamManager.startingEleven[i];

            if (player == null)
            {
                continue;
            }

            if (unit == "ATT")
            {
                total += (player.speed + player.shoot * 2f) / 3f;
            }
            else if (unit == "DEF")
            {
                total += (player.defense * 2f + player.speed) / 3f;
            }
            else
            {
                total += GetOverall(player);
            }

            count++;
        }

        if (count == 0)
        {
            return 0;
        }

        int tacticalBonus = GetTacticalBonus(unit);

        return Mathf.Clamp(
            Mathf.RoundToInt(total / count) + tacticalBonus,
            0,
            99
        );
    }

    private int GetFitBonus(
        PlayerData player,
        FormationSlot slot
    )
    {
        if (IsExactFit(player, slot))
        {
            return 3;
        }

        if (IsSameUnit(player, slot))
        {
            return 1;
        }

        return -4;
    }

    private bool IsExactFit(
        PlayerData player,
        FormationSlot slot
    )
    {
        string playerPosition = NormalizePosition(player.position);

        foreach (string preferredPosition in slot.preferredPositions)
        {
            if (playerPosition == NormalizePosition(preferredPosition))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsSameUnit(
        PlayerData player,
        FormationSlot slot
    )
    {
        string playerCategory = GetPositionCategory(player.position);
        string slotCategory = GetSlotCategory(slot);

        return playerCategory == slotCategory;
    }

    private int GetFormationPowerBonus()
    {
        switch (currentFormation)
        {
            case "4-3-3":
                return 2;

            case "4-4-2":
                return 1;

            case "4-2-3-1":
                return 2;

            case "3-5-2":
                return 1;
        }

        return 0;
    }

    private int GetFormationChemistryBonus()
    {
        switch (currentFormation)
        {
            case "4-3-3":
                return 3;

            case "4-4-2":
                return 4;

            case "4-2-3-1":
                return 5;

            case "3-5-2":
                return 1;
        }

        return 0;
    }

    private int GetTacticalBonus(string unit)
    {
        switch (currentFormation)
        {
            case "4-3-3":
                if (unit == "ATT") return 5;
                if (unit == "MID") return 3;
                if (unit == "DEF") return 1;
                break;

            case "4-4-2":
                if (unit == "ATT") return 3;
                if (unit == "MID") return 3;
                if (unit == "DEF") return 5;
                break;

            case "4-2-3-1":
                if (unit == "ATT") return 4;
                if (unit == "MID") return 6;
                if (unit == "DEF") return 3;
                break;

            case "3-5-2":
                if (unit == "ATT") return 4;
                if (unit == "MID") return 7;
                if (unit == "DEF") return -2;
                break;
        }

        return 0;
    }

    private FormationSlot[] GetFormationSlots(string formation)
    {
        switch (formation)
        {
            case "4-4-2":
                return new FormationSlot[]
                {
                    new FormationSlot("GK", new Vector2(0f, -3.70f), "GK"),

                    new FormationSlot("LB", new Vector2(-2f, -2.65f), "LB"),
                    new FormationSlot("LCB", new Vector2(-0.70f, -2.85f), "CB"),
                    new FormationSlot("RCB", new Vector2(0.70f, -2.85f), "CB"),
                    new FormationSlot("RB", new Vector2(2f, -2.65f), "RB"),

                    new FormationSlot("LM", new Vector2(-2.05f, -0.65f), "LW", "LM", "CM", "AM"),
                    new FormationSlot("LCM", new Vector2(-0.70f, -0.65f), "CM", "DM", "CAM", "AM"),
                    new FormationSlot("RCM", new Vector2(0.70f, -0.65f), "CM", "DM", "CAM", "AM"),
                    new FormationSlot("RM", new Vector2(2.05f, -0.65f), "RW", "RM", "CM", "AM"),

                    new FormationSlot("LS", new Vector2(-0.85f, 2f), "ST", "LW"),
                    new FormationSlot("RS", new Vector2(0.85f, 2f), "ST", "RW")
                };

            case "4-2-3-1":
                return new FormationSlot[]
                {
                    new FormationSlot("GK", new Vector2(0f, -3.70f), "GK"),

                    new FormationSlot("LB", new Vector2(-2f, -2.65f), "LB"),
                    new FormationSlot("LCB", new Vector2(-0.70f, -2.85f), "CB"),
                    new FormationSlot("RCB", new Vector2(0.70f, -2.85f), "CB"),
                    new FormationSlot("RB", new Vector2(2f, -2.65f), "RB"),

                    new FormationSlot("LDM", new Vector2(-0.80f, -1.05f), "DM", "CM"),
                    new FormationSlot("RDM", new Vector2(0.80f, -1.05f), "DM", "CM"),

                    new FormationSlot("LAM", new Vector2(-1.80f, 0.65f), "LW", "AM", "CAM"),
                    new FormationSlot("CAM", new Vector2(0f, 0.95f), "CAM", "AM", "CM"),
                    new FormationSlot("RAM", new Vector2(1.80f, 0.65f), "RW", "AM", "CAM"),

                    new FormationSlot("ST", new Vector2(0f, 2.40f), "ST")
                };

            case "3-5-2":
                return new FormationSlot[]
                {
                    new FormationSlot("GK", new Vector2(0f, -3.70f), "GK"),

                    new FormationSlot("LCB", new Vector2(-1.15f, -2.70f), "CB"),
                    new FormationSlot("CB", new Vector2(0f, -2.90f), "CB"),
                    new FormationSlot("RCB", new Vector2(1.15f, -2.70f), "CB"),

                    new FormationSlot("LWB", new Vector2(-2.15f, -0.95f), "LB", "LW"),
                    new FormationSlot("LCM", new Vector2(-0.80f, -0.45f), "CM", "DM", "CAM"),
                    new FormationSlot("CM", new Vector2(0f, 0.05f), "CM", "DM", "CAM"),
                    new FormationSlot("RCM", new Vector2(0.80f, -0.45f), "CM", "DM", "CAM"),
                    new FormationSlot("RWB", new Vector2(2.15f, -0.95f), "RB", "RW"),

                    new FormationSlot("LS", new Vector2(-0.85f, 2f), "ST", "LW"),
                    new FormationSlot("RS", new Vector2(0.85f, 2f), "ST", "RW")
                };

            default:
                return new FormationSlot[]
                {
                    new FormationSlot("GK", new Vector2(0f, -3.70f), "GK"),

                    new FormationSlot("LB", new Vector2(-2f, -2.65f), "LB"),
                    new FormationSlot("LCB", new Vector2(-0.70f, -2.85f), "CB"),
                    new FormationSlot("RCB", new Vector2(0.70f, -2.85f), "CB"),
                    new FormationSlot("RB", new Vector2(2f, -2.65f), "RB"),

                    new FormationSlot("LCM", new Vector2(-1.45f, -0.80f), "CM", "DM", "CAM", "AM"),
                    new FormationSlot("CM", new Vector2(0f, -0.35f), "CM", "DM", "CAM", "AM"),
                    new FormationSlot("RCM", new Vector2(1.45f, -0.80f), "CM", "DM", "CAM", "AM"),

                    new FormationSlot("LW", new Vector2(-1.95f, 1.65f), "LW"),
                    new FormationSlot("ST", new Vector2(0f, 2.30f), "ST"),
                    new FormationSlot("RW", new Vector2(1.95f, 1.65f), "RW")
                };
        }
    }

    private string GetSlotCategory(FormationSlot slot)
    {
        switch (slot.slotName)
        {
            case "GK":
                return "GK";

            case "LB":
            case "RB":
            case "CB":
            case "LCB":
            case "RCB":
            case "LWB":
            case "RWB":
                return "DEF";

            case "LM":
            case "RM":
            case "CM":
            case "LCM":
            case "RCM":
            case "DM":
            case "LDM":
            case "RDM":
            case "CAM":
            case "LAM":
            case "RAM":
            case "AM":
                return "MID";

            default:
                return "ATT";
        }
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

    private int GetOverall(PlayerData player)
    {
        return (
            player.speed +
            player.shoot +
            player.defense
        ) / 3;
    }

    private void ClearOldTokens()
    {
        foreach (PlayerToken token in spawnedTokens)
        {
            if (token != null)
            {
                Destroy(token.gameObject);
            }
        }

        spawnedTokens.Clear();
    }

    private void SetStatus(string message)
    {
        if (selectionText != null)
        {
            selectionText.text = message;
        }
    }

    private string NormalizePosition(string position)
    {
        if (string.IsNullOrWhiteSpace(position))
        {
            return "";
        }

        return position.Trim().ToUpperInvariant();
    }

    private class FormationSlot
    {
        public string slotName;
        public Vector2 position;
        public string[] preferredPositions;

        public FormationSlot(
            string slotName,
            Vector2 position,
            params string[] preferredPositions
        )
        {
            this.slotName = slotName;
            this.position = position;
            this.preferredPositions = preferredPositions;
        }
    }
}