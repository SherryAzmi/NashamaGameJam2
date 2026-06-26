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
                float keeperDefense = (moment.opponent != null ? moment.opponent.defense : 50f) * moment.difficultyMultiplier;
                float chance = Chance(0.3f, moment.actor.shoot, keeperDefense, moment.attackBoost);
                outcome.success = Roll(chance);
                outcome.isGoal = outcome.success;
                outcome.bonusPoints = outcome.success ? 15 : 0;
                outcome.message = outcome.success ? "GOAL!" : "SAVED!";
                break;
            }

            case DecisiveAction.Pass:
            {
                float chance = Chance(0.45f, moment.actor.speed, 50f * moment.difficultyMultiplier, 1f);
                outcome.success = Roll(chance);
                outcome.nextAttackBoost = outcome.success ? 1.3f : 1f;
                outcome.bonusPoints = outcome.success ? 5 : 2;
                outcome.message = outcome.success ? "GREAT PASS - ATTACK BUILDING" : "PASS WAS OKAY";
                break;
            }

            case DecisiveAction.Dribble:
            {
                float chance = Chance(0.35f, moment.actor.speed, moment.opponentTeamDefenseRating * moment.difficultyMultiplier, 1f);
                outcome.success = Roll(chance);
                outcome.nextAttackBoost = outcome.success ? 1.5f : 1f;
                outcome.possessionTurnover = !outcome.success;
                outcome.bonusPoints = outcome.success ? 8 : 0;
                outcome.message = outcome.success ? "BEAT THE DEFENDER!" : "DRIBBLE FAILED - LOST THE BALL";
                break;
            }
        }

        return outcome;
    }

    private static DecisiveMomentOutcome ResolveDefense(DecisiveMoment moment, DecisiveAction action)
    {
        DecisiveMomentOutcome outcome = new DecisiveMomentOutcome { action = action };
        PlayerData threat = moment.opponent;

        switch (action)
        {
            case DecisiveAction.Tackle:
            {
                float chance = Chance(0.35f, moment.actor.defense, threat.speed * moment.difficultyMultiplier, 1f);
                outcome.success = Roll(chance);
                outcome.possessionTurnover = outcome.success;
                outcome.bonusPoints = outcome.success ? 8 : 0;
                outcome.message = outcome.success ? "TACKLE WON THE BALL!" : "TACKLE MISSED";
                break;
            }

            case DecisiveAction.Block:
            {
                float chance = Chance(0.4f, moment.actor.defense, threat.shoot * moment.difficultyMultiplier, 1f);
                outcome.success = Roll(chance);
                outcome.isGoal = !outcome.success;
                outcome.bonusPoints = outcome.success ? 8 : 0;
                outcome.message = outcome.success ? "BLOCKED!" : "GOAL FOR THE OPPONENT";
                break;
            }

            case DecisiveAction.Press:
            {
                float chance = Chance(0.4f, moment.actor.speed, threat.speed * moment.difficultyMultiplier, 1f);
                outcome.success = Roll(chance);
                outcome.possessionTurnover = outcome.success;
                outcome.bonusPoints = outcome.success ? 6 : 0;
                outcome.message = outcome.success ? "PRESSED INTO A MISTAKE!" : "OPPONENT KEEPS THE BALL";
                break;
            }
        }

        return outcome;
    }

    private static float Chance(float basePercent, float favorStat, float againstStat, float boost)
    {
        float chance = basePercent + (favorStat - againstStat) * 0.006f * boost;
        return Mathf.Clamp(chance, 0.08f, 0.92f);
    }

    private static bool Roll(float chance)
    {
        return Random.value < chance;
    }
}
