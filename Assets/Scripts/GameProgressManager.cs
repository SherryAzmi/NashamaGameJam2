using UnityEngine;

[DisallowMultipleComponent]
public class GameProgressManager : MonoBehaviour
{
    public static GameProgressManager Instance { get; private set; }

    private const string CoachNameKey = "NASHAMA_COACH_NAME";

    // Coach name can safely stay after the app closes.
    public string CoachName { get; private set; }

    // HasSelectedTeam is restored from disk only via ApplyLoadedSquadFlag,
    // called by TeamManager right after it finishes restoring the actual
    // squad lists - never set directly from a save file in Awake() here,
    // so the flag and the squad it depends on always change together.
    public bool HasSelectedTeam { get; private set; }
    public string CurrentFormation { get; private set; } = "4-3-3";

    // Ensures GameProgressManager exists from the very first scene loaded,
    // regardless of whether that is IntroScene or any other scene (e.g.
    // testing a later scene directly in the Editor). Without this, any
    // scene entered before IntroScene would see Instance == null forever,
    // which silently breaks HomeSceneButton's lock checks (they default to
    // "team not selected" when Instance is null).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null)
        {
            return;
        }

        GameObject managerObject = new GameObject("GameProgressManager");
        managerObject.AddComponent<GameProgressManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        CoachName = PlayerPrefs.GetString(CoachNameKey, "");

        // Never load only a "team selected" flag from disk.
        // The actual 26-player squad is a runtime object, so both must start
        // together in a new session.
        HasSelectedTeam = false;
        CurrentFormation = "4-3-3";
    }

    public void SetCoachName(string coachName)
    {
        CoachName = string.IsNullOrWhiteSpace(coachName)
            ? "Coach"
            : coachName.Trim();

        PlayerPrefs.SetString(CoachNameKey, CoachName);
        PlayerPrefs.Save();
    }

    public void MarkTeamSelected()
    {
        HasSelectedTeam = true;
    }

    // Called only by TeamManager, immediately after it has restored the
    // actual squad lists from the save file - see the comment on
    // HasSelectedTeam above.
    public void ApplyLoadedSquadFlag(bool hasSelectedTeam)
    {
        HasSelectedTeam = hasSelectedTeam;
    }

    public void SetCurrentFormation(string formation)
    {
        if (string.IsNullOrWhiteSpace(formation))
        {
            return;
        }

        CurrentFormation = formation.Trim();
    }

[ContextMenu("DEBUG Reset Team Progress")]
    public void ResetTeamProgress()
    {
        HasSelectedTeam = false;
        CurrentFormation = "4-3-3";
    }
}
