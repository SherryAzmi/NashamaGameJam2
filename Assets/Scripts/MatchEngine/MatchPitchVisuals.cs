using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Shared helper for building a numbered "dot" UI element at runtime, with
// no prefab/art dependency. Used by both the live pitch (MatchPitchController)
// and the static pre-match mini diagrams (MatchPreviewDiagram).
public static class MatchPitchVisuals
{
    public static GameObject CreateDot(Transform parent, string name, Color color, float size, int? number)
    {
        GameObject dotObject = new GameObject(name, typeof(RectTransform));
        dotObject.transform.SetParent(parent, false);

        RectTransform rect = dotObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(size, size);

        Image image = dotObject.AddComponent<Image>();
        image.color = color;

        if (number.HasValue)
        {
            GameObject labelObject = new GameObject("Number", typeof(RectTransform));
            labelObject.transform.SetParent(dotObject.transform, false);

            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
            label.text = number.Value.ToString();
            label.fontSize = size * 0.5f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;

            dotObject.AddComponent<MatchDotToken>().numberLabel = label;
        }

        return dotObject;
    }

    // Maps pitch-space coordinates (x in [-5,5], y in [-10,10]) onto a
    // RectTransform's anchored-position space sized pitchPixelSize.
    public static Vector2 PitchToAnchoredPosition(Vector2 pitchPosition, Vector2 pitchPixelSize)
    {
        float scaleX = pitchPixelSize.x / 10f;
        float scaleY = pitchPixelSize.y / 20f;

        return new Vector2(pitchPosition.x * scaleX, pitchPosition.y * scaleY);
    }
}
