using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Creates the complete categorized training menu automatically at runtime.
/// Add this component once to TrainingFieldManager in TrainingScene.
/// </summary>
public class TrainingProgramMenuGenerator : MonoBehaviour
{
    [Header("Optional")]
    [Tooltip("Turn this on only after you want to hide the old world stations.")]
    public bool hideOldWorldStations = false;

    private TrainingManager trainingManager;
    private TrainingFieldManager trainingFieldManager;
    private Canvas canvas;
    private TextMeshProUGUI activeTrainingText;

    private GameObject menuPanel;
    private GameObject previewPanel;
    private Transform menuContent;
    private TextMeshProUGUI previewTitleText;
    private TextMeshProUGUI previewBodyText;
    private Button confirmButton;

    private TrainingPreview pendingPreview;
    private PlayerToken pendingPlayerToken;

    private readonly List<TrainingEntry> individualTraining =
        new List<TrainingEntry>
        {
            new TrainingEntry(TrainingType.FinishingDrill, "Finishing Drill"),
            new TrainingEntry(TrainingType.SpeedSprint, "Speed & Sprint"),
            new TrainingEntry(TrainingType.DefensiveTechnique, "Defensive Technique"),
            new TrainingEntry(TrainingType.GoalkeeperReflexes, "Goalkeeper Reflexes"),
            new TrainingEntry(TrainingType.AllRoundSession, "All-Round Session")
        };

    private readonly List<TrainingEntry> unitTraining =
        new List<TrainingEntry>
        {
            new TrainingEntry(TrainingType.AttackUnitTraining, "Attack Unit Training"),
            new TrainingEntry(TrainingType.MidfieldControl, "Midfield Control"),
            new TrainingEntry(TrainingType.DefensiveShape, "Defensive Shape"),
            new TrainingEntry(TrainingType.GoalkeeperBacklineDrill, "Goalkeeper + Backline Drill")
        };

    private readonly List<TrainingEntry> teamTraining =
        new List<TrainingEntry>
        {
            new TrainingEntry(TrainingType.TeamBonding, "Team Bonding"),
            new TrainingEntry(TrainingType.FormationRehearsal, "Formation Rehearsal"),
            new TrainingEntry(TrainingType.SetPieceTraining, "Set Piece Training"),
            new TrainingEntry(TrainingType.HighPressSystem, "High Press System"),
            new TrainingEntry(TrainingType.DefensiveTransition, "Defensive Transition"),
            new TrainingEntry(TrainingType.VideoAnalysis, "Video Analysis")
        };

    private void Start()
    {
        trainingManager = FindFirstObjectByType<TrainingManager>();
        trainingFieldManager = FindFirstObjectByType<TrainingFieldManager>();
        canvas = FindFirstObjectByType<Canvas>();

        if (trainingManager == null)
        {
            Debug.LogError("TrainingProgramMenuGenerator: TrainingManager not found.");
            return;
        }

        if (canvas == null)
        {
            Debug.LogError("TrainingProgramMenuGenerator: Canvas not found.");
            return;
        }

        RemoveOldGeneratedMenus();

        if (hideOldWorldStations)
        {
            HideOldWorldStations();
        }

        BuildMenu();
        BuildPreview();
    }

    private void Update()
    {
        RefreshActiveTrainingText();
    }

    private void RemoveOldGeneratedMenus()
    {
        Transform oldMenu = canvas.transform.Find("GeneratedTrainingPrograms");
        if (oldMenu != null)
        {
            Destroy(oldMenu.gameObject);
        }

        Transform oldPreview = canvas.transform.Find("GeneratedTrainingPreview");
        if (oldPreview != null)
        {
            Destroy(oldPreview.gameObject);
        }

        Transform oldToggle = canvas.transform.Find("TrainingProgramsToggle");
        if (oldToggle != null)
        {
            Destroy(oldToggle.gameObject);
        }
    }

    private void HideOldWorldStations()
    {
        string[] stationNames =
        {
            "ShootingStation",
            "SpeedStation",
            "DefenseStation",
            "GoalkeeperStation",
            "TeamTrainingStation"
        };

        foreach (string stationName in stationNames)
        {
            GameObject station = GameObject.Find(stationName);
            if (station != null)
            {
                station.SetActive(false);
            }
        }
    }

    private void BuildMenu()
    {
        CreateToggleButton();

        menuPanel = CreatePanel(
            "GeneratedTrainingPrograms",
            canvas.transform,
            new Vector2(0.5f, 0.5f),
            new Vector2(670f, 1040f),
            new Color(0.04f, 0.08f, 0.12f, 0.96f)
        );

        CreateText(
            "Title",
            menuPanel.transform,
            "TRAINING PROGRAMS",
            42,
            TextAlignmentOptions.Center,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -42f),
            new Vector2(590f, 64f)
        );

        Button closeButton = CreateButton(
            "CloseButton",
            menuPanel.transform,
            "X",
            new Vector2(0.92f, 0.94f),
            new Vector2(76f, 58f)
        );
        closeButton.onClick.AddListener(() => menuPanel.SetActive(false));

        Transform tabRoot = CreateRect("Tabs", menuPanel.transform);
        RectTransform tabRect = tabRoot.GetComponent<RectTransform>();
        tabRect.anchorMin = new Vector2(0.5f, 1f);
        tabRect.anchorMax = new Vector2(0.5f, 1f);
        tabRect.pivot = new Vector2(0.5f, 1f);
        tabRect.anchoredPosition = new Vector2(0f, -118f);
        tabRect.sizeDelta = new Vector2(600f, 70f);

        HorizontalLayoutGroup tabLayout = tabRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
        tabLayout.spacing = 10f;
        tabLayout.childAlignment = TextAnchor.MiddleCenter;
        tabLayout.childControlWidth = true;
        tabLayout.childControlHeight = true;
        tabLayout.childForceExpandWidth = true;
        tabLayout.childForceExpandHeight = true;

        Button individualTab = CreateLayoutButton("IndividualTab", tabRoot, "INDIVIDUAL");
        Button unitTab = CreateLayoutButton("UnitTab", tabRoot, "UNIT");
        Button teamTab = CreateLayoutButton("TeamTab", tabRoot, "TEAM");

        individualTab.onClick.AddListener(() => ShowCategory(TrainingScope.Individual));
        unitTab.onClick.AddListener(() => ShowCategory(TrainingScope.Unit));
        teamTab.onClick.AddListener(() => ShowCategory(TrainingScope.Team));

        ScrollRect scrollRect = CreateScrollView(menuPanel.transform, out menuContent);
        scrollRect.verticalNormalizedPosition = 1f;

        activeTrainingText = CreateText(
            "ActiveTrainingText",
            menuPanel.transform,
            "NO ACTIVE TRAINING",
            22,
            TextAlignmentOptions.Center,
            new Vector2(0.5f, 0.13f),
            new Vector2(0.5f, 0.13f),
            Vector2.zero,
            new Vector2(560f, 100f)
        );

       
       

        RefreshActiveTrainingText();

        menuPanel.SetActive(false);
        ShowCategory(TrainingScope.Individual);
    }

    private void BuildPreview()
    {
        previewPanel = CreatePanel(
            "GeneratedTrainingPreview",
            canvas.transform,
            new Vector2(0.5f, 0.5f),
            new Vector2(700f, 780f),
            new Color(0.02f, 0.04f, 0.08f, 0.98f)
        );

        previewTitleText = CreateText(
            "PreviewTitle",
            previewPanel.transform,
            "TRAINING PREVIEW",
            40,
            TextAlignmentOptions.Center,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -48f),
            new Vector2(620f, 70f)
        );

        previewBodyText = CreateText(
            "PreviewBody",
            previewPanel.transform,
            "",
            28,
            TextAlignmentOptions.Center,
            new Vector2(0.5f, 0.55f),
            new Vector2(0.5f, 0.55f),
            Vector2.zero,
            new Vector2(620f, 500f)
        );
        previewBodyText.enableWordWrapping = true;

        confirmButton = CreateButton(
            "GeneratedConfirmButton",
            previewPanel.transform,
            "CONFIRM TRAINING",
            new Vector2(0.30f, 0.08f),
            new Vector2(270f, 64f)
        );
        confirmButton.onClick.AddListener(ConfirmTraining);

        Button cancelButton = CreateButton(
            "GeneratedCancelButton",
            previewPanel.transform,
            "CANCEL",
            new Vector2(0.70f, 0.08f),
            new Vector2(220f, 64f)
        );
        cancelButton.onClick.AddListener(CancelPreview);

        previewPanel.SetActive(false);
    }

    private void CreateToggleButton()
    {
        Button button = CreateButton(
            "TrainingProgramsToggle",
            canvas.transform,
            "TRAINING\nPROGRAMS",
            new Vector2(0.89f, 0.90f),
            new Vector2(230f, 88f)
        );

        button.onClick.AddListener(() =>
        {
            menuPanel.SetActive(true);
            ShowCategory(TrainingScope.Individual);
        });
    }

    private void ShowCategory(TrainingScope scope)
    {
        if (menuContent == null)
        {
            return;
        }

        for (int i = menuContent.childCount - 1; i >= 0; i--)
        {
            Destroy(menuContent.GetChild(i).gameObject);
        }

        string title = scope == TrainingScope.Individual
            ? "INDIVIDUAL PLAYER TRAINING"
            : scope == TrainingScope.Unit
                ? "UNIT TRAINING"
                : "FULL TEAM TRAINING";

        TextMeshProUGUI header = CreateLayoutText(
            "CategoryHeader",
            menuContent,
            title,
            31,
            TextAlignmentOptions.Center
        );
        header.color = new Color(1f, 0.82f, 0.24f, 1f);
        header.gameObject.AddComponent<LayoutElement>().preferredHeight = 56f;

        List<TrainingEntry> entries = scope == TrainingScope.Individual
            ? individualTraining
            : scope == TrainingScope.Unit
                ? unitTraining
                : teamTraining;

        foreach (TrainingEntry entry in entries)
        {
            TrainingEntry capturedEntry = entry;

            Button option = CreateLayoutButton(
                capturedEntry.displayName.Replace(" ", ""),
                menuContent,
                capturedEntry.displayName
            );

            LayoutElement layout = option.gameObject.GetComponent<LayoutElement>();
            layout.preferredHeight = 74f;

            option.onClick.AddListener(() =>
            {
                OpenTrainingPreview(capturedEntry.type, capturedEntry.displayName);
            });
        }
    }

    private void OpenTrainingPreview(
        TrainingType type,
        string displayName
    )
    {
        if (trainingManager == null)
        {
            return;
        }

        pendingPlayerToken = null;
        PlayerData targetPlayer = null;

        if (IsIndividualTraining(type))
        {
            pendingPlayerToken = trainingFieldManager != null
                ? trainingFieldManager.SelectedPlayerToken
                : FindSelectedPlayerToken();

            if (pendingPlayerToken != null)
            {
                targetPlayer = pendingPlayerToken.Player;
            }
        }

        pendingPreview = trainingManager.GetTrainingPreview(
            targetPlayer,
            type
        );

        previewTitleText.text = displayName;
        previewBodyText.text = pendingPreview.description;
        confirmButton.interactable = pendingPreview.canStart;

        previewPanel.SetActive(true);
    }

    private void ConfirmTraining()
    {
        if (pendingPreview == null || !pendingPreview.canStart)
        {
            return;
        }

        bool started = trainingFieldManager != null
            ? trainingFieldManager.StartTrainingFromProgramsMenu(
                pendingPreview
            )
            : trainingManager.StartTraining(pendingPreview);

        if (!started)
        {
            previewBodyText.text = "TRAINING COULD NOT START.";
            confirmButton.interactable = false;
            return;
        }

        CancelPreview();
        RefreshExistingTrainingScene();
        RefreshActiveTrainingText();
    }

    private void CancelPreview()
    {
        pendingPreview = null;
        pendingPlayerToken = null;
        previewPanel.SetActive(false);
    }

    private PlayerToken FindSelectedPlayerToken()
    {
        PlayerToken[] tokens = FindObjectsByType<PlayerToken>(
            FindObjectsSortMode.None
        );

        foreach (PlayerToken token in tokens)
        {
            if (token == null || token.Player == null)
            {
                continue;
            }

            SpriteRenderer renderer = token.GetComponent<SpriteRenderer>();

            if (renderer == null)
            {
                continue;
            }

            Color color = renderer.color;

            // PlayerToken.SetSelected uses (1, 0.75, 0.15).
            bool selectedGold =
                color.r > 0.9f &&
                color.g > 0.55f &&
                color.g < 0.9f &&
                color.b < 0.3f;

            if (selectedGold)
            {
                return token;
            }
        }

        return null;
    }

    private void RefreshExistingTrainingScene()
    {
        if (trainingFieldManager == null)
        {
            trainingFieldManager =
                FindFirstObjectByType<TrainingFieldManager>();
        }

        if (trainingFieldManager != null)
        {
            trainingFieldManager.RefreshAll();
        }
    }

    private void RefreshActiveTrainingText()
    {
        if (activeTrainingText == null || trainingManager == null)
        {
            return;
        }

        activeTrainingText.text =
            trainingManager.HasActiveTraining
                ? "ACTIVE TRAINING\n" +
                  trainingManager.GetActiveTrainingSummary()
                : "NO ACTIVE TRAINING";
    }

    private bool IsIndividualTraining(TrainingType type)
    {
        return type == TrainingType.FinishingDrill ||
               type == TrainingType.SpeedSprint ||
               type == TrainingType.DefensiveTechnique ||
               type == TrainingType.GoalkeeperReflexes ||
               type == TrainingType.AllRoundSession;
    }

    private GameObject CreatePanel(
        string objectName,
        Transform parent,
        Vector2 anchor,
        Vector2 size,
        Color color
    )
    {
        GameObject panel = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(Image)
        );

        panel.transform.SetParent(parent, false);

        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;

        panel.GetComponent<Image>().color = color;
        return panel;
    }

    private Transform CreateRect(string objectName, Transform parent)
    {
        GameObject go = new GameObject(
            objectName,
            typeof(RectTransform)
        );
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private TextMeshProUGUI CreateText(
        string objectName,
        Transform parent,
        string value,
        float fontSize,
        TextAlignmentOptions alignment,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 anchoredPosition,
        Vector2 size
    )
    {
        GameObject go = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(TextMeshProUGUI)
        );

        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.enableWordWrapping = true;
        return text;
    }

    private TextMeshProUGUI CreateLayoutText(
        string objectName,
        Transform parent,
        string value,
        float fontSize,
        TextAlignmentOptions alignment
    )
    {
        GameObject go = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(TextMeshProUGUI)
        );

        go.transform.SetParent(parent, false);

        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.enableWordWrapping = true;
        return text;
    }

    private Button CreateButton(
        string objectName,
        Transform parent,
        string label,
        Vector2 anchor,
        Vector2 size
    )
    {
        GameObject go = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(Image),
            typeof(Button)
        );

        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;

        Image image = go.GetComponent<Image>();
        image.color = new Color(0.12f, 0.38f, 0.62f, 1f);

        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;

        TextMeshProUGUI text = CreateText(
            "Text",
            go.transform,
            label,
            25,
            TextAlignmentOptions.Center,
            Vector2.zero,
            Vector2.one,
            Vector2.zero,
            Vector2.zero
        );
        text.raycastTarget = false;

        return button;
    }

    private Button CreateLayoutButton(
        string objectName,
        Transform parent,
        string label
    )
    {
        GameObject go = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(Image),
            typeof(Button),
            typeof(LayoutElement)
        );

        go.transform.SetParent(parent, false);

        Image image = go.GetComponent<Image>();
        image.color = new Color(0.10f, 0.32f, 0.52f, 1f);

        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;

        TextMeshProUGUI text = CreateText(
            "Text",
            go.transform,
            label,
            27,
            TextAlignmentOptions.Center,
            Vector2.zero,
            Vector2.one,
            Vector2.zero,
            Vector2.zero
        );
        text.raycastTarget = false;

        return button;
    }

    private ScrollRect CreateScrollView(
        Transform parent,
        out Transform content
    )
    {
        GameObject scrollGo = new GameObject(
            "TrainingScrollView",
            typeof(RectTransform),
            typeof(Image),
            typeof(ScrollRect)
        );
        scrollGo.transform.SetParent(parent, false);

        RectTransform scrollRectTransform =
            scrollGo.GetComponent<RectTransform>();
        scrollRectTransform.anchorMin = new Vector2(0f, 0f);
        scrollRectTransform.anchorMax = new Vector2(1f, 1f);
        scrollRectTransform.offsetMin = new Vector2(34f, 105f);
        scrollRectTransform.offsetMax = new Vector2(-34f, -170f);

        scrollGo.GetComponent<Image>().color =
            new Color(0f, 0f, 0f, 0.15f);

        GameObject viewportGo = new GameObject(
            "Viewport",
            typeof(RectTransform),
            typeof(Image),
            typeof(Mask)
        );
        viewportGo.transform.SetParent(scrollGo.transform, false);

        RectTransform viewportRect =
            viewportGo.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;

        Image viewportImage = viewportGo.GetComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.01f);
        viewportGo.GetComponent<Mask>().showMaskGraphic = false;

        GameObject contentGo = new GameObject(
            "Content",
            typeof(RectTransform),
            typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter)
        );
        contentGo.transform.SetParent(viewportGo.transform, false);

        RectTransform contentRect = contentGo.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup layout =
            contentGo.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(14, 14, 18, 18);
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter =
            contentGo.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scrollRect = scrollGo.GetComponent<ScrollRect>();
        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;

        content = contentGo.transform;
        return scrollRect;
    }

    private class TrainingEntry
    {
        public TrainingType type;
        public string displayName;

        public TrainingEntry(TrainingType type, string displayName)
        {
            this.type = type;
            this.displayName = displayName;
        }
    }
}
