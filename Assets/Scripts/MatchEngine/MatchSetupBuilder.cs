using System.Collections.Generic;
using UnityEngine;

// Builds a MatchSetup from a confirmed starting XI. Rating calculation is
// independent from FormationManager (kept isolated on purpose) and uses the
// same raw player attributes. Opponent ratings come from real
// NationalTeamData assets via NationalTeamOpponentBuilder.
public static class MatchSetupBuilder
{
    public static TeamMatchRatings BuildRatings(string teamName, List<PlayerData> startingEleven)
    {
        int attack = AverageStat(startingEleven, p => (p.speed + p.shoot * 2) / 3);
        int defense = AverageStat(startingEleven, p => (p.defense * 2 + p.speed) / 3);
        int midfield = AverageStat(startingEleven, p => (p.speed + p.shoot + p.defense) / 3);

        return new TeamMatchRatings(teamName, startingEleven, attack, midfield, defense);
    }

    private static int AverageStat(List<PlayerData> players, System.Func<PlayerData, int> selector)
    {
        if (players == null || players.Count == 0)
        {
            return 0;
        }

        int total = 0;

        foreach (PlayerData player in players)
        {
            total += selector(player);
        }

        return Mathf.Clamp(total / players.Count, 0, 99);
    }
}
