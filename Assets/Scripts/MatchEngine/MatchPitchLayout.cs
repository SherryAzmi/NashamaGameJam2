using System.Collections.Generic;
using UnityEngine;

// Computes dot positions for a starting XI purely from each player's own
// "position" attribute (no dependency on FormationManager's private slot
// data). Pitch space: x in [-5, 5], y in [-10, 10]. Home team defends
// y = -10, attacks toward y = +10. Away team is mirrored (rotated 180).
public static class MatchPitchLayout
{
    private const float GoalkeeperY = -9f;
    private const float DefenseY = -5f;
    private const float MidfieldY = -1f;
    private const float AttackY = 5f;

    public static List<Vector2> GetPositions(List<PlayerData> startingEleven, bool mirrored)
    {
        List<PlayerData> goalkeepers = new List<PlayerData>();
        List<PlayerData> defenders = new List<PlayerData>();
        List<PlayerData> midfielders = new List<PlayerData>();
        List<PlayerData> attackers = new List<PlayerData>();

        foreach (PlayerData player in startingEleven)
        {
            switch (GetCategory(player.position))
            {
                case "GK": goalkeepers.Add(player); break;
                case "DEF": defenders.Add(player); break;
                case "MID": midfielders.Add(player); break;
                default: attackers.Add(player); break;
            }
        }

        Dictionary<PlayerData, Vector2> positionByPlayer = new Dictionary<PlayerData, Vector2>();

        AssignRow(goalkeepers, GoalkeeperY, positionByPlayer);
        AssignRow(defenders, DefenseY, positionByPlayer);
        AssignRow(midfielders, MidfieldY, positionByPlayer);
        AssignRow(attackers, AttackY, positionByPlayer);

        List<Vector2> result = new List<Vector2>();

        foreach (PlayerData player in startingEleven)
        {
            Vector2 position = positionByPlayer[player];

            if (mirrored)
            {
                position = new Vector2(-position.x, -position.y);
            }

            result.Add(position);
        }

        return result;
    }

    public static string InferFormationLabel(List<PlayerData> startingEleven)
    {
        int defenders = 0;
        int midfielders = 0;
        int attackers = 0;

        foreach (PlayerData player in startingEleven)
        {
            switch (GetCategory(player.position))
            {
                case "DEF": defenders++; break;
                case "MID": midfielders++; break;
                case "ATT": attackers++; break;
            }
        }

        return $"{defenders}-{midfielders}-{attackers}";
    }

    private static void AssignRow(List<PlayerData> players, float y, Dictionary<PlayerData, Vector2> positionByPlayer)
    {
        if (players.Count == 0)
        {
            return;
        }

        float spread = 8f;

        for (int i = 0; i < players.Count; i++)
        {
            float t = players.Count == 1 ? 0.5f : (float)i / (players.Count - 1);
            float x = Mathf.Lerp(-spread / 2f, spread / 2f, t);

            positionByPlayer[players[i]] = new Vector2(x, y);
        }
    }

    public static string GetCategory(string position)
    {
        if (string.IsNullOrWhiteSpace(position))
        {
            return "MID";
        }

        switch (position.Trim().ToUpperInvariant())
        {
            case "GK":
                return "GK";

            case "LB":
            case "RB":
            case "CB":
            case "LWB":
            case "RWB":
                return "DEF";

            case "LM":
            case "RM":
            case "DM":
            case "CM":
            case "CAM":
            case "AM":
                return "MID";

            case "LW":
            case "RW":
            case "ST":
                return "ATT";
        }

        return "MID";
    }
}
