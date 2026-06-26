using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class TeamManager : MonoBehaviour
{   
    public List<PlayerData> selectedPlayers = new List<PlayerData>();
    [HideInInspector] public List<PlayerData> startingEleven = new List<PlayerData>();
    [HideInInspector] public List<PlayerData> benchPlayers = new List<PlayerData>();
    [HideInInspector] public bool formationInitialized = false;
    [HideInInspector] public int substitutionsUsed = 0;

private static TeamManager instance;    
    public PlayerDatabase database;

    [Header("UI")]
    public Transform content;
    public GameObject playerCardPrefab;
    public TMP_Text selectedText;
    public Button confirmButton;


    private readonly List<PlayerCardUI> spawnedCards = new List<PlayerCardUI>();

    private int maxPlayers = 26;

    private int maxGoalkeepers = 3;
    private int maxDefenders = 8;
    private int maxMidfielders = 8;
    private int maxAttackers = 7;

    private bool squadConfirmed = false;

    private void Awake()
{
    if (instance != null && instance != this)
    {
        Destroy(gameObject);
        return;
    }

    instance = this;
    DontDestroyOnLoad(gameObject);
}

    private void Start()
    {
        CreatePlayerCards();

        confirmButton.onClick.RemoveAllListeners();
        confirmButton.onClick.AddListener(ConfirmSquad);

        UpdateSelectedText();
        UpdateConfirmButton();
    }

    void CreatePlayerCards()
    {
        foreach (PlayerData player in database.players)
        {
            GameObject card = Instantiate(playerCardPrefab, content);

            PlayerCardUI cardUI = card.GetComponent<PlayerCardUI>();

            if (cardUI == null)
            {
                Debug.LogError("PlayerCard Prefab is missing PlayerCardUI.");
                Destroy(card);
                continue;
            }

            cardUI.Setup(player, this);
            spawnedCards.Add(cardUI);
        }
    }

    public void TogglePlayer(PlayerData player, PlayerCardUI cardUI)
    {
        if (squadConfirmed)
        {
            Debug.Log("The squad is already confirmed. No changes allowed.");
            return;
        }

        if (selectedPlayers.Contains(player))
        {
            selectedPlayers.Remove(player);
            cardUI.SetSelected(false);
        }
        else
        {
            if (selectedPlayers.Count >= maxPlayers)
            {
                Debug.Log("You can only select 26 players.");
                return;
            }

            if (!CanSelectPlayer(player))
            {
                Debug.Log("The limit for this position has been reached.");
                return;
            }

            selectedPlayers.Add(player);
            cardUI.SetSelected(true);
        }

        UpdateSelectedText();
        UpdateConfirmButton();
    }

    void ConfirmSquad()
    {
        if (selectedPlayers.Count != maxPlayers)
        {
            Debug.Log("You must select exactly 26 players first.");
            return;
        }

        squadConfirmed = true;

        foreach (PlayerCardUI card in spawnedCards)
        {
            card.SetLocked(true);
        }

        confirmButton.interactable = false;

        TMP_Text buttonText = confirmButton.GetComponentInChildren<TMP_Text>();

        if (buttonText != null)
        {
            buttonText.text = "SQUAD CONFIRMED";
        }

        UpdateSelectedText();

        Debug.Log("Final Squad Confirmed!");

        foreach (PlayerData player in selectedPlayers)
        {
            Debug.Log(player.playerName + " - " + player.position);
        }

        SceneManager.LoadScene("CampaignScene");
    }

    void UpdateConfirmButton()
    {
        if (squadConfirmed)
            return;

        confirmButton.interactable = selectedPlayers.Count == maxPlayers;

        TMP_Text buttonText = confirmButton.GetComponentInChildren<TMP_Text>();

        if (buttonText != null)
        {
            buttonText.text = selectedPlayers.Count == maxPlayers
                ? "CONFIRM SQUAD"
                : "SELECT 26 PLAYERS";
        }
    }

    bool CanSelectPlayer(PlayerData player)
    {
        int gk = 0;
        int def = 0;
        int mid = 0;
        int att = 0;

        foreach (PlayerData p in selectedPlayers)
        {
            switch (GetPositionCategory(p.position))
            {
                case "GK": gk++; break;
                case "DEF": def++; break;
                case "MID": mid++; break;
                case "ATT": att++; break;
            }
        }

        string category = GetPositionCategory(player.position);

        if (category == "GK" && gk >= maxGoalkeepers) return false;
        if (category == "DEF" && def >= maxDefenders) return false;
        if (category == "MID" && mid >= maxMidfielders) return false;
        if (category == "ATT" && att >= maxAttackers) return false;

        return true;
    }

    string GetPositionCategory(string position)
    {
        switch (position)
        {
            case "GK":
                return "GK";

            case "CB":
            case "LB":
            case "RB":
                return "DEF";

            case "DM":
            case "CM":
            case "CAM":
            case "AM":
                return "MID";

            case "LW":
            case "RW":
            case "ST":
                return "ATT";
        }

        return "MID";
    }

    void UpdateSelectedText()
    {
        int gk = 0;
        int def = 0;
        int mid = 0;
        int att = 0;

        foreach (PlayerData player in selectedPlayers)
        {
            switch (GetPositionCategory(player.position))
            {
                case "GK": gk++; break;
                case "DEF": def++; break;
                case "MID": mid++; break;
                case "ATT": att++; break;
            }
        }

        string confirmedMessage = squadConfirmed
            ? "\n\nSQUAD CONFIRMED ✓"
            : "";

        selectedText.text =
            "Selected: "+
            " (GK: " + gk + " / " + maxGoalkeepers+")" +
            " (DEF: " + def + " / " + maxDefenders+")" +
            " (MID: " + mid + " / " + maxMidfielders+")" +
            " (ATT: " + att + " / " + maxAttackers+")" +
            confirmedMessage;
    }
}