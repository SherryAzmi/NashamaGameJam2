using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Lives in CampaignScene, as a panel sibling to the normal fixture-list UI.
// Knockout play has no group table, so "standings" here is a scrollable
// list of all 16 teams drawn into the World Cup, each with their power
// rating and current status (still in / eliminated in round X / champion).
// Opened on demand via a permanent "View Standings" button in the hub.
public class WorldCupStandingsController : MonoBehaviour
{
    [Header("Header")]
    public TMP_Text headerText;

    [Tooltip("Content transform of a ScrollRect, with a VerticalLayoutGroup - one row is built per team under here.")]
    public Transform rowsContainer;

    public Button closeButton;

    private static readonly Color ChampionColor = new Color(0.9f, 0.75f, 0.2f, 1f);
    private static readonly Color StillInColor = Color.white;
    private static readonly Color EliminatedColor = new Color(0.6f, 0.6f, 0.6f, 1f);

    public void Show(CampaignState state, CampaignHubController hub)
    {
        headerText.text = "WORLD CUP - FIELD OF 16";

        for (int i = rowsContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(rowsContainer.GetChild(i).gameObject);
        }

        foreach (NationalTeamData team in state.WorldCupField)
        {
            BuildRow(team, state.GetTeamStatus(team));
        }

        closeButton.onClick.RemoveAllListeners();
        closeButton.onClick.AddListener(() =>
        {
            gameObject.SetActive(false);
            hub.Render();
        });

        gameObject.SetActive(true);
    }

    private void BuildRow(NationalTeamData team, string status)
    {
        GameObject row = new GameObject("TeamRow", typeof(RectTransform));
        row.transform.SetParent(rowsContainer, false);

        LayoutElement rowLayout = row.AddComponent<LayoutElement>();
        rowLayout.preferredHeight = 48f;
        rowLayout.minHeight = 48f;

        HorizontalLayoutGroup rowGroup = row.AddComponent<HorizontalLayoutGroup>();
        rowGroup.spacing = 10f;
        rowGroup.padding = new RectOffset(6, 6, 4, 4);
        rowGroup.childAlignment = TextAnchor.MiddleLeft;
        rowGroup.childForceExpandWidth = false;
        rowGroup.childForceExpandHeight = true;
        rowGroup.childControlWidth = true;
        rowGroup.childControlHeight = true;

        GameObject flagGo = new GameObject("Flag", typeof(RectTransform));
        flagGo.transform.SetParent(row.transform, false);
        Image flagImage = flagGo.AddComponent<Image>();
        flagImage.preserveAspect = true;
        Sprite flag = team != null ? team.flag : null;
        flagImage.sprite = flag;
        flagImage.enabled = flag != null;
        LayoutElement flagLayout = flagGo.AddComponent<LayoutElement>();
        flagLayout.preferredWidth = 28f;
        flagLayout.preferredHeight = 28f;

        GameObject nameGo = new GameObject("Name", typeof(RectTransform));
        nameGo.transform.SetParent(row.transform, false);
        TMP_Text nameText = nameGo.AddComponent<TextMeshProUGUI>();
        nameText.text = team != null
            ? (string.IsNullOrWhiteSpace(team.teamName) ? team.name.ToUpperInvariant() : team.teamName.ToUpperInvariant())
            : "?";
        nameText.fontSize = 20f;
        nameText.color = Color.white;
        nameText.alignment = TextAlignmentOptions.Left;
        LayoutElement nameLayout = nameGo.AddComponent<LayoutElement>();
        nameLayout.preferredWidth = 180f;
        nameLayout.minWidth = 180f;

        GameObject powerGo = new GameObject("Power", typeof(RectTransform));
        powerGo.transform.SetParent(row.transform, false);
        TMP_Text powerText = powerGo.AddComponent<TextMeshProUGUI>();
        powerText.text = team != null ? "POWER " + team.Overall : "";
        powerText.fontSize = 18f;
        powerText.color = Color.white;
        powerText.alignment = TextAlignmentOptions.Left;
        LayoutElement powerLayout = powerGo.AddComponent<LayoutElement>();
        powerLayout.preferredWidth = 100f;
        powerLayout.minWidth = 100f;

        GameObject statusGo = new GameObject("Status", typeof(RectTransform));
        statusGo.transform.SetParent(row.transform, false);
        TMP_Text statusText = statusGo.AddComponent<TextMeshProUGUI>();
        statusText.text = status;
        statusText.fontSize = 16f;
        statusText.alignment = TextAlignmentOptions.Right;
        statusText.color = status == "CHAMPION" ? ChampionColor : status == "STILL IN" ? StillInColor : EliminatedColor;
        statusText.enableAutoSizing = true;
        statusText.fontSizeMin = 11f;
        statusText.fontSizeMax = 16f;
        LayoutElement statusLayout = statusGo.AddComponent<LayoutElement>();
        statusLayout.flexibleWidth = 1f;
        statusLayout.minWidth = 100f;
    }
}
