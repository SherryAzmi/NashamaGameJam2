using UnityEngine;
using UnityEngine.UI;

// Attach to the confirm button inside the "are you sure?" restart popup.
// Wires itself in code, same reasoning as PanelToggleButton - avoids
// hand-configuring Unity's persistent UnityEvent call list for this.
public class RestartCareerButton : MonoBehaviour
{
    private void Start()
    {
        Button button = GetComponent<Button>();

        if (button != null)
        {
            button.onClick.AddListener(() => CampaignRestartService.RestartWholeCampaign());
        }
    }
}
