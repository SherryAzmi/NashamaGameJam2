using System.Collections.Generic;
using UnityEngine;

// Converts a NationalTeamData (Dev A's aggregate-rating asset) into a
// playable opponent for the match engine. NationalTeamData has no
// individual player roster, only team-level ratings, so this synthesizes
// 11 transient PlayerData instances (created at runtime via
// ScriptableObject.CreateInstance, never written to the shared asset)
// spread around those ratings in a standard 4-3-3 shape.
public static class NationalTeamOpponentBuilder
{
    private static readonly string[] RosterPositions =
    {
        "GK",
        "CB", "CB", "LB", "RB",
        "CM", "CM", "CM",
        "ST", "LW", "RW"
    };

    private const int StatVariance = 8;

    public static TeamMatchRatings BuildOpponentRatings(NationalTeamData team)
    {
        List<PlayerData> roster = GenerateRoster(team);
        string teamName = string.IsNullOrWhiteSpace(team.teamName) ? team.name : team.teamName;

        return new TeamMatchRatings(teamName, roster, team.attack, team.midfield, team.defense)
        {
            formation = string.IsNullOrWhiteSpace(team.preferredFormation) ? "4-3-3" : team.preferredFormation,
            flag = team.flag
        };
    }

    private static List<PlayerData> GenerateRoster(NationalTeamData team)
    {
        string teamName = string.IsNullOrWhiteSpace(team.teamName) ? team.name : team.teamName;
        List<PlayerData> roster = new List<PlayerData>();

        for (int i = 0; i < RosterPositions.Length; i++)
        {
            string position = RosterPositions[i];

            PlayerData player = ScriptableObject.CreateInstance<PlayerData>();
            player.playerName = teamName + " #" + (i + 1);
            player.club = teamName;
            player.nationality = teamName;
            player.category = "Opponent";
            player.position = position;

            player.speed = Vary(team.midfield);
            player.shoot = position == "GK" ? Vary(team.goalkeeper / 2) : Vary(team.attack);
            player.defense = position == "GK" ? Vary(team.goalkeeper) : Vary(team.defense);

            roster.Add(player);
        }

        return roster;
    }

    private static int Vary(int baseStat)
    {
        int value = baseStat + Random.Range(-StatVariance, StatVariance + 1);
        return Mathf.Clamp(value, 1, 99);
    }
}
