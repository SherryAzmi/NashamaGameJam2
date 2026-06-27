using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Lives in CampaignScene, as a panel sibling to the normal fixture-list UI.
// Renders a real mirrored bracket tree (Round of 16 on the outer edges,
// Quarterfinal/Semifinal converging inward on each side, Final centered)
// with drawn connector lines, similar to a classic tournament bracket
// poster. Pops up automatically right after the initial draw and after
// every round completes (CampaignState.BracketRecapPending), and can also
// be reopened any time via a permanent "View Bracket" button in the hub.
public class WorldCupDrawController : MonoBehaviour
{
    [Header("Header")]
    public TMP_Text headerText;

    [Tooltip("Fixed-size RectTransform (top-left pivot) the bracket tree is drawn into - wrap in a horizontal ScrollRect if it should be pannable.")]
    public RectTransform treeContainer;

    [Tooltip("The ScrollRect wrapping treeContainer - used to auto-scroll to Jordan's current match and to show a scrollbar so players know there's more to either side.")]
    public ScrollRect scrollRect;

    public Button continueButton;

    private const float BoxWidth = 180f;
    private const float BoxHeight = 50f;
    private const float VerticalGap = 14f;
    private const float ColumnSpacing = 220f;
    private const float LineThickness = 3f;
    private const float TreeWidth = 1500f;

    private static readonly Color JordanBoxColor = new Color(0.85f, 0.65f, 0.15f, 1f);
    private static readonly Color NormalBoxColor = new Color(0.15f, 0.15f, 0.18f, 0.9f);
    private static readonly Color LineColor = new Color(1f, 1f, 1f, 0.6f);

    // X position (left edge) of whichever box drawn last had isJordanMatch
    // true - used to auto-scroll there right after the tree is built, since
    // Jordan's match can land on either side of the mirrored tree.
    private float jordanBoxX = -1f;

    public void Show(CampaignState state, CampaignHubController hub)
    {
        BracketRound currentRound = state.GetCurrentRound();
        headerText.text = "WORLD CUP" + (currentRound != null ? " - " + currentRound.roundName.ToUpperInvariant() : "");

        for (int i = treeContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(treeContainer.GetChild(i).gameObject);
        }

        jordanBoxX = -1f;
        BuildBracketTree(state.Bracket);
        ScrollToJordan();

        continueButton.onClick.RemoveAllListeners();
        continueButton.onClick.AddListener(() =>
        {
            if (state.Stage == CampaignStage.WorldCupDrawPending)
            {
                state.MarkDrawRevealShown();
            }
            else
            {
                state.MarkBracketRecapShown();
            }

            gameObject.SetActive(false);
            hub.Render();
        });
    }

    // Draws every round currently in the bracket as a mirrored tree: round 0
    // (Round of 16) sits on the outer left/right edges, each later round
    // moves inward, and the Final (once it exists) sits centered in the
    // gap left between the last left/right columns.
    private void BuildBracketTree(List<BracketRound> bracket)
    {
        if (bracket.Count == 0)
        {
            return;
        }

        int firstRoundCount = bracket[0].matches.Count;
        int sideCount = firstRoundCount / 2;

        List<float> leftY = new List<float>();

        for (int i = 0; i < sideCount; i++)
        {
            leftY.Add(i * (BoxHeight + VerticalGap) + BoxHeight / 2f);
        }

        List<float> rightY = new List<float>(leftY);

        // Holds the previous round's box Y-positions (twice the length of
        // this round's), used only to draw connectors from those children
        // into this round's boxes. Null for round 0, which has nothing
        // earlier to connect from.
        List<float> prevLeftY = null;
        List<float> prevRightY = null;

        for (int r = 0; r < bracket.Count; r++)
        {
            List<BracketMatch> matches = bracket[r].matches;

            if (matches.Count == 1)
            {
                // Final - centered in the gap left by the last side round.
                // leftY[0]/rightY[0] here are the two semifinal winners'
                // box positions, carried forward unchanged from the SF round.
                float leftEdge = LeftX(r - 1) + BoxWidth;
                float rightEdge = RightX(r - 1);
                float centerX = (leftEdge + rightEdge) / 2f - BoxWidth / 2f;
                float centerYValue = (leftY[0] + rightY[0]) / 2f;

                DrawLine(leftEdge, leftY[0], centerX, centerYValue);
                DrawLine(rightEdge, rightY[0], centerX + BoxWidth, centerYValue);

                BuildMatchBox(matches[0], centerX, centerYValue);
                break;
            }

            int halfCount = matches.Count / 2;
            float leftX = LeftX(r);
            float rightX = RightX(r);

            for (int j = 0; j < halfCount; j++)
            {
                BuildMatchBox(matches[j], leftX, leftY[j]);
                BuildMatchBox(matches[halfCount + j], rightX, rightY[j]);

                if (r > 0)
                {
                    float prevLeftExitX = LeftX(r - 1) + BoxWidth;
                    float prevRightExitX = RightX(r - 1);

                    DrawConnector(prevLeftExitX, prevLeftY[2 * j], prevLeftY[2 * j + 1], leftX, leftY[j]);
                    DrawConnector(prevRightExitX, prevRightY[2 * j], prevRightY[2 * j + 1], rightX + BoxWidth, rightY[j]);
                }
            }

            // Next round's per-side Y positions: pair up this round's boxes,
            // except when this round is already down to one box per side
            // (the semifinal) - that single value just carries forward
            // unchanged, since the Final pairs across sides, not within one.
            List<float> nextLeftY = new List<float>();
            List<float> nextRightY = new List<float>();

            if (halfCount == 1)
            {
                nextLeftY.Add(leftY[0]);
                nextRightY.Add(rightY[0]);
            }
            else
            {
                for (int j = 0; j < halfCount; j += 2)
                {
                    nextLeftY.Add((leftY[j] + leftY[j + 1]) / 2f);
                    nextRightY.Add((rightY[j] + rightY[j + 1]) / 2f);
                }
            }

            prevLeftY = leftY;
            prevRightY = rightY;
            leftY = nextLeftY;
            rightY = nextRightY;
        }
    }

    private static float LeftX(int round)
    {
        return round * ColumnSpacing;
    }

    private static float RightX(int round)
    {
        return 1500f - round * ColumnSpacing - BoxWidth;
    }

    // Draws the elbow connector joining two child boxes (exiting at
    // childExitX, at childY1/childY2) into one parent box (entering at
    // parentEntryX, at parentY - the midpoint of the two child Ys). Pass
    // each box's edge that actually faces the other (e.g. a left-side
    // child's right edge into a left-side parent's left edge; a right-side
    // child's left edge into a right-side parent's right edge).
    private void DrawConnector(float childExitX, float childY1, float childY2, float parentEntryX, float parentY)
    {
        float elbowX = (childExitX + parentEntryX) / 2f;

        DrawLine(childExitX, childY1, elbowX, childY1);
        DrawLine(childExitX, childY2, elbowX, childY2);
        DrawLine(elbowX, childY1, elbowX, childY2);
        DrawLine(elbowX, parentY, parentEntryX, parentY);
    }

    private void DrawLine(float x1, float y1, float x2, float y2)
    {
        GameObject lineGo = new GameObject("Line", typeof(RectTransform));
        lineGo.transform.SetParent(treeContainer, false);

        RectTransform rt = lineGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 0.5f);

        Vector2 a = new Vector2(x1, -y1);
        Vector2 b = new Vector2(x2, -y2);
        float length = Vector2.Distance(a, b);
        float angle = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;

        rt.sizeDelta = new Vector2(length, LineThickness);
        rt.anchoredPosition = a;
        rt.localRotation = Quaternion.Euler(0f, 0f, angle);

        Image image = lineGo.AddComponent<Image>();
        image.color = LineColor;
    }

    // Auto-pans the horizontal scroll view to center on Jordan's current
    // match box, since it can land on either half of the mirrored tree -
    // without this the player has to manually scroll to find their match.
    private void ScrollToJordan()
    {
        if (scrollRect == null || jordanBoxX < 0f)
        {
            return;
        }

        float viewportWidth = scrollRect.viewport != null ? scrollRect.viewport.rect.width : 0f;
        float scrollableWidth = TreeWidth - viewportWidth;

        if (scrollableWidth <= 0f)
        {
            return;
        }

        float targetX = Mathf.Clamp(jordanBoxX + BoxWidth / 2f - viewportWidth / 2f, 0f, scrollableWidth);
        scrollRect.horizontalNormalizedPosition = targetX / scrollableWidth;
    }

    private void BuildMatchBox(BracketMatch match, float x, float centerY)
    {
        GameObject box = new GameObject("MatchBox", typeof(RectTransform));
        box.transform.SetParent(treeContainer, false);

        RectTransform rt = box.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.sizeDelta = new Vector2(BoxWidth, BoxHeight);
        rt.anchoredPosition = new Vector2(x, -centerY);

        Image background = box.AddComponent<Image>();
        background.color = match.isJordanMatch ? JordanBoxColor : NormalBoxColor;

        if (match.isJordanMatch)
        {
            jordanBoxX = x;
        }

        VerticalLayoutGroup boxGroup = box.AddComponent<VerticalLayoutGroup>();
        boxGroup.padding = new RectOffset(8, 8, 4, 4);
        boxGroup.spacing = 2f;
        boxGroup.childForceExpandWidth = true;
        boxGroup.childForceExpandHeight = false;

        BuildSideRow(box.transform, match.teamA, match.played ? match.scoreA : -1);
        BuildSideRow(box.transform, match.teamB, match.played ? match.scoreB : -1);

        if (match.hasPenalties)
        {
            GameObject penGo = new GameObject("PenaltyLabel", typeof(RectTransform));
            penGo.transform.SetParent(box.transform, false);
            TMP_Text penText = penGo.AddComponent<TextMeshProUGUI>();
            penText.text = "PEN " + match.penaltyScoreA + "-" + match.penaltyScoreB;
            penText.fontSize = 11f;
            penText.alignment = TextAlignmentOptions.Center;
            penText.color = Color.yellow;
        }
    }

    private void BuildSideRow(Transform parent, NationalTeamData team, int score)
    {
        GameObject row = new GameObject("Side", typeof(RectTransform));
        row.transform.SetParent(parent, false);

        HorizontalLayoutGroup rowGroup = row.AddComponent<HorizontalLayoutGroup>();
        rowGroup.spacing = 6f;
        rowGroup.childForceExpandWidth = false;
        rowGroup.childAlignment = TextAnchor.MiddleLeft;

        LayoutElement rowLayout = row.AddComponent<LayoutElement>();
        rowLayout.preferredHeight = 20f;

        GameObject flagGo = new GameObject("Flag", typeof(RectTransform));
        flagGo.transform.SetParent(row.transform, false);
        Image flagImage = flagGo.AddComponent<Image>();
        flagImage.preserveAspect = true;
        Sprite flag = team != null ? team.flag : null;
        flagImage.sprite = flag;
        flagImage.enabled = flag != null;
        LayoutElement flagLayout = flagGo.AddComponent<LayoutElement>();
        flagLayout.preferredWidth = 16f;
        flagLayout.preferredHeight = 16f;

        GameObject nameGo = new GameObject("Name", typeof(RectTransform));
        nameGo.transform.SetParent(row.transform, false);
        TMP_Text nameText = nameGo.AddComponent<TextMeshProUGUI>();
        nameText.text = team != null
            ? (string.IsNullOrWhiteSpace(team.teamName) ? team.name.ToUpperInvariant() : team.teamName.ToUpperInvariant())
            : "?";
        nameText.fontSize = 12f;
        nameText.color = Color.white;
        nameText.alignment = TextAlignmentOptions.Left;
        nameText.enableAutoSizing = true;
        nameText.fontSizeMin = 8f;
        nameText.fontSizeMax = 12f;
        LayoutElement nameLayout = nameGo.AddComponent<LayoutElement>();
        nameLayout.flexibleWidth = 1f;

        if (score >= 0)
        {
            GameObject scoreGo = new GameObject("Score", typeof(RectTransform));
            scoreGo.transform.SetParent(row.transform, false);
            TMP_Text scoreText = scoreGo.AddComponent<TextMeshProUGUI>();
            scoreText.text = score.ToString();
            scoreText.fontSize = 12f;
            scoreText.color = Color.white;
            scoreText.alignment = TextAlignmentOptions.Right;
            LayoutElement scoreLayout = scoreGo.AddComponent<LayoutElement>();
            scoreLayout.preferredWidth = 16f;
        }
    }
}
