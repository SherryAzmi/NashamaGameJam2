using UnityEngine;

[DisallowMultipleComponent]
public class GameProgressManager : MonoBehaviour
{
    public static GameProgressManager Instance { get; private set; }

    private const string CoachNameKey = "NASHAMA_COACH_NAME";

    // Coach name can safely stay after the app closes.
    public string CoachName { get; private set; }

    // These are SESSION values. They survive scene changes, but reset when a
    // brand-new Play session starts. A full disk save for squad/training comes
    // later once PlayerData has stable player IDs.
    public bool HasSelectedTeam { get; private set; }
    public string CurrentFormation { get; private set; } = "4-3-3";

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
