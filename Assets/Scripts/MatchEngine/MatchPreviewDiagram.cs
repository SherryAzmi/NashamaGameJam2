using System.Collections.Generic;
using UnityEngine;

// Static (non-moving) mini pitch diagram for the pre-kickoff comparison
// screen. One instance per side.
public class MatchPreviewDiagram : MonoBehaviour
{
    public RectTransform diagramPanel;
    public Color dotColor = Color.white;
    public float dotSize = 18f;

    public void Render(List<PlayerData> startingEleven, bool mirrored)
    {
        for (int i = diagramPanel.childCount - 1; i >= 0; i--)
        {
            Destroy(diagramPanel.GetChild(i).gameObject);
        }

        List<Vector2> positions = MatchPitchLayout.GetPositions(startingEleven, mirrored);
        Vector2 pitchPixelSize = diagramPanel.rect.size;

        for (int i = 0; i < startingEleven.Count; i++)
        {
            GameObject dotObject = MatchPitchVisuals.CreateDot(diagramPanel, startingEleven[i].playerName, dotColor, dotSize, i + 1);
            RectTransform rect = dotObject.GetComponent<RectTransform>();
            rect.anchoredPosition = MatchPitchVisuals.PitchToAnchoredPosition(positions[i], pitchPixelSize);
        }
    }
}
