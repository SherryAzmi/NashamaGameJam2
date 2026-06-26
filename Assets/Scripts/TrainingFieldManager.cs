using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class TrainingFieldManager : MonoBehaviour
{
    [Header("HUD")]
    public TMP_Text trainingHudText;
    public TMP_Text trainingStatusText;

    private TrainingManager trainingManager;
    private Camera mainCamera;

    private PlayerToken selectedPlayerToken;

    public PlayerToken SelectedPlayerToken => selectedPlayerToken;

    private void Start()
    {
        trainingManager = FindFirstObjectByType<TrainingManager>();
        mainCamera = Camera.main;

        if (trainingManager == null)
        {
            Debug.LogError(
                "TrainingManager not found. Start from Call-up System."
            );

            return;
        }

        if (mainCamera == null)
        {
            Debug.LogError("Main Camera not found.");
            return;
        }

        trainingManager.OnTrainingStateChanged += RefreshAll;

        RefreshAll();

        SetStatus(
            "SELECT A PLAYER, THEN OPEN TRAINING PROGRAMS"
        );
    }

    private void OnDestroy()
    {
        if (trainingManager != null)
        {
            trainingManager.OnTrainingStateChanged -= RefreshAll;
        }
    }

    private void Update()
    {
        if (trainingManager == null ||
            mainCamera == null ||
            Mouse.current == null)
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

        Vector2 mousePosition =
            Mouse.current.position.ReadValue();

        Vector3 worldPosition =
            mainCamera.ScreenToWorldPoint(
                new Vector3(
                    mousePosition.x,
                    mousePosition.y,
                    -mainCamera.transform.position.z
                )
            );

        Collider2D clickedCollider =
            Physics2D.OverlapPoint(
                new Vector2(
                    worldPosition.x,
                    worldPosition.y
                )
            );

        if (clickedCollider == null)
        {
            return;
        }

        PlayerToken token =
            clickedCollider.GetComponent<PlayerToken>();

        if (token != null)
        {
            SelectTrainingPlayer(token);
        }
    }

    private void SelectTrainingPlayer(PlayerToken token)
    {
        if (token == null || token.Player == null)
        {
            return;
        }

        if (trainingManager.IsPlayerBusy(token.Player))
        {
            SetStatus(
                token.Player.playerName +
                " IS CURRENTLY IN TRAINING."
            );

            return;
        }

        if (selectedPlayerToken == token)
        {
            selectedPlayerToken.SetSelected(false);
            selectedPlayerToken = null;

            SetStatus("PLAYER DESELECTED.");

            return;
        }

        if (selectedPlayerToken != null)
        {
            selectedPlayerToken.SetSelected(false);
        }

        selectedPlayerToken = token;
        selectedPlayerToken.SetSelected(true);

        SetStatus(
            "SELECTED: " +
            token.Player.playerName +
            " | OPEN TRAINING PROGRAMS"
        );
    }

    public bool StartTrainingFromProgramsMenu(
        TrainingPreview preview
    )
    {
        if (trainingManager == null || preview == null)
        {
            return false;
        }

        bool started =
            trainingManager.StartTraining(preview);

        if (!started)
        {
            return false;
        }

        string targetName =
            preview.targetPlayer != null
                ? preview.targetPlayer.playerName
                : "TEAM";

        SetStatus(
            "TRAINING STARTED: " +
            targetName +
            ". WAIT UNTIL THE TIMER FINISHES."
        );

        ClearSelectedPlayer();
        RefreshAll();

        return true;
    }

    public void RefreshAll()
    {
        RefreshPlayers();
        RefreshHud();
    }

    private void ClearSelectedPlayer()
    {
        if (selectedPlayerToken != null)
        {
            selectedPlayerToken.SetSelected(false);
            selectedPlayerToken = null;
        }
    }

    private void RefreshPlayers()
    {
        if (trainingManager == null)
        {
            return;
        }

        PlayerToken[] tokens =
            FindObjectsByType<PlayerToken>(
                FindObjectsSortMode.None
            );

        foreach (PlayerToken token in tokens)
        {
            if (token == null || token.Player == null)
            {
                continue;
            }

            token.SetBusy(
                trainingManager.IsPlayerBusy(token.Player)
            );
        }
    }

    private void RefreshHud()
    {
        if (trainingHudText == null ||
            trainingManager == null)
        {
            return;
        }

        TeamTrainingState team =
            trainingManager.TeamState;

        trainingHudText.text =
            "TRAINING CENTER" +
            "\nTIME: " +
            trainingManager.CurrentTimeLabel +
            "\nDEVELOPMENT POINTS: " +
            trainingManager.DevelopmentPoints +
            "\nFORMATION: " +
            trainingManager.CurrentFormation +
            "\nCHEMISTRY BONUS: +" +
            team.chemistryBonus +
            "\nATT: +" +
            team.attackUnitBonus +
            "  MID: +" +
            team.midfieldUnitBonus +
            "  DEF: +" +
            team.defenseUnitBonus;
    }

    private void SetStatus(string message)
    {
        if (trainingStatusText != null)
        {
            trainingStatusText.text = message;
        }
    }
}