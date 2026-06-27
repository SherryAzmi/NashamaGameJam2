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

    // Where the ball actually was last tick, and who held it - so the next
    // carrier is chosen relative to that instead of a fresh pitch-wide
    // random pick every tick. Without this, the ball could "teleport" from
    // one box to the other in a single tick with no visual passing chain.
    private Vector2? lastBallPosition;
    private MatchSide? lastPossessingSide;

    // The last 2 carriers, tracked separately per side (index spaces don't
    // overlap between teams). Excluded from the next same-side pass pick so
    // the ball can't immediately bounce straight back to whoever just had
    // it - without this, two nearby players can end up passing back and
    // forth to each other forever since they're often each other's closest
    // neighbor on every single tick.
    private int lastCarrierIndexHome = -1;
    private int prevCarrierIndexHome = -1;
    private int lastCarrierIndexAway = -1;
    private int prevCarrierIndexAway = -1;

    // Which side is currently mirrored (attacking toward -Y instead of +Y).
    // Flipped at half-time so both teams swap ends, like real football.
    private bool homeMirrored;
    private bool awayMirrored = true;

    // The real, live ball carrier - whoever is actually holding it on the
    // pitch right now, not a fresh random pick. MatchDecisiveMomentController
    // reads this so the decision panel always matches who has the ball.
    public MatchSide? CurrentPossessingSide { get; private set; }
    public PlayerData CurrentCarrier { get; private set; }

    // Live field context for the decisive-moment system: how close the
    // carrier is to the goal they're attacking (0 = own box, 1 = right on
    // the opponent's goal line) and how many defenders are currently
    // converging on them - so decisions reflect what's actually happening
    // on the pitch instead of a context-free random trigger.
    public float CurrentAttackProgress01 { get; private set; }
    public int CurrentNearbyDefenderCount { get; private set; }

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
        lastBallPosition = null;
        lastPossessingSide = null;
        lastCarrierIndexHome = -1;
        prevCarrierIndexHome = -1;
        lastCarrierIndexAway = -1;
        prevCarrierIndexAway = -1;

        pitchPixelSize = pitchPanel.rect.size;
        pitchRestPosition = pitchPanel.anchoredPosition;

        // The away roster is always synthesized in a fixed 4-3-3 shape
        // (NationalTeamOpponentBuilder) regardless of the opponent's
        // declared preferred formation, so its layout/category lookup uses
        // that real shape rather than the (cosmetic-only) label.
        homeBasePositions = MatchPitchLayout.GetPositions(setup.home.startingEleven, setup.home.formation, homeMirrored);
        awayBasePositions = MatchPitchLayout.GetPositions(setup.away.startingEleven, "4-3-3", awayMirrored);

        SpawnTeam(setup.home.startingEleven, homeBasePositions, homeColor, homeTokens, homeCategories, setup.home.formation);
        SpawnTeam(setup.away.startingEleven, awayBasePositions, awayColor, awayTokens, awayCategories, "4-3-3");

        SpawnGoal(new Vector2(0f, -9.5f));
        SpawnGoal(new Vector2(0f, 9.5f));

        GameObject ballObject = MatchPitchVisuals.CreateDot(pitchPanel, "Ball", ballColor, ballSize, null);
        ballToken = ballObject.AddComponent<MatchDotToken>();
        ballToken.SetIndividualProfile(5f, 4f, 0.9f, 1f, 9999f, 9999f);
        ballToken.SetPositionImmediate(Vector2.zero);
    }

    private void SpawnTeam(List<PlayerData> players, List<Vector2> basePositions, Color color, List<MatchDotToken> tokens, List<string> categories, string formation)
    {
        for (int i = 0; i < players.Count; i++)
        {
            string category = MatchPitchLayout.GetCategoryForIndex(players[i].position, formation, i);

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

    // Called at half-time, before re-running Setup() for the 2nd half: both
    // teams swap which goal they attack, matching real football.
    public void SetSecondHalf()
    {
        homeMirrored = !homeMirrored;
        awayMirrored = !awayMirrored;
    }

    // Kickoff restart after a goal: snap the ball back to the center spot
    // and clear all possession-continuity state, so the next tick doesn't
    // try to "pass" from inside the goal it was just scored in.
    private void ResetBallToCenter()
    {
        if (ballToken != null)
        {
            ballToken.SetPositionImmediate(MatchPitchVisuals.PitchToAnchoredPosition(Vector2.zero, pitchPixelSize));
        }

        lastBallPosition = Vector2.zero;
        lastPossessingSide = null;
        lastCarrierIndexHome = -1;
        prevCarrierIndexHome = -1;
        lastCarrierIndexAway = -1;
        prevCarrierIndexAway = -1;
    }

    private void HandleGoalShake(GoalEvent goalEvent)
    {
        ResetBallToCenter();

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

        bool attackingMirrored = homeAttacking ? homeMirrored : awayMirrored;
        bool defendingMirrored = homeAttacking ? awayMirrored : homeMirrored;
        float attackSign = attackingMirrored ? -1f : 1f;
        float defendSign = defendingMirrored ? -1f : 1f;

        float attackPush = pushAmount * attackSign;
        float defendPush = -retreatAmount * defendSign;

        // Tactical base targets (the shape each team holds this minute),
        // before any ball-chasing adjustments.
        List<Vector2> attackingTargets = BuildTacticalTargets(attackingTokens, attackingBase, attackingCategories, attackPush);
        List<Vector2> defendingTargets = BuildTacticalTargets(defendingTokens, defendingBase, defendingCategories, defendPush);

        bool sameSideAsLastTick = lastPossessingSide.HasValue && lastPossessingSide.Value == possessingSide;

        int recentCarrier = homeAttacking ? lastCarrierIndexHome : lastCarrierIndexAway;
        int recentCarrierBefore = homeAttacking ? prevCarrierIndexHome : prevCarrierIndexAway;

        int carrierIndex = PickCarrierIndex(
            attackingTargets,
            attackingCategories,
            sameSideAsLastTick,
            attackSign,
            sameSideAsLastTick ? recentCarrier : -1,
            sameSideAsLastTick ? recentCarrierBefore : -1
        );
        Vector2 carrierPosition = attackingTargets[carrierIndex];

        if (homeAttacking)
        {
            prevCarrierIndexHome = lastCarrierIndexHome;
            lastCarrierIndexHome = carrierIndex;
        }
        else
        {
            prevCarrierIndexAway = lastCarrierIndexAway;
            lastCarrierIndexAway = carrierIndex;
        }

        CurrentPossessingSide = possessingSide;
        List<PlayerData> attackingPlayers = homeAttacking ? currentSetup.home.startingEleven : currentSetup.away.startingEleven;
        CurrentCarrier = attackingPlayers[carrierIndex];

        CurrentAttackProgress01 = Mathf.Clamp01((carrierPosition.y * attackSign + 9.5f) / 19f);
        CurrentNearbyDefenderCount = CountNearbyDefenders(defendingTargets, defendingCategories, carrierPosition);

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

        float forwardNudge = attackSign * 0.4f;
        Vector2 ballPitchPosition = carrierPosition + new Vector2(0f, forwardNudge);
        ballToken.SetTargetPosition(MatchPitchVisuals.PitchToAnchoredPosition(ballPitchPosition, pitchPixelSize));

        lastBallPosition = ballPitchPosition;
        lastPossessingSide = possessingSide;
    }

    // How many defenders (any outfield role, goalkeeper excluded) are
    // standing within "challenge range" of the ball carrier right now.
    private int CountNearbyDefenders(List<Vector2> defendingTargets, List<string> defendingCategories, Vector2 carrierPosition)
    {
        const float challengeRange = 2.6f;
        int count = 0;

        for (int i = 0; i < defendingTargets.Count; i++)
        {
            if (defendingCategories[i] == "GK")
            {
                continue;
            }

            if (Vector2.Distance(defendingTargets[i], carrierPosition) <= challengeRange)
            {
                count++;
            }
        }

        return count;
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

    [Header("Possession continuity")]
    [Tooltip("How far (pitch units) a short pass can realistically travel to the next carrier when the same side keeps the ball.")]
    public float passRange = 7f;
    [Tooltip("How many of the nearest teammates within passRange are considered as the next pass target.")]
    public int passCandidateCount = 6;

    // Same side keeps the ball: the next carrier is one of the nearby
    // teammates (a short pass), not a fresh pitch-wide random pick - so the
    // ball visibly moves through nearby players instead of teleporting.
    // Side just changed (a turnover): the new carrier is whoever on this
    // team is actually closest to where the ball was won, not a random
    // pick anywhere on the pitch. Only falls back to the old pure-random
    // weighted pick when there's no previous ball position yet (kickoff).
    private int PickCarrierIndex(
        List<Vector2> targets,
        List<string> categories,
        bool sameSideAsLastTick,
        float attackSign,
        int excludeRecent1,
        int excludeRecent2
    )
    {
        if (!lastBallPosition.HasValue)
        {
            return PickWeightedRandomIndex(categories);
        }

        if (!sameSideAsLastTick)
        {
            List<int> nearest = FindNearestIndices(targets, lastBallPosition.Value, -1, 1);
            return nearest.Count > 0 ? nearest[0] : PickWeightedRandomIndex(categories);
        }

        List<int> candidates = FindNearestIndices(targets, lastBallPosition.Value, -1, passCandidateCount);
        candidates.RemoveAll(index => Vector2.Distance(targets[index], lastBallPosition.Value) > passRange);

        // Don't pass straight back to whoever just had it (or the carrier
        // before that) - unless that leaves no one else to pass to at all.
        List<int> freshCandidates = new List<int>(candidates);
        freshCandidates.RemoveAll(index => index == excludeRecent1 || index == excludeRecent2);

        if (freshCandidates.Count > 0)
        {
            candidates = freshCandidates;
        }

        if (candidates.Count == 0)
        {
            return PickWeightedRandomIndex(categories);
        }

        float totalWeight = 0f;
        float[] weights = new float[candidates.Count];

        for (int i = 0; i < candidates.Count; i++)
        {
            Vector2 candidatePosition = targets[candidates[i]];
            float distance = Vector2.Distance(candidatePosition, lastBallPosition.Value);
            float distanceFalloff = Mathf.Clamp01(1f - distance / passRange);

            // Passes toward the goal being attacked are weighted higher
            // than sideways/backward ones, so possession tends to progress
            // instead of shuffling between the same couple of players.
            float forwardProgress = (candidatePosition.y - lastBallPosition.Value.y) * attackSign;
            float forwardBias = Mathf.Clamp(1f + forwardProgress * 0.2f, 0.4f, 2f);

            weights[i] = GetCarrierWeight(categories[candidates[i]]) * (0.3f + distanceFalloff) * forwardBias;
            totalWeight += weights[i];
        }

        float pick = Random.Range(0f, totalWeight);
        float cumulative = 0f;

        for (int i = 0; i < weights.Length; i++)
        {
            cumulative += weights[i];

            if (pick < cumulative)
            {
                return candidates[i];
            }
        }

        return candidates[candidates.Count - 1];
    }

    // Weighted pick: midfielders and attackers carry the ball far more
    // often than defenders, goalkeepers rarely (just after a save). Used
    // only as a cold-start fallback now (kickoff, or no valid nearby pass
    // target) - see PickCarrierIndex above for the normal, position-aware
    // path.
    private int PickWeightedRandomIndex(List<string> categories)
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
