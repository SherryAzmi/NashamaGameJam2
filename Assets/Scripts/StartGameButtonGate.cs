using UnityEngine;

// Attach to the "Start The Game" button in FormationScene. The button is
// only meant to be usable when the player reached FormationScene via
// CampaignState.LaunchFixture (i.e. from CampaignScene); any other entry
// path (squad selection, training, etc.) hides it.
public class StartGameButtonGate : MonoBehaviour
{
    private void Start()
    {
        gameObject.SetActive(CampaignState.ConsumeEnteredFromCampaign());
    }
}
