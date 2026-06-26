using UnityEngine;

// Carries a built MatchSetup across the scene load from FormationScene to
// MatchDayScene. Same persistence pattern as TeamManager (DontDestroyOnLoad
// singleton) so data survives the scene transition.
public class MatchSession : MonoBehaviour
{
    private static MatchSession instance;

    public MatchSetup PendingSetup { get; private set; }

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
}
