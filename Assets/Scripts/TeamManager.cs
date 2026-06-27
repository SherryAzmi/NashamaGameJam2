using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TeamManager : MonoBehaviour
{
    public List<PlayerData> selectedPlayers = new List<PlayerData>();
    [HideInInspector] public List<PlayerData> startingEleven = new List<PlayerData>();
    [HideInInspector] public List<PlayerData> benchPlayers = new List<PlayerData>();
    [HideInInspector] public bool formationInitialized = false;
    [HideInInspector] public int substitutionsUsed = 0;

    private static TeamManager instance;
    public static TeamManager Instance => instance;

    public PlayerDatabase database;

    [Header("Existing UI")]
    public Transform content;
    public GameObject playerCardPrefab;
    public TMP_Text selectedText;
    public Button confirmButton;

    [Header("Club Browser")]
    [Tooltip("Optional. Assign a logo Sprite for every club name and for PLAYERS ABROAD.")]
    [SerializeField] private List<ClubVisual> clubVisuals =
        new List<ClubVisual>();

    [Tooltip("Optional fallback logo when a club does not have its own Sprite.")]
    [SerializeField] private Sprite defaultClubLogo;

    private const string AbroadGroupName = "PLAYERS ABROAD";

    private readonly List<PlayerCardUI> spawnedCards =
        new List<PlayerCardUI>();

    private readonly Dictionary<string, List<PlayerData>> playersByGroup =
        new Dictionary<string, List<PlayerData>>(
            StringComparer.OrdinalIgnoreCase
        );

    private int maxPlayers = 26;

    private int maxGoalkeepers = 3;
    private int maxDefenders = 8;
    private int maxMidfielders = 8;
    private int maxAttackers = 7;

    private bool squadConfirmed = false;
    public bool SquadConfirmed => squadConfirmed;

    [Serializable]
    public class ClubVisual
    {
        [Tooltip("Write exactly the club name from PlayerData, or PLAYERS ABROAD.")]
        public string clubName;

        public Sprite logo;
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

        RestoreFromSave();
    }

    // Restores the confirmed squad from the shared save snapshot, if any.
    // The squad list and the "team selected" flag are restored together in
    // this one method so GameProgressManager never sees one without the
    // other (the exact split this project's save design explicitly avoids).
    private void RestoreFromSave()
    {
        GameSaveData save = SaveManager.PendingLoadData;

        if (save == null || database == null)
        {
            return;
        }

        SquadSaveData data = save.squad;

        selectedPlayers = ResolvePlayers(data.selectedPlayerNames);
        startingEleven = ResolvePlayers(data.startingElevenNames);
        benchPlayers = ResolvePlayers(data.benchPlayerNames);
        formationInitialized = data.formationInitialized;
        substitutionsUsed = data.substitutionsUsed;
        squadConfirmed = data.squadConfirmed;

        if (GameProgressManager.Instance != null)
        {
            GameProgressManager.Instance.ApplyLoadedSquadFlag(squadConfirmed);
        }
    }

    private List<PlayerData> ResolvePlayers(List<string> playerNames)
    {
        List<PlayerData> resolved = new List<PlayerData>();

        foreach (string playerName in playerNames)
        {
            PlayerData player = database.players.Find(p => p != null && p.name == playerName);

            if (player != null)
            {
                resolved.Add(player);
            }
        }

        return resolved;
    }

    // Called by SaveManager to fill in this manager's section of the save.
    public void WriteSaveData(SquadSaveData data)
    {
        data.selectedPlayerNames = selectedPlayers.ConvertAll(p => p.name);
        data.startingElevenNames = startingEleven.ConvertAll(p => p.name);
        data.benchPlayerNames = benchPlayers.ConvertAll(p => p.name);
        data.formationInitialized = formationInitialized;
        data.substitutionsUsed = substitutionsUsed;
        data.squadConfirmed = squadConfirmed;
    }

    private void Start()
    {
        BuildPlayerGroups();
        ShowClubCards();

        confirmButton.onClick.RemoveAllListeners();
        confirmButton.onClick.AddListener(ConfirmSquad);

        UpdateSelectedText();
        UpdateConfirmButton();
    }

    // Main screen: one image card for each Jordanian club + one Abroad card.
    public void ShowClubCards()
    {
        ClearContent();

        foreach (string groupName in GetOrderedGroupNames())
        {
            CreateClubCard(groupName, playersByGroup[groupName]);
        }
    }

    // Called when a club card is pressed.
    private void ShowPlayersForClub(string groupName)
    {
        if (!playersByGroup.ContainsKey(groupName))
        {
            return;
        }

        ClearContent();
        CreateBackToClubsButton();
        CreateGroupHeader(groupName, playersByGroup[groupName].Count);

        List<PlayerData> sortedPlayers =
            playersByGroup[groupName]
                .OrderBy(GetPositionSortOrder)
                .ThenBy(player => player.playerName)
                .ToList();

        foreach (PlayerData player in sortedPlayers)
        {
            CreatePlayerCard(player);
        }
    }

    private void BuildPlayerGroups()
    {
        playersByGroup.Clear();

        if (database == null || database.players == null)
        {
            Debug.LogError("Player Database is missing.");
            return;
        }

        foreach (PlayerData player in database.players)
        {
            if (player == null)
            {
                continue;
            }

            string groupName = IsAbroadPlayer(player)
                ? AbroadGroupName
                : GetSafeClubName(player.club);

            if (!playersByGroup.ContainsKey(groupName))
            {
                playersByGroup[groupName] = new List<PlayerData>();
            }

            playersByGroup[groupName].Add(player);
        }
    }

    private bool IsAbroadPlayer(PlayerData player)
    {
        return player != null &&
               string.Equals(
                   player.category,
                   "Abroad",
                   StringComparison.OrdinalIgnoreCase
               );
    }

    private string GetSafeClubName(string clubName)
    {
        return string.IsNullOrWhiteSpace(clubName)
            ? "OTHER JORDANIAN CLUBS"
            : clubName.Trim();
    }

    private List<string> GetOrderedGroupNames()
    {
        List<string> ordered = new List<string>();

        // These appear first when they exist in your Player Database.
        string[] preferredOrder =
        {
            "Al Wehdat",
            "Al Faisaly",
            "Al Hussein Irbid",
            "Al Ramtha",
            "Al Jazeera",
            "Al Salt"
        };

        foreach (string clubName in preferredOrder)
        {
            if (playersByGroup.ContainsKey(clubName))
            {
                ordered.Add(clubName);
            }
        }

        foreach (string groupName in playersByGroup.Keys
                     .Where(name =>
                         !string.Equals(
                             name,
                             AbroadGroupName,
                             StringComparison.OrdinalIgnoreCase
                         ) &&
                         !ordered.Contains(name)
                     )
                     .OrderBy(name => name))
        {
            ordered.Add(groupName);
        }

        if (playersByGroup.ContainsKey(AbroadGroupName))
        {
            ordered.Add(AbroadGroupName);
        }

        return ordered;
    }

    private void ClearContent()
    {
        spawnedCards.Clear();

        if (content == null)
        {
            return;
        }

        for (int i = content.childCount - 1; i >= 0; i--)
        {
            Destroy(content.GetChild(i).gameObject);
        }
    }

    private void CreateClubCard(
        string groupName,
        List<PlayerData> players
    )
    {
        if (content == null)
        {
            return;
        }

        GameObject cardObject = new GameObject(
            groupName + " Club Card",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button),
            typeof(LayoutElement),
            typeof(HorizontalLayoutGroup)
        );

        cardObject.transform.SetParent(content, false);

        LayoutElement layout = cardObject.GetComponent<LayoutElement>();
        layout.preferredHeight = 185f;

        Image background = cardObject.GetComponent<Image>();
        background.color = IsAbroadGroup(groupName)
            ? new Color(0.08f, 0.20f, 0.38f, 1f)
            : new Color(0.02f, 0.25f, 0.14f, 1f);

        HorizontalLayoutGroup horizontal =
            cardObject.GetComponent<HorizontalLayoutGroup>();
        horizontal.padding = new RectOffset(22, 22, 18, 18);
        horizontal.spacing = 22f;
        horizontal.childAlignment = TextAnchor.MiddleLeft;
        horizontal.childControlWidth = true;
        horizontal.childControlHeight = true;
        horizontal.childForceExpandWidth = false;
        horizontal.childForceExpandHeight = true;

        GameObject logoObject = new GameObject(
            "Club Logo",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(LayoutElement)
        );
        logoObject.transform.SetParent(cardObject.transform, false);

        LayoutElement logoLayout =
            logoObject.GetComponent<LayoutElement>();
        logoLayout.preferredWidth = 130f;
        logoLayout.preferredHeight = 130f;
        logoLayout.flexibleWidth = 0f;

        Sprite clubLogo = FindClubLogo(groupName);

        Image logoImage = logoObject.GetComponent<Image>();
        logoImage.sprite = clubLogo;
        logoImage.overrideSprite = clubLogo;
        logoImage.type = Image.Type.Simple;
        logoImage.preserveAspect = true;

        // No default grey UI square when a logo is missing.
        logoImage.enabled = clubLogo != null;

        Debug.Log(
            "Club logo: " + groupName + " -> " +
            (clubLogo != null ? clubLogo.name : "MISSING")
        );

        GameObject textArea = new GameObject(
            "Text Area",
            typeof(RectTransform),
            typeof(VerticalLayoutGroup),
            typeof(LayoutElement)
        );
        textArea.transform.SetParent(cardObject.transform, false);

        LayoutElement textLayout =
            textArea.GetComponent<LayoutElement>();
        textLayout.flexibleWidth = 1f;
        textLayout.minWidth = 0f;


        VerticalLayoutGroup vertical =
            textArea.GetComponent<VerticalLayoutGroup>();
        vertical.childAlignment = TextAnchor.MiddleLeft;
        vertical.spacing = 8f;
        vertical.childControlWidth = true;
        vertical.childControlHeight = false;
        vertical.childForceExpandHeight = false;

        CreateCardText(
            "Club Name",
            textArea.transform,
            groupName.ToUpperInvariant(),
            34f,
            FontStyles.Bold,
            Color.white,
            52f
        );

        CreateCardText(
            "Player Count",
            textArea.transform,
            players.Count + " PLAYERS",
            23f,
            FontStyles.Normal,
            new Color(0.85f, 0.92f, 0.88f, 1f),
            38f
        );

        Button button = cardObject.GetComponent<Button>();
        button.targetGraphic = background;

        string capturedGroupName = groupName;
        button.onClick.AddListener(() =>
        {
            ShowPlayersForClub(capturedGroupName);
        });
    }

    private void CreateBackToClubsButton()
    {
        GameObject backObject = new GameObject(
            "Back To Clubs Button",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button),
            typeof(LayoutElement)
        );

        backObject.transform.SetParent(content, false);

        LayoutElement layout = backObject.GetComponent<LayoutElement>();
        layout.preferredHeight = 78f;

        Image background = backObject.GetComponent<Image>();
        background.color = new Color(0.12f, 0.18f, 0.42f, 1f);

        Button button = backObject.GetComponent<Button>();
        button.targetGraphic = background;
        button.onClick.AddListener(ShowClubCards);

        TextMeshProUGUI label = CreateStretchText(
            "Text",
            backObject.transform,
            "← BACK TO CLUBS",
            27f,
            FontStyles.Bold,
            Color.white,
            TextAlignmentOptions.Center
        );
        label.raycastTarget = false;
    }

    private void CreateGroupHeader(string groupName, int count)
    {
        GameObject headerObject = new GameObject(
            "Club Header",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(LayoutElement)
        );

        headerObject.transform.SetParent(content, false);

        LayoutElement layout = headerObject.GetComponent<LayoutElement>();
        layout.preferredHeight = 90f;

        Image background = headerObject.GetComponent<Image>();
        background.color = new Color(0.03f, 0.11f, 0.07f, 1f);

        TextMeshProUGUI label = CreateStretchText(
            "Text",
            headerObject.transform,
            groupName.ToUpperInvariant() + "\n" + count + " PLAYERS",
            25f,
            FontStyles.Bold,
            new Color(0.95f, 0.95f, 0.95f, 1f),
            TextAlignmentOptions.Center
        );
        label.raycastTarget = false;
    }

    private void CreatePlayerCard(PlayerData player)
    {
        if (playerCardPrefab == null || content == null)
        {
            Debug.LogError("Player Card Prefab or Content is missing.");
            return;
        }

        GameObject card = Instantiate(playerCardPrefab, content);

        PlayerCardUI cardUI = card.GetComponent<PlayerCardUI>();

        if (cardUI == null)
        {
            Debug.LogError("PlayerCard Prefab is missing PlayerCardUI.");
            Destroy(card);
            return;
        }

        cardUI.Setup(player, this);

        // The selection stays visible when the user goes back to clubs
        // then opens the same club again.
        cardUI.SetSelected(selectedPlayers.Contains(player));

        if (squadConfirmed)
        {
            cardUI.SetLocked(true);
        }

        spawnedCards.Add(cardUI);
    }

    private Sprite FindClubLogo(string groupName)
    {
        string requestedKey = NormalizeClubKey(groupName);

        foreach (ClubVisual visual in clubVisuals)
        {
            if (visual == null || visual.logo == null)
            {
                continue;
            }

            if (NormalizeClubKey(visual.clubName) == requestedKey)
            {
                return visual.logo;
            }
        }

        return defaultClubLogo;
    }

    // Handles invisible spaces, different capitalization, hyphens,
    // and accidental extra spaces in a club name.
    private string NormalizeClubKey(string clubName)
    {
        if (string.IsNullOrWhiteSpace(clubName))
        {
            return string.Empty;
        }

        return clubName
            .Trim()
            .Replace(" ", string.Empty)
            .Replace("-", string.Empty)
            .Replace("_", string.Empty)
            .ToUpperInvariant();
    }

    private bool IsAbroadGroup(string groupName)
    {
        return string.Equals(
            groupName,
            AbroadGroupName,
            StringComparison.OrdinalIgnoreCase
        );
    }

    private TextMeshProUGUI CreateCardText(
        string objectName,
        Transform parent,
        string value,
        float fontSize,
        FontStyles style,
        Color color,
        float preferredHeight
    )
    {
        GameObject textObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(TextMeshProUGUI),
            typeof(LayoutElement)
        );

        textObject.transform.SetParent(parent, false);

        LayoutElement layout = textObject.GetComponent<LayoutElement>();
        layout.preferredHeight = preferredHeight;

        TextMeshProUGUI text =
            textObject.GetComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = TextAlignmentOptions.Left;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;

        return text;
    }

    private TextMeshProUGUI CreateStretchText(
        string objectName,
        Transform parent,
        string value,
        float fontSize,
        FontStyles style,
        Color color,
        TextAlignmentOptions alignment
    )
    {
        GameObject textObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(TextMeshProUGUI)
        );

        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        TextMeshProUGUI text =
            textObject.GetComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = alignment;
        text.enableWordWrapping = true;
        text.raycastTarget = false;

        return text;
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

    private void ConfirmSquad()
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

        TMP_Text buttonText =
            confirmButton.GetComponentInChildren<TMP_Text>();

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

        TrainingManager trainingManager =
            FindFirstObjectByType<TrainingManager>();

        if (trainingManager != null)
        {
            trainingManager.ClearActiveTrainingForNewSquad();
        }

        if (GameProgressManager.Instance != null)
        {
            GameProgressManager.Instance.MarkTeamSelected();
        }

        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.SaveCurrentState();
        }

        SceneManager.LoadScene("HomeScene");
    }

    private void UpdateConfirmButton()
    {
        if (squadConfirmed)
        {
            return;
        }

        confirmButton.interactable =
            selectedPlayers.Count == maxPlayers;

        TMP_Text buttonText =
            confirmButton.GetComponentInChildren<TMP_Text>();

        if (buttonText != null)
        {
            buttonText.text = selectedPlayers.Count == maxPlayers
                ? "CONFIRM SQUAD"
                : "SELECT 26 PLAYERS";
        }
    }

    private bool CanSelectPlayer(PlayerData player)
    {
        int gk = 0;
        int def = 0;
        int mid = 0;
        int att = 0;

        foreach (PlayerData selectedPlayer in selectedPlayers)
        {
            switch (GetPositionCategory(selectedPlayer.position))
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

    private string GetPositionCategory(string position)
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

    private int GetPositionSortOrder(PlayerData player)
    {
        switch (GetPositionCategory(player.position))
        {
            case "GK": return 0;
            case "DEF": return 1;
            case "MID": return 2;
            case "ATT": return 3;
            default: return 4;
        }
    }

    private void UpdateSelectedText()
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
            "Selected: " +
            " (GK: " + gk + " / " + maxGoalkeepers + ")" +
            " (DEF: " + def + " / " + maxDefenders + ")" +
            " (MID: " + mid + " / " + maxMidfielders + ")" +
            " (ATT: " + att + " / " + maxAttackers + ")" +
            confirmedMessage;
    }
}
