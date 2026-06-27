using UnityEngine;
using UnityEngine.UI;

// Tiny helper for a button whose only job is to show or hide one panel.
// Wires itself in code at Start() instead of needing Unity's persistent
// UnityEvent call list hand-configured in the Inspector for this one
// common case.
public class PanelToggleButton : MonoBehaviour
{
    public GameObject targetPanel;
    public bool activate;

    private void Start()
    {
        Button button = GetComponent<Button>();

        if (button != null && targetPanel != null)
        {
            button.onClick.AddListener(() => targetPanel.SetActive(activate));
        }
    }
}
