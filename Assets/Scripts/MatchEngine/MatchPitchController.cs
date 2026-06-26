using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Drives the live moving dots (11 home + 11 away + ball) on the match
// pitch panel during playback. The ball is "attached" to one specific
// carrier on the possessing team each tick; nearby teammates shift toward
// them to offer support, and the nearest opponents press/close them down.
// Every dot still has its own movement profile (speed/wander/reaction) so
// nothing moves in lockstep, and a role-based push clamp keeps each player
// anchored to the zone their position is responsible for (goalkeepers stay
// on their own goal line, etc).
public class MatchPitchController : MonoBehaviour
{
    public RectTransform pitchPanel;
    public Color homeColor = new Color(0.2f, 0.4f, 1f);
    public Color awayColor = new Color(1f, 0.3f, 0.3f);
    public Color ballColor = Color.white;
    public float dotSize = 32f;
    public float ballSize = 16f;
    public float pushAmount = 2.5f;
    public float retreatAmount = 1f;
    public int supportingTeammateCount = 2;
    public int pressingOpponentCount = 2;
    public float supportStrength = 0.35f;
    public float pressStrength = 0.5f;

    [Header("Goal celebration")]
    public float shakeDuration = 0.35f;
    public float shakeStrength = 18f;

    private List<MatchDotToken> homeTokens = new List<MatchDotToken>();
    private List<MatchDotToken> awayTokens = new List<MatchDotToken>();
    private List<string> homeCategories = new List<string>();
    private List<string> awayCategories = new List<string>();
    private MatchDotToken ballToken;

    private List<Vector2> homeBasePositions = new List<Vector2>();
    private List<Vector2> awayBasePositions = new List<Vector2>();

    private Vector2 pitchPixelSize;

    private Vector2 pitchRestPosition;
    private Coroutine shakeRoutine;
    private MatchSetup currentSetup;

    // The real, live ball carrier - whoever is actually holding it on the
    // pitch right now, not a fresh random pick. MatchDecisiveMomentController
    // reads this so the decision panel always matches who has the ball.
    public MatchSide? CurrentPossessingSide { get; private set; }
    public PlayerData CurrentCarrier { get; private set; }

    private void OnEnable()
    {
        MatchEvents.OnPossessionTick += HandlePossessionTick;
        MatchEvents.OnGoal += HandleGoalShake;
    }

    private void OnDisable()
    {
        MatchEvents.OnPossessionTick -= HandlePossessionTick;
        MatchEvents.OnGoal -= HandleGoalShake;
    }

    public void Setup(MatchSetup setup)
    {
        ClearDots();

        currentSetup = setup;
        CurrentPossessingSide = null;
        CurrentCarrier = null;

        pitchPixelSize = pitchPanel.rect.size;
        pitchRestPosition = pitchPanel.anchoredPosition;

        homeBasePositions = MatchPitchLayout.GetPositions(setup.home.startingEleven, false);
        awayBasePositions = MatchPitchLayout.GetPositions(setup.away.startingEleven, true);

        SpawnTeam(setup.home.startingEleven, homeBasePositions, homeColor, homeTokens, homeCategories);
        SpawnTeam(setup.away.startingEleven, awayBasePositions, awayColor, awayTokens, awayCategories);

        SpawnGoal(new Vector2(0f, -9.5f));
        SpawnGoal(new Vector2(0f, 9.5f));

        GameObject ballObject = MatchPitchVisuals.CreateDot(pitchPanel, "Ball", ballColor, ballSize, null);
        ballToken = ballObject.AddComponent<MatchDotToken>();
        ballToken.SetIndividualProfile(5f, 4f, 0.9f, 1f, 9999f, 9999f);
        ballToken.SetPositionImmediate(Vector2.zero);
    }

    private void SpawnTeam(List<PlayerData> players, List<Vector2> basePositions, Color color, List<MatchDotToken> tokens, List<string> categories)
    {
        for (int i = 0; i < players.Count; i++)
        {
            string category = MatchPitchLayout.GetCategory(players[i].position);

            GameObject dotObject = MatchPitchVisuals.CreateDot(pitchPanel, players[i].playerName, color, dotSize, i + 1);
            MatchDotToken token = dotObject.GetComponent<MatchDotToken>();

            Vector2 anchored = MatchPitchVisuals.PitchToAnchoredPosition(basePositions[i], pitchPixelSize);
            token.SetPositionImmediate(anchored);

            float moveSpeed = Random.Range(2.5f, 4.5f);
            float wanderRadius = Random.Range(7f, 15f);
            float wanderSpeed = Random.Range(0.3f, 0.7f);
            float individualPush = Random.Range(0.8f, 1.2f);
            Vector2 wanderClamp = GetWanderClampPixels(category);

            token.SetIndividualProfile(moveSpeed, wanderRadius, wanderSpeed, individualPush, wanderClamp.x, wanderClamp.y);

            tokens.Add(token);
            categories.Add(category);
        }
    }

    private void SpawnGoal(Vector2 pitchPosition)
    {
        GameObject goalObject = new GameObject("Goal", typeof(RectTransform));
        goalObject.transform.SetParent(pitchPanel, false);

        RectTransform rect = goalObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(2.4f * (pitchPixelSize.x / 10f), 8f);
        rect.anchoredPosition = MatchPitchVisuals.PitchToAnchoredPosition(pitchPosition, pitchPixelSize);

        UnityEngine.UI.Image image = goalObject.AddComponent<UnityEngine.UI.Image>();
        image.color = Color.white;
    }

    private void HandleGoalShake(GoalEvent goalEvent)
    {
        if (shakeRoutine != null)
        {
            StopCoroutine(shakeRoutine);
        }

        shakeRoutine = StartCoroutine(ShakeRoutine());
    }

    private IEnumerator ShakeRoutine()
    {
        float elapsed = 0f;

        while (elapsed < shakeDuration)
        {
            float falloff = 1f - elapsed / shakeDuration;
            Vector2 offset = Random.insideUnitCircle * shakeStrength * falloff;
            pitchPanel.anchoredPosition = pitchRestPosition + offset;

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        pitchPanel.anchoredPosition = pitchRestPosition;
        shakeRoutine = null;
    }

    private void HandlePossessionTick(int minute, MatchSide possessingSide)
    {
        if (homeTokens.Count == 0)
        {
            return;
        }

        bool homeAttacking = possessingSide == MatchSide.Home;

        List<MatchDotToken> attackingTokens = homeAttacking ? homeTokens : awayTokens;
        List<Vector2> attackingBase = homeAttacking ? homeBasePositions : awayBasePositions;
        List<string> attackingCategories = homeAttacking ? homeCategories : awayCategories;

        List<MatchDotToken> defendingTokens = homeAttacking ? awayTokens : homeTokens;
        List<Vector2> defendingBase = homeAttacking ? awayBasePositions : homeBasePositions;
        List<string> defendingCategories = homeAttacking ? awayCategories : homeCategories;

        float attackPush = pushAmount;
        float defendPush = -retreatAmount;

        // Tactical base targets (the shape each team holds this minute),
        // before any ball-chasing adjustments.
        List<Vector2> attackingTargets = BuildTacticalTargets(attackingTokens, attackingBase, attackingCategories, attackPush);
        List<Vector2> defendingTargets = BuildTacticalTargets(defendingTokens, defendingBase, defendingCategories, defendPush);

        int carrierIndex = PickCarrierIndex(attackingCategories);
        Vector2 carrierPosition = attackingTargets[carrierIndex];

        CurrentPossessingSide = possessingSide;
        List<PlayerData> attackingPlayers = homeAttacking ? currentSetup.home.startingEleven : currentSetup.away.startingEleven;
        CurrentCarrier = attackingPlayers[carrierIndex];

        ApplySupportShift(attackingTargets, carrierIndex, carrierPosition);
        ApplyPressShift(defendingTargets, defendingCategories, carrierPosition);

        // Whole-team lateral shift: both sides lean across the pitch
        // toward whichever flank the ball is on, like a real defensive/
        // attacking block, not just the few players closest to the carrier.
        ApplyTeamLateralShift(attackingTargets, attackingCategories, carrierPosition.x);
        ApplyTeamLateralShift(defendingTargets, defendingCategories, carrierPosition.x);

        ApplySeparation(attackingTargets, defendingTargets);

        PushTargets(attackingTokens, attackingTargets);
        PushTargets(defendingTokens, defendingTargets);

        float forwardNudge = homeAttacking ? 0.4f : -0.4f;
        Vector2 ballPitchPosition = carrierPosition + new Vector2(0f, forwardNudge);
        ballToken.SetTargetPosition(MatchPitchVisuals.PitchToAnchoredPosition(ballPitchPosition, pitchPixelSize));
    }

    private List<Vector2> BuildTacticalTargets(List<MatchDotToken> tokens, List<Vector2> basePositions, List<string> categories, float yOffset)
    {
        List<Vector2> targets = new List<Vector2>(tokens.Count);

        for (int i = 0; i < tokens.Count; i++)
        {
            float maxRolePush = GetCategoryMaxPush(categories[i]);
            float individualPush = Mathf.Clamp(yOffset * tokens[i].pushMultiplier, -maxRolePush, maxRolePush);

            targets.Add(basePositions[i] + new Vector2(0f, individualPush));
        }

        return targets;
    }

    // Pulls the closest teammates a little toward the ball carrier, as if
    // offering a passing option / overlapping run.
    private void ApplySupportShift(List<Vector2> attackingTargets, int carrierIndex, Vector2 carrierPosition)
    {
        List<int> nearest = FindNearestIndices(attackingTargets, carrierPosition, carrierIndex, supportingTeammateCount);

        foreach (int index in nearest)
        {
            attackingTargets[index] = Vector2.Lerp(attackingTargets[index], carrierPosition, supportStrength);
        }
    }

    // Pulls the closest opponents toward the ball carrier to press/close
    // them down, while the rest of the defense holds its shape.
    private void ApplyPressShift(List<Vector2> defendingTargets, List<string> defendingCategories, Vector2 carrierPosition)
    {
        List<int> nearest = FindNearestIndices(defendingTargets, carrierPosition, -1, pressingOpponentCount);

        foreach (int index in nearest)
        {
            if (defendingCategories[index] == "GK")
            {
                continue;
            }

            defendingTargets[index] = Vector2.Lerp(defendingTargets[index], carrierPosition, pressStrength);
        }
    }

    private void ApplyTeamLateralShift(List<Vector2> targets, List<string> categories, float ballX)
    {
        float shift = Mathf.Clamp(ballX * 0.15f, -1f, 1f);

        for (int i = 0; i < targets.Count; i++)
        {
            if (categories[i] == "GK")
            {
                continue;
            }

            targets[i] = targets[i] + new Vector2(shift, 0f);
        }
    }

    // Nudges any two players (teammate or opponent) apart if they've
    // drifted closer than minSeparation, so dots never sit on top of each
    // other.
    private void ApplySeparation(List<Vector2> attackingTargets, List<Vector2> defendingTargets)
    {
        const float minSeparation = 0.7f;
        const float strength = 0.5f;

        List<Vector2> combined = new List<Vector2>(attackingTargets.Count + defendingTargets.Count);
        combined.AddRange(attackingTargets);
        combined.AddRange(defendingTargets);

        for (int i = 0; i < combined.Count; i++)
        {
            for (int j = i + 1; j < combined.Count; j++)
            {
                Vector2 delta = combined[i] - combined[j];
                float distance = delta.magnitude;

                if (distance <= 0.001f || distance >= minSeparation)
                {
                    continue;
                }

                Vector2 push = delta.normalized * (minSeparation - distance) * strength * 0.5f;
                combined[i] += push;
                combined[j] -= push;
            }
        }

        for (int i = 0; i < attackingTargets.Count; i++)
        {
            attackingTargets[i] = combined[i];
        }

        for (int i = 0; i < defendingTargets.Count; i++)
        {
            defendingTargets[i] = combined[attackingTargets.Count + i];
        }
    }

    private List<int> FindNearestIndices(List<Vector2> positions, Vector2 from, int excludeIndex, int count)
    {
        List<int> indices = new List<int>();

        for (int i = 0; i < positions.Count; i++)
        {
            if (i != excludeIndex)
            {
                indices.Add(i);
            }
        }

        indices.Sort((a, b) => Vector2.Distance(positions[a], from).CompareTo(Vector2.Distance(positions[b], from)));

        if (indices.Count > count)
        {
            indices.RemoveRange(count, indices.Count - count);
        }

        return indices;
    }

    private void PushTargets(List<MatchDotToken> tokens, List<Vector2> pitchTargets)
    {
        for (int i = 0; i < tokens.Count; i++)
        {
            tokens[i].SetTargetPosition(MatchPitchVisuals.PitchToAnchoredPosition(pitchTargets[i], pitchPixelSize));
        }
    }

    // Weighted pick: midfielders and attackers carry the ball far more
    // often than defenders, goalkeepers rarely (just after a save).
    private int PickCarrierIndex(List<string> categories)
    {
        float totalWeight = 0f;
        float[] weights = new float[categories.Count];

        for (int i = 0; i < categories.Count; i++)
        {
            float weight = GetCarrierWeight(categories[i]);
            weights[i] = weight;
            totalWeight += weight;
        }

        float roll = Random.Range(0f, totalWeight);
        float cumulative = 0f;

        for (int i = 0; i < weights.Length; i++)
        {
            cumulative += weights[i];

            if (roll < cumulative)
            {
                return i;
            }
        }

        return categories.Count - 1;
    }

    private float GetCarrierWeight(string category)
    {
        switch (category)
        {
            case "GK": return 0.2f;
            case "DEF": return 1f;
            case "MID": return 3f;
            case "ATT": return 3.5f;
        }

        return 1f;
    }

    // How far (in pitch units) a role is allowed to push/retreat from its
    // anchored row. Goalkeepers barely move, midfielders roam the most.
    private float GetCategoryMaxPush(string category)
    {
        switch (category)
        {
            case "GK": return 0.5f;
            case "DEF": return 1.3f;
            case "MID": return 2.2f;
            case "ATT": return 1.7f;
        }

        return 1.5f;
    }

    // Local idle-wander jitter clamp, in anchored-pixel space, per role.
    private Vector2 GetWanderClampPixels(string category)
    {
        float bandPitchUnits;

        switch (category)
        {
            case "GK": bandPitchUnits = 0.6f; break;
            case "DEF": bandPitchUnits = 1.1f; break;
            case "MID": bandPitchUnits = 1.6f; break;
            case "ATT": bandPitchUnits = 1.3f; break;
            default: bandPitchUnits = 1.2f; break;
        }

        return MatchPitchVisuals.PitchToAnchoredPosition(new Vector2(bandPitchUnits, bandPitchUnits), pitchPixelSize);
    }

    private void ClearDots()
    {
        for (int i = pitchPanel.childCount - 1; i >= 0; i--)
        {
            Destroy(pitchPanel.GetChild(i).gameObject);
        }

        homeTokens.Clear();
        awayTokens.Clear();
        homeCategories.Clear();
        awayCategories.Clear();
    }
}
