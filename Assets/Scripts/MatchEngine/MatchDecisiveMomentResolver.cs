using UnityEngine;

// Pure resolution math for a Decisive Moment. Each check is a stat-driven
// chance (winner's stat minus loser's stat, scaled, plus a base chance),
// matching the design doc's "stat x random vs stat x random" intent.
// Every "opponent side" stat is scaled by moment.difficultyMultiplier so
// Easy/Medium/Hard changes outcomes without ever touching the shared
// PlayerData ScriptableObjects.
public static class MatchDecisiveMomentResolver
{
    public static DecisiveMomentOutcome Resolve(DecisiveMoment moment, DecisiveAction action)
    {
        return moment.type == DecisiveMomentType.Attack
            ? ResolveAttack(moment, action)
            : ResolveDefense(moment, action);
    }

    private static DecisiveMomentOutcome ResolveAttack(DecisiveMoment moment, DecisiveAction action)
    {
        DecisiveMomentOutcome outcome = new DecisiveMomentOutcome { action = action };

        switch (action)
        {
            case DecisiveAction.Shoot:
            {
                float chance = ShootChance(moment);
                outcome.success = Roll(chance);
                outcome.isGoal = outcome.success;
                outcome.bonusPoints = outcome.success ? 15 : 0;
                outcome.message = outcome.success ? "GOAL!" : "SAVED!";
                break;
            }

            case DecisiveAction.Pass:
            {
                float chance = PassChance(moment);
                outcome.success = Roll(chance);
                outcome.nextAttackBoost = outcome.success ? 1.3f : 1f;
                outcome.bonusPoints = outcome.success ? 5 : 2;
                outcome.message = outcome.success ? "GREAT PASS - ATTACK BUILDING" : "PASS WAS OKAY";
                break;
            }

            case DecisiveAction.Dribble:
            {
                float chance = DribbleChance(moment);
                outcome.success = Roll(chance);
                outcome.nextAttackBoost = outcome.success ? 1.5f : 1f;
                outcome.possessionTurnover = !outcome.success;
                outcome.bonusPoints = outcome.success ? 8 : 0;
                outcome.message = outcome.success ? "BEAT THE DEFENDER!" : "DRIBBLE FAILED - LOST THE BALL";
                break;
            }

            case DecisiveAction.ThroughBall:
            {
                float chance = ThroughBallChance(moment);
                outcome.success = Roll(chance);
                outcome.nextAttackBoost = outcome.success ? 1.8f : 1f;
                outcome.possessionTurnover = !outcome.success;
                outcome.bonusPoints = outcome.success ? 10 : 0;
                outcome.message = outcome.success ? "SPLIT THE DEFENSE - CLEAR CHANCE!" : "THROUGH BALL CUT OUT";
                break;
            }

            case DecisiveAction.LongBall:
            {
                float chance = LongBallChance(moment);
                outcome.success = Roll(chance);
                outcome.nextAttackBoost = outcome.success ? 1.4f : 1f;
                outcome.possessionTurnover = !outcome.success;
                outcome.bonusPoints = outcome.success ? 6 : 0;
                outcome.message = outcome.success ? "LONG BALL FOUND ITS TARGET" : "LONG BALL OVERHIT - LOST POSSESSION";
                break;
            }
        }

        return outcome;
    }

    private static DecisiveMomentOutcome ResolveDefense(DecisiveMoment moment, DecisiveAction action)
    {
        DecisiveMomentOutcome outcome = new DecisiveMomentOutcome { action = action };

        switch (action)
        {
            case DecisiveAction.Tackle:
            {
                float chance = TackleChance(moment);
                outcome.success = Roll(chance);
                outcome.possessionTurnover = outcome.success;
                outcome.bonusPoints = outcome.success ? 8 : 0;
                outcome.message = outcome.success ? "TACKLE WON THE BALL!" : "TACKLE MISSED";
                break;
            }

            case DecisiveAction.Block:
            {
                float chance = BlockChance(moment);
                outcome.success = Roll(chance);
                outcome.isGoal = !outcome.success;
                outcome.bonusPoints = outcome.success ? 8 : 0;
                outcome.message = outcome.success ? "BLOCKED!" : "GOAL FOR THE OPPONENT";
                break;
            }

            case DecisiveAction.Press:
            {
                float chance = PressChance(moment);
                outcome.success = Roll(chance);
                outcome.possessionTurnover = outcome.success;
                outcome.bonusPoints = outcome.success ? 6 : 0;
                outcome.message = outcome.success ? "PRESSED INTO A MISTAKE!" : "OPPONENT KEEPS THE BALL";
                break;
            }

            case DecisiveAction.Cover:
            {
                float chance = CoverChance(moment);
                outcome.success = Roll(chance);
                outcome.possessionTurnover = false;
                outcome.bonusPoints = outcome.success ? 4 : 0;
                outcome.message = outcome.success ? "CONTAINED - NO WAY THROUGH" : "BEATEN FOR PACE, STILL DANGEROUS";
                break;
            }
        }

        return outcome;
    }

    // Exposed so the UI can show an approximate success percentage on each
    // decision button before the player commits to an action.
    public static float PreviewChance(DecisiveMoment moment, DecisiveAction action)
    {
        switch (action)
        {
            case DecisiveAction.Shoot: return ShootChance(moment);
            case DecisiveAction.Pass: return PassChance(moment);
            case DecisiveAction.Dribble: return DribbleChance(moment);
            case DecisiveAction.ThroughBall: return ThroughBallChance(moment);
            case DecisiveAction.LongBall: return LongBallChance(moment);
            case DecisiveAction.Tackle: return TackleChance(moment);
            case DecisiveAction.Block: return BlockChance(moment);
            case DecisiveAction.Press: return PressChance(moment);
            case DecisiveAction.Cover: return CoverChance(moment);
        }

        return 0.5f;
    }

    // Closer to goal = an easier look at it; more defenders converging =
    // a harder one, on top of the keeper's own stat. Momentum from a
    // previous successful action in this same spell (attackBoost > 1)
    // gives a flat chance bonus here too - "the pass worked, so the next
    // player's shot is a better one" - instead of only ever affecting the
    // very next Shoot like before.
    private static float ShootChance(DecisiveMoment moment)
    {
        float keeperDefense = moment.opponentTeamDefenseRating * moment.difficultyMultiplier;
        float defenderPenalty = moment.nearbyDefenderCount * 7f;
        float basePercent = Mathf.Lerp(0.14f, 0.42f, moment.fieldProgress);

        return Chance(basePercent, moment.actor.shoot, keeperDefense + defenderPenalty, moment.attackBoost);
    }

    private static float PassChance(DecisiveMoment moment)
    {
        return Chance(0.45f, moment.actor.speed, moment.opponentTeamMidfieldRating * moment.difficultyMultiplier, moment.attackBoost);
    }

    // More defenders crowding the ball carrier makes beating "a defender"
    // harder than the abstract team-defense rating alone implies.
    private static float DribbleChance(DecisiveMoment moment)
    {
        float defenderPenalty = moment.nearbyDefenderCount * 5f;

        return Chance(
            0.35f,
            moment.actor.speed,
            moment.opponentTeamDefenseRating * moment.difficultyMultiplier + defenderPenalty,
            moment.attackBoost
        );
    }

    // Riskier than a normal Pass (threading it between defenders), but the
    // payoff if it works is a near-clear chance next time. Crowded boxes
    // make it harder to find the gap.
    private static float ThroughBallChance(DecisiveMoment moment)
    {
        float defenderPenalty = moment.nearbyDefenderCount * 9f;

        return Chance(0.32f, moment.actor.speed, moment.opponentTeamDefenseRating * moment.difficultyMultiplier + defenderPenalty, moment.attackBoost);
    }

    // A hopeful ball forward from deep - lower success than a short Pass,
    // but doesn't depend on how many defenders are right on top of the
    // carrier right now since it's going over their heads.
    private static float LongBallChance(DecisiveMoment moment)
    {
        return Chance(0.4f, moment.actor.speed, moment.opponentTeamDefenseRating * moment.difficultyMultiplier, moment.attackBoost);
    }

    // Extra defenders already converging on the ball help the tackle.
    // The threat's PlayerData is only used for display (who's on the
    // ball); the actual difficulty comes from the enemy team's overall
    // midfield rating - your team's odds are per-player, theirs are
    // team-wide, per design.
    private static float TackleChance(DecisiveMoment moment)
    {
        float supportBonus = moment.nearbyDefenderCount * 4f;

        return Chance(0.35f, moment.actor.defense + supportBonus, moment.opponentTeamMidfieldRating * moment.difficultyMultiplier, 1f);
    }

    // The deeper the threat already is into the box, the harder the block.
    private static float BlockChance(DecisiveMoment moment)
    {
        float dangerPenalty = moment.fieldProgress * 12f;

        return Chance(0.4f, moment.actor.defense, moment.opponentTeamAttackRating * moment.difficultyMultiplier + dangerPenalty, 1f);
    }

    private static float PressChance(DecisiveMoment moment)
    {
        float supportBonus = moment.nearbyDefenderCount * 3f;

        return Chance(0.4f, moment.actor.speed + supportBonus, moment.opponentTeamMidfieldRating * moment.difficultyMultiplier, 1f);
    }

    // The safe option for an isolated defender: easier than a Tackle (no
    // real risk of being skinned outright), but it only contains the
    // threat rather than winning the ball back.
    private static float CoverChance(DecisiveMoment moment)
    {
        return Chance(0.5f, moment.actor.defense, moment.opponentTeamMidfieldRating * moment.difficultyMultiplier, 1f);
    }

    // favorStat/againstStat drive the base stat-vs-stat swing; momentum is
    // a separate flat bonus (only attack actions pass anything but 1f) so
    // a hot streak always helps regardless of which way the raw stats lean.
    // favorStat (always the player's/team's own side taking the action) is
    // weighted higher than againstStat on purpose - a slight house lean
    // toward whoever's acting, so a good stat on your side counts for more
    // than the same gap counts against you, making the game a bit easier
    // overall without flattening the stat-vs-stat feel.
    private static float Chance(float basePercent, float favorStat, float againstStat, float momentum)
    {
        float chance = basePercent + favorStat * 0.018f - againstStat * 0.011f + (momentum - 1f) * 0.18f;
        return Mathf.Clamp(chance, 0.05f, 0.95f);
    }

    private static bool Roll(float chance)
    {
        return Random.value < chance;
    }
}
