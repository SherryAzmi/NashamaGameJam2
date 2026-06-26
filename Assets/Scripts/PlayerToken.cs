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
    private FormationFieldManager formationManager;

    public PlayerData Player { get; private set; }
    public string SlotName { get; private set; }

    private void Awake()
    {
        tokenRenderer = GetComponent<SpriteRenderer>();

        tmpLabel = GetComponentInChildren<TMP_Text>();
        textMeshLabel = GetComponentInChildren<TextMesh>();

        normalColor = tokenRenderer.color;

        tokenRenderer.sortingOrder = 10;

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
        FormationFieldManager manager
    )
    {
        Player = playerData;
        SlotName = slotName;
        formationManager = manager;

        if (Player == null)
        {
            SetLabel($"{slotName}\nEMPTY");
            return;
        }

        SetLabel(
            $"{Player.playerName}\n" +
            $"{Player.position} • OVR {GetOverall(Player)}"
        );
    }

    private void OnMouseDown()
    {
        if (formationManager != null)
        {
            formationManager.SelectStarter(this);
        }
    }

    public void SetSelected(bool selected)
    {
        tokenRenderer.color = selected
            ? new Color(1f, 0.75f, 0.15f)
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

    private int GetOverall(PlayerData playerData)
    {
        return
            (playerData.speed +
             playerData.shoot +
             playerData.defense) / 3;
    }
}