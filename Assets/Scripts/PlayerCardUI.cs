using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerCardUI : MonoBehaviour
{
    public TMP_Text infoText;
    public Button selectButton;
    public Image background;

    private PlayerData player;
    private TeamManager teamManager;

    public void Setup(PlayerData playerData, TeamManager manager)
    {
        player = playerData;
        teamManager = manager;

        int overall = (player.speed + player.shoot + player.defense) / 3;

        infoText.text =
            player.playerName + "\n" +
            player.club + "\n" +
            player.position + " | OVR " + overall;

        selectButton.onClick.RemoveAllListeners();
        selectButton.onClick.AddListener(() =>
        {
            teamManager.TogglePlayer(player, this);
        });

        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        background.color = selected
            ? new Color(0.45f, 0.85f, 0.45f)
            : Color.white;
    }
    public void SetLocked(bool locked)
{
    selectButton.interactable = !locked;
}
}