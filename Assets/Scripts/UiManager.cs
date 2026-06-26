using UnityEngine;
using UnityEngine.SceneManagement;

public class UiManager : MonoBehaviour
{
    public GameObject SettingsPanel;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        SettingsPanel.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void GoToScene(string sceneName)
    {
           SceneManager.LoadScene(sceneName);
    }

    public void OpenSettings()
    {
SettingsPanel.SetActive(true);
    }

    public void CloseSettings()
    {
SettingsPanel.SetActive(false);

    
    }

    public void OpenMusic()
    {

    }

    public void CloseMusic()
    {


    
    }
}
