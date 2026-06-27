using System.Collections.Generic;
using UnityEngine;

// Builds a MatchSetup from a confirmed starting XI. Ratings go through
// TeamRatingMath - the same module FormationFieldManager uses - so the
// MatchDay preview always shows the exact same ATT/MID/DEF/POWER numbers
// the player already saw in FormationScene (formation tactics + unit
// training bonuses included), instead of a second, disagreeing formula.
// Opponent ratings come from real NationalTeamData assets via
// NationalTeamOpponentBuilder.
public static class MatchSetupBuilder
{
    public static TeamMatchRatings BuildRatings(
        string teamName,
        List<PlayerData> startingEleven,
        string formation,
        TeamTrainingState trainingState,
        Sprite flag = null
    )
    {
        TeamRatingMath.TeamRatings ratings = TeamRatingMath.CalculateAll(startingEleven, formation, trainingState);

        return new TeamMatchRatings(teamName, startingEleven, ratings.attack, ratings.midfield, ratings.defense)
        {
            power = ratings.power,
            formation = formation,
            flag = flag != null ? flag : CampaignState.Instance != null ? CampaignState.Instance.GetJordanFlag() : null
        };
    }
}
