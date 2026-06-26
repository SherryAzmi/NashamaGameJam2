using TMPro;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class PlayerToken : MonoBehaviour
{
    private SpriteRenderer tokenRenderer;
    private TMP_Text tmpLabel;
    private TextMesh textMeshLabel;

    private Color normalColor;
    private bool isBusy;

    public PlayerData Player { get; private set; }
    public string SlotName { get; private set; }

    private void Awake()
    {
        tokenRenderer = GetComponent<SpriteRenderer>();
        tmpLabel = GetComponentInChildren<TMP_Text>();
        textMeshLabel = GetComponentInChildren<TextMesh>();

        normalColor = tokenRenderer != null
            ? tokenRenderer.color
            : Color.white;

        if (tokenRenderer != null)
        {
            tokenRenderer.sortingOrder = 10;
        }

        if (tmpLabel != null)
        {
            Renderer labelRenderer = tmpLabel.GetComponent<Renderer>();

            if (labelRenderer != null)
            {
                labelRenderer.sortingOrder = 11;
            }
        }

        if (textMeshLabel != null)
        {
            MeshRenderer labelRenderer =
                textMeshLabel.GetComponent<MeshRenderer>();

            if (labelRenderer != null)
            {
                labelRenderer.sortingOrder = 11;
            }

            textMeshLabel.anchor = TextAnchor.MiddleCenter;
            textMeshLabel.alignment = TextAlignment.Center;
        }
    }

    public void Setup(
        PlayerData playerData,
        string slotName,
        FormationFieldManager unusedManager
    )
    {
        Setup(playerData, slotName);
    }

    public void Setup(PlayerData playerData, string slotName)
    {
        Player = playerData;
        SlotName = slotName;
        isBusy = false;

        RefreshDisplay();
    }

    // Called after a training job ends, so the displayed OVR updates immediately.
    public void RefreshDisplay()
    {
        if (Player == null)
        {
            SetLabel(SlotName + "\nEMPTY");
            ApplyColor();
            return;
        }

        SetLabel(
            SlotName +
            "\n" +
            Player.playerName +
            "\nOVR " +
            GetOverall(Player)
        );

        ApplyColor();
    }

    public void SetSelected(bool selected)
    {
        if (tokenRenderer == null)
        {
            return;
        }

        // A player in individual training stays grey by default,
        // but can still be selected in FormationScene and replaced
        // with an available bench player before a match.
        tokenRenderer.color = selected
            ? new Color(1f, 0.75f, 0.15f)
            : isBusy
                ? new Color(0.5f, 0.5f, 0.5f)
                : normalColor;
    }

    public void SetBusy(bool busy)
    {
        isBusy = busy;

        // TrainingFieldManager calls SetBusy when time progresses.
        // RefreshDisplay here makes the new permanent OVR appear right away.
        RefreshDisplay();
    }

    private void ApplyColor()
    {
        if (tokenRenderer == null)
        {
            return;
        }

        tokenRenderer.color = isBusy
            ? new Color(0.5f, 0.5f, 0.5f)
            : normalColor;
    }

    private void SetLabel(string value)
    {
        if (tmpLabel != null)
        {
            tmpLabel.text = value;
        }

        if (textMeshLabel != null)
        {
            textMeshLabel.text = value;
        }
    }

    private int GetOverall(PlayerData player)
    {
        return (
            player.speed +
            player.shoot +
            player.defense
        ) / 3;
    }
}
