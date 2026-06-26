using UnityEngine;

public class HomeMenuManager : MonoBehaviour
{
    public HomeSceneButton[] sceneButtons;

    private void Start()
    {
        RefreshAll();
    }

    private void OnEnable()
    {
        RefreshAll();
    }

    public void RefreshAll()
    {
        if (sceneButtons == null) return;

        foreach (HomeSceneButton item in sceneButtons)
        {
            if (item != null)
               item.RefreshLockState();
        }
    }
}