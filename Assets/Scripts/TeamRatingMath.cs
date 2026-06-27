using System.Collections.Generic;
using UnityEngine;

// Single source of truth for ATT/MID/DEF/POWER/CHEMISTRY. FormationScene and
// the MatchDay preview used to compute these with two different, unrelated
// formulas (FormationFieldManager's own private methods vs a standalone
// average in MatchSetupBuilder) and disagreed for the exact same squad.
// Both now call into this class so a given squad + formation + training
// state always produces the exact same numbers everywhere they're shown.
public static class TeamRatingMath
{
    // Mirrors FormationFieldManager.FormationSlot, minus the Vector2 layout
    // position - rating math only ever needs the slot's category and which
    // player positions count as an exact fit for it.
    public class RatingSlot
    {
        public string category;
        public string[] preferredPositions;

        public RatingSlot(string category, params string[] preferredPositions)
        {
            this.category = category;
            this.preferredPositions = preferredPositions;
        }
    }

    public struct TeamRatings
    {
        public int attack;
        public int midfield;
        public int defense;
        public int power;
        public int chemistry;
    }

    public static TeamRatings CalculateAll(
        List<PlayerData> players,
        string formation,
        TeamTrainingState trainingState
    )
    {
        RatingSlot[] slots = GetFormationRatingSlots(formation);

        return new TeamRatings
        {
            attack = CalculateUnitRating(players, slots, "ATT", formation, trainingState),
            midfield = CalculateUnitRating(players, slots, "MID", formation, trainingState),
            defense = CalculateUnitRating(players, slots, "DEF", formation, trainingState),
            power = CalculateTeamPower(players, slots, formation),
            chemistry = CalculateChemistry(players, slots, formation, trainingState)
        };
    }

    public static int CalculateUnitRating(
        List<PlayerData> players,
        RatingSlot[] slots,
        string unit,
        string formation,
        TeamTrainingState trainingState
    )
    {
        float total = 0f;
        int count = 0;

        for (int i = 0; i < players.Count && i < slots.Length; i++)
        {
            if (slots[i].category != unit)
            {
                continue;
            }

            PlayerData player = players[i];

            if (player == null)
            {
                continue;
            }

            if (unit == "ATT")
            {
                total += (player.speed + player.shoot * 2f) / 3f;
            }
            else if (unit == "DEF")
            {
                total += (player.defense * 2f + player.speed) / 3f;
            }
            else
            {
                total += GetOverall(player);
            }

            count++;
        }

        if (count == 0)
        {
            return 0;
        }

        int tacticalBonus = GetTacticalBonus(formation, unit);
        int trainingBonus = GetTrainingUnitBonus(unit, trainingState);

        return Mathf.Clamp(
            Mathf.RoundToInt(total / count) + tacticalBonus + trainingBonus,
            0,
            99
        );
    }

    public static int CalculateTeamPower(
        List<PlayerData> players,
        RatingSlot[] slots,
        string formation
    )
    {
        float totalOverall = 0f;
        float fitBonus = 0f;
        int count = 0;

        for (int i = 0; i < players.Count && i < slots.Length; i++)
        {
            PlayerData player = players[i];

            if (player == null)
            {
                continue;
            }

            totalOverall += GetOverall(player);
            fitBonus += GetFitBonus(player, slots[i]);
            count++;
        }

        if (count == 0)
        {
            return 0;
        }

        return Mathf.Clamp(
            Mathf.RoundToInt(totalOverall / count + fitBonus / count) + GetFormationPowerBonus(formation),
            0,
            99
        );
    }

    public static int CalculateChemistry(
        List<PlayerData> players,
        RatingSlot[] slots,
        string formation,
        TeamTrainingState trainingState
    )
    {
        int chemistry = 45;

        for (int i = 0; i < players.Count && i < slots.Length; i++)
        {
            PlayerData player = players[i];

            if (player == null)
            {
                continue;
            }

            if (IsExactFit(player, slots[i]))
            {
                chemistry += 4;
            }
            else if (GetPositionCategory(player.position) == slots[i].category)
            {
                chemistry += 2;
            }
            else
            {
                chemistry -= 3;
            }
        }

        for (int i = 0; i < players.Count; i++)
        {
            for (int j = i + 1; j < players.Count; j++)
            {
                PlayerData first = players[i];
                PlayerData second = players[j];

                if (first == null || second == null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(first.club) && first.club == second.club)
                {
                    chemistry += 2;
                }
            }
        }

        chemistry += GetFormationChemistryBonus(formation);
        chemistry += trainingState != null ? trainingState.chemistryBonus : 0;

        return Mathf.Clamp(chemistry, 0, 100);
    }

    private static int GetTrainingUnitBonus(string unit, TeamTrainingState trainingState)
    {
        if (trainingState == null)
        {
            return 0;
        }

        switch (unit)
        {
            case "ATT":
                return trainingState.attackUnitBonus;

            case "MID":
                return trainingState.midfieldUnitBonus;

            case "DEF":
                return trainingState.defenseUnitBonus + trainingState.goalkeeperUnitBonus / 2;
        }

        return 0;
    }

    public static int GetOverall(PlayerData player)
    {
        return player == null ? 0 : (player.speed + player.shoot + player.defense) / 3;
    }

    public static bool IsExactFit(PlayerData player, RatingSlot slot)
    {
        string playerPosition = NormalizePosition(player.position);

        foreach (string preferredPosition in slot.preferredPositions)
        {
            if (playerPosition == NormalizePosition(preferredPosition))
            {
                return true;
            }
        }

        return false;
    }

    public static int GetFitBonus(PlayerData player, RatingSlot slot)
    {
        if (IsExactFit(player, slot))
        {
            return 3;
        }

        if (GetPositionCategory(player.position) == slot.category)
        {
            return 1;
        }

        return -4;
    }

    public static string NormalizePosition(string position)
    {
        return string.IsNullOrWhiteSpace(position) ? "" : position.Trim().ToUpperInvariant();
    }

    public static string GetPositionCategory(string position)
    {
        switch (NormalizePosition(position))
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
            case "LDM":
            case "RDM":
                return "MID";

            case "LW":
            case "RW":
            case "ST":
                return "ATT";
        }

        return "MID";
    }

    public static int GetFormationPowerBonus(string formation)
    {
        switch (formation)
        {
            case "4-3-3": return 2;
            case "4-4-2": return 1;
            case "4-2-3-1": return 2;
            case "3-5-2": return 1;
            case "4-3-2-1": return 2;
        }

        return 0;
    }

    public static int GetFormationChemistryBonus(string formation)
    {
        switch (formation)
        {
            case "4-3-3": return 3;
            case "4-4-2": return 4;
            case "4-2-3-1": return 5;
            case "3-5-2": return 1;
            case "4-3-2-1": return 3;
        }

        return 0;
    }

    public static int GetTacticalBonus(string formation, string unit)
    {
        switch (formation)
        {
            case "4-3-3":
                if (unit == "ATT") return 5;
                if (unit == "MID") return 3;
                if (unit == "DEF") return 1;
                break;

            case "4-4-2":
                if (unit == "ATT") return 3;
                if (unit == "MID") return 3;
                if (unit == "DEF") return 5;
                break;

            case "4-2-3-1":
                if (unit == "ATT") return 4;
                if (unit == "MID") return 6;
                if (unit == "DEF") return 3;
                break;

            case "3-5-2":
                if (unit == "ATT") return 4;
                if (unit == "MID") return 7;
                if (unit == "DEF") return -2;
                break;

            case "4-3-2-1":
                if (unit == "ATT") return 5;
                if (unit == "MID") return 4;
                if (unit == "DEF") return 2;
                break;
        }

        return 0;
    }

    // Category + preferred-position shape only (no layout coordinates) for
    // every formation FormationFieldManager supports. Must stay in sync
    // with FormationFieldManager.GetFormationSlots if a formation's shape
    // ever changes there.
    public static RatingSlot[] GetFormationRatingSlots(string formation)
    {
        switch (formation)
        {
            case "4-4-2":
                return new RatingSlot[]
                {
                    new RatingSlot("GK", "GK"),
                    new RatingSlot("DEF", "LB"),
                    new RatingSlot("DEF", "CB"),
                    new RatingSlot("DEF", "CB"),
                    new RatingSlot("DEF", "RB"),
                    new RatingSlot("MID", "LW", "LM", "CM", "AM"),
                    new RatingSlot("MID", "CM", "DM", "CAM", "AM"),
                    new RatingSlot("MID", "CM", "DM", "CAM", "AM"),
                    new RatingSlot("MID", "RW", "RM", "CM", "AM"),
                    new RatingSlot("ATT", "ST", "LW"),
                    new RatingSlot("ATT", "ST", "RW")
                };

            case "4-2-3-1":
                return new RatingSlot[]
                {
                    new RatingSlot("GK", "GK"),
                    new RatingSlot("DEF", "LB"),
                    new RatingSlot("DEF", "CB"),
                    new RatingSlot("DEF", "CB"),
                    new RatingSlot("DEF", "RB"),
                    new RatingSlot("MID", "DM", "CM"),
                    new RatingSlot("MID", "DM", "CM"),
                    new RatingSlot("MID", "LW", "AM", "CAM"),
                    new RatingSlot("MID", "CAM", "AM", "CM"),
                    new RatingSlot("MID", "RW", "AM", "CAM"),
                    new RatingSlot("ATT", "ST")
                };

            case "3-5-2":
                // Note: LWB/RWB are categorized DEF here (not MID), matching
                // FormationFieldManager.GetSlotCategory's existing behavior
                // for those two slot names - even though that makes this
                // formation's actual unit split 5 DEF / 3 MID rather than
                // the "3-5-2" name's literal 3/5. Kept as-is on purpose so
                // the numbers match what FormationScene already shows.
                return new RatingSlot[]
                {
                    new RatingSlot("GK", "GK"),
                    new RatingSlot("DEF", "CB"),
                    new RatingSlot("DEF", "CB"),
                    new RatingSlot("DEF", "CB"),
                    new RatingSlot("DEF", "LB", "LW"),
                    new RatingSlot("MID", "CM", "DM", "CAM"),
                    new RatingSlot("MID", "CM", "DM", "CAM"),
                    new RatingSlot("MID", "CM", "DM", "CAM"),
                    new RatingSlot("DEF", "RB", "RW"),
                    new RatingSlot("ATT", "ST", "LW"),
                    new RatingSlot("ATT", "ST", "RW")
                };

            case "4-3-2-1":
                return new RatingSlot[]
                {
                    new RatingSlot("GK", "GK"),
                    new RatingSlot("DEF", "LB"),
                    new RatingSlot("DEF", "CB"),
                    new RatingSlot("DEF", "CB"),
                    new RatingSlot("DEF", "RB"),
                    new RatingSlot("MID", "CM", "DM", "AM"),
                    new RatingSlot("MID", "CM", "DM", "AM"),
                    new RatingSlot("MID", "CM", "DM", "AM"),
                    new RatingSlot("ATT", "CAM", "AM", "LW", "ST"),
                    new RatingSlot("ATT", "CAM", "AM", "RW", "ST"),
                    new RatingSlot("ATT", "ST")
                };

            default:
                return new RatingSlot[]
                {
                    new RatingSlot("GK", "GK"),
                    new RatingSlot("DEF", "LB"),
                    new RatingSlot("DEF", "CB"),
                    new RatingSlot("DEF", "CB"),
                    new RatingSlot("DEF", "RB"),
                    new RatingSlot("MID", "CM", "DM", "CAM", "AM"),
                    new RatingSlot("MID", "CM", "DM", "CAM", "AM"),
                    new RatingSlot("MID", "CM", "DM", "CAM", "AM"),
                    new RatingSlot("ATT", "LW"),
                    new RatingSlot("ATT", "ST"),
                    new RatingSlot("ATT", "RW")
                };
        }
    }
}
