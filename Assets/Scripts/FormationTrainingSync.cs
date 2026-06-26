using System.Collections.Generic;
using UnityEngine;

// Attach this to the same GameObject that has FormationFieldManager
// in FormationScene.
//
// It makes players who are in INDIVIDUAL training appear grey in
// FormationScene, while still allowing them to be selected and swapped
// with an available bench player.
[DisallowMultipleComponent]
public class FormationTrainingSync : MonoBehaviour
{
    private TrainingManager trainingManager;

    private readonly Dictionary<PlayerToken, bool> lastBusyState =
        new Dictionary<PlayerToken, bool>();

    private void LateUpdate()
    {
        if (trainingManager == null)
        {
            trainingManager = FindFirstObjectByType<TrainingManager>();
        }

        PlayerToken[] tokens = FindObjectsByType<PlayerToken>(
            FindObjectsSortMode.None
        );

        for (int i = tokens.Length - 1; i >= 0; i--)
        {
            PlayerToken token = tokens[i];

            if (token == null)
            {
                continue;
            }

            bool shouldBeBusy =
                trainingManager != null &&
                token.Player != null &&
                trainingManager.IsIndividualTrainingActive(token.Player);

            bool alreadyTracked;
            bool previousBusy = lastBusyState.TryGetValue(
                token,
                out alreadyTracked
            ) && alreadyTracked;

            if (!lastBusyState.ContainsKey(token) ||
                previousBusy != shouldBeBusy)
            {
                token.SetBusy(shouldBeBusy);
                lastBusyState[token] = shouldBeBusy;
            }
        }

        RemoveDestroyedTokens(tokens);
    }

    private void RemoveDestroyedTokens(PlayerToken[] currentTokens)
    {
        HashSet<PlayerToken> liveTokens =
            new HashSet<PlayerToken>(currentTokens);

        List<PlayerToken> removed =
            new List<PlayerToken>();

        foreach (PlayerToken token in lastBusyState.Keys)
        {
            if (token == null || !liveTokens.Contains(token))
            {
                removed.Add(token);
            }
        }

        foreach (PlayerToken token in removed)
        {
            lastBusyState.Remove(token);
        }
    }
}
