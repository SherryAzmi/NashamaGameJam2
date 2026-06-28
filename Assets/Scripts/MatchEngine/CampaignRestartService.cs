using UnityEngine;
using UnityEngine.SceneManagement;

// Used when the player has lost the same match twice (their one retry per
// match was already spent) or hits "New Career" from Settings: wipes the
// save file and every persisted DontDestroyOnLoad singleton, then sends the
// player all the way back to IntroScene - same as a fresh install, including
// re-entering their coach name (pre-filled with the old one, but editable).
public static class CampaignRestartService
{
    public static void RestartWholeCampaign(string targetSceneName = "IntroScene")
    {
        SaveManager.Instance?.DeleteSave();

        DestroyIfExists(TeamManager.Instance != null ? TeamManager.Instance.gameObject : null);
        DestroyIfExists(TrainingManager.Instance != null ? TrainingManager.Instance.gameObject : null);
        DestroyIfExists(CampaignState.Instance != null ? CampaignState.Instance.gameObject : null);
        DestroyIfExists(MatchSession.GetOrCreate().gameObject);

        // GameProgressManager is recreated only once per process
        // (RuntimeInitializeOnLoadMethod runs before the first scene, not
        // every scene), so it is reset in place instead of destroyed.
        if (GameProgressManager.Instance != null)
        {
            GameProgressManager.Instance.ResetTeamProgress();
        }

        SceneManager.LoadScene(targetSceneName);
    }

    private static void DestroyIfExists(GameObject target)
    {
        if (target != null)
        {
            Object.Destroy(target);
        }
    }
}
