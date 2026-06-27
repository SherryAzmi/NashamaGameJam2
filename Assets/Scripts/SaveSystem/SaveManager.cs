using System;
using System.IO;
using UnityEngine;

// DontDestroyOnLoad singleton, same pattern as GameProgressManager/TeamManager.
// Performs exactly one disk read at app start (cached in PendingLoadData) so
// every other singleton restores from that single shared snapshot instead of
// each doing its own independent disk access - avoids the "flag without its
// data" race called out in GameProgressManager's existing code comments.
public class SaveManager : MonoBehaviour
{
    private static SaveManager instance;
    public static SaveManager Instance => instance;

    // Populated once at startup by the bootstrap method below. Never nulled
    // out after consumption - each singleton just reads from it in its own
    // Awake()/Start() whenever it needs to restore.
    public static GameSaveData PendingLoadData { get; private set; }

    private static string SavePath =>
        Path.Combine(Application.persistentDataPath, "savegame.json");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
        {
            return;
        }

        GameObject managerObject = new GameObject("SaveManager");
        instance = managerObject.AddComponent<SaveManager>();
        DontDestroyOnLoad(managerObject);

        PendingLoadData = instance.Load();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // Gathers current state from every persisted singleton and writes it.
    // Safe to call even if some singletons are not yet loaded for the
    // current scene - their section of the save just keeps its last value.
    public void SaveCurrentState()
    {
        GameSaveData data = PendingLoadData ?? new GameSaveData();

        if (TeamManager.Instance != null)
        {
            TeamManager.Instance.WriteSaveData(data.squad);
        }

        if (TrainingManager.Instance != null)
        {
            TrainingManager.Instance.WriteSaveData(data.training);
        }

        if (CampaignState.Instance != null)
        {
            CampaignState.Instance.WriteSaveData(data.campaign);
        }

        PendingLoadData = data;
        Save(data);
    }

    private void Save(GameSaveData data)
    {
        try
        {
            string json = JsonUtility.ToJson(data, true);
            string tempPath = SavePath + ".tmp";

            File.WriteAllText(tempPath, json);

            if (File.Exists(SavePath))
            {
                File.Delete(SavePath);
            }

            File.Move(tempPath, SavePath);
        }
        catch (Exception exception)
        {
            Debug.LogError("SaveManager: failed to save game - " + exception.Message);
        }
    }

    public bool HasSaveFile()
    {
        return File.Exists(SavePath);
    }

    public void DeleteSave()
    {
        try
        {
            if (File.Exists(SavePath))
            {
                File.Delete(SavePath);
            }
        }
        catch (Exception exception)
        {
            Debug.LogError("SaveManager: failed to delete save - " + exception.Message);
        }

        PendingLoadData = null;
    }

    private GameSaveData Load()
    {
        try
        {
            if (!File.Exists(SavePath))
            {
                return null;
            }

            string json = File.ReadAllText(SavePath);
            return JsonUtility.FromJson<GameSaveData>(json);
        }
        catch (Exception exception)
        {
            Debug.LogError("SaveManager: failed to load save - " + exception.Message);
            return null;
        }
    }
}
