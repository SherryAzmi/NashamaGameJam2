using UnityEngine;
using UnityEngine.SceneManagement;

// Used when the player has lost the same match twice (their one retry per
// match was already spent): wipes the save file and every persisted
// DontDestroyOnLoad singleton so the next scene load starts a completely
// fresh campaign, then sends the player back to the hub to pick a new squad.
public static class CampaignRestartService
{
    public static void RestartWholeCampaign(string homeSceneName = "HomeScene")
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

        SceneManager.LoadScene(homeSceneName);
    }

    private static void DestroyIfExists(GameObject target)
    {
        if (target != null)
        {
            Object.Destroy(target);
        }
    }
}
