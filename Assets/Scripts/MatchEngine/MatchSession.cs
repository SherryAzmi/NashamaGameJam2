using UnityEngine;

// Carries a built MatchSetup across the scene load from FormationScene to
// MatchDayScene. Same persistence pattern as TeamManager (DontDestroyOnLoad
// singleton) so data survives the scene transition.
public class MatchSession : MonoBehaviour
{
    private static MatchSession instance;

    public MatchSetup PendingSetup { get; private set; }

    // Set by the campaign hub before loading FormationScene, so MatchLauncher
    // knows which national team this fixture is against.
    public NationalTeamData PendingOpponentTeam { get; private set; }

    // Set by the campaign hub when launching the Final - tells MatchDayController
    // to enable the penalty shootout if the match is drawn after full time.
    public bool PendingIsKnockout { get; private set; }

    // True while FormationScene is loaded additively on top of MatchDayScene
    // during a half-time formation edit. FormationScene's launcher checks
    // this to show "Continue 2nd Half" instead of "Play Match".
    public bool IsHalftimeEditing { get; private set; }

    public static MatchSession GetOrCreate()
    {
        if (instance != null)
        {
            return instance;
        }

        GameObject sessionObject = new GameObject("MatchSession");
        instance = sessionObject.AddComponent<MatchSession>();
        DontDestroyOnLoad(sessionObject);

        return instance;
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

    public void SetPendingSetup(MatchSetup setup)
    {
        PendingSetup = setup;
    }

    public MatchSetup ConsumePendingSetup()
    {
        MatchSetup setup = PendingSetup;
        PendingSetup = null;
        return setup;
    }

    public void SetPendingOpponentTeam(NationalTeamData team)
    {
        PendingOpponentTeam = team;
    }

    public NationalTeamData ConsumePendingOpponentTeam()
    {
        NationalTeamData team = PendingOpponentTeam;
        PendingOpponentTeam = null;
        return team;
    }

    public void SetPendingIsKnockout(bool isKnockout)
    {
        PendingIsKnockout = isKnockout;
    }

    public bool ConsumePendingIsKnockout()
    {
        bool value = PendingIsKnockout;
        PendingIsKnockout = false;
        return value;
    }

    public void SetHalftimeEditing(bool editing)
    {
        IsHalftimeEditing = editing;
    }
}
