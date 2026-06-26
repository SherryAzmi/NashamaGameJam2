using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BenchCardUI : MonoBehaviour
{
    private Button button;
    private TMP_Text label;

    private PlayerData player;
    private FormationFieldManager formationManager;

    private void Awake()
    {
        button = GetComponent<Button>();
        label = GetComponentInChildren<TMP_Text>();
    }

    public void Setup(
        PlayerData playerData,
        FormationFieldManager manager
    )
    {
        player = playerData;
        formationManager = manager;

        if (label != null)
        {
            label.text =
                player.playerName +
                "\n" +
                player.position +
                " | OVR " +
                GetOverall(player);
        }

        button.onClick.RemoveAllListeners();

        button.onClick.AddListener(() =>
        {
            formationManager.SwapWithBench(player);
        });
    }

    private int GetOverall(PlayerData playerData)
    {
        return
            (playerData.speed +
             playerData.shoot +
             playerData.defense) / 3;
    }
}