using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Club-select screen for the squad call-up scene. Shows one button per
// Jordanian club; tapping one filters the existing player grid down to
// just that club's players. Players whose club isn't one of the six
// Jordanian league clubs (abroad professionals) are grouped into a single
// "National" bucket. Reuses TeamManager's existing card grid and selection
// logic as-is - this only shows/hides cards, it never changes selection.
public class ClubFilterController : MonoBehaviour
{
    private static readonly string[] JordanianClubs =
    {
        "Al Wehdat",
        "Al Faisaly",
        "Al Salt",
        "Al Jazeera",
        "Al Ramtha",
        "Al Hussein Irbid"
    };

    public const string NationalBucketName = "National";

    [Header("Club select")]
    public GameObject clubSelectPanel;
    public Button[] clubButtons;
    public string[] clubNames;

    [Header("Player grid")]
    public GameObject playerGridRoot;
    public Button backButton;

    [Header("Systems")]
    public TeamManager teamManager;

    private void Start()
    {
        for (int i = 0; i < clubButtons.Length; i++)
        {
            string clubName = clubNames[i];

            clubButtons[i].onClick.RemoveAllListeners();
            clubButtons[i].onClick.AddListener(() => SelectClub(clubName));
        }

        backButton.onClick.RemoveAllListeners();
        backButton.onClick.AddListener(ShowClubSelect);

        ShowClubSelect();
    }

    private void ShowClubSelect()
    {
        clubSelectPanel.SetActive(true);
        playerGridRoot.SetActive(false);
        backButton.gameObject.SetActive(false);
    }

    private void SelectClub(string clubName)
    {
        clubSelectPanel.SetActive(false);
        playerGridRoot.SetActive(true);
        backButton.gameObject.SetActive(true);

        List<PlayerData> players = teamManager.database.players;
        Transform content = teamManager.content;

        for (int i = 0; i < players.Count && i < content.childCount; i++)
        {
            bool matches = clubName == NationalBucketName
                ? !IsJordanianClub(players[i].club)
                : players[i].club == clubName;

            content.GetChild(i).gameObject.SetActive(matches);
        }
    }

    private bool IsJordanianClub(string club)
    {
        foreach (string jordanianClub in JordanianClubs)
        {
            if (jordanianClub == club)
            {
                return true;
            }
        }

        return false;
    }
}
