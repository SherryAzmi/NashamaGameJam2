using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class TeamMatchRatings
{
    public string teamName;
    public List<PlayerData> startingEleven;

    public int attack;
    public int midfield;
    public int defense;
    public int power;

    // The real formation string ("4-3-3", etc.) the player actually picked
    // (home) or the opponent's preferred formation (away) - shown directly
    // in the MatchDay preview instead of being re-inferred from positions,
    // which could disagree with what was actually selected.
    public string formation;

    // Null until set by MatchSetupBuilder (home/Jordan) or
    // NationalTeamOpponentBuilder (away) - the flag sprite from the source
    // NationalTeamData, threaded through for the MatchDay preview UI.
    public Sprite flag;

    public TeamMatchRatings(string teamName, List<PlayerData> startingEleven, int attack, int midfield, int defense)
    {
        this.teamName = teamName;
        this.startingEleven = startingEleven;
        this.attack = attack;
        this.midfield = midfield;
        this.defense = defense;
        this.power = (attack + midfield + defense) / 3;
    }
}

public class MatchSetup
{
    public TeamMatchRatings home;
    public TeamMatchRatings away;

    public MatchSetup(TeamMatchRatings home, TeamMatchRatings away)
    {
        this.home = home;
        this.away = away;
    }
}

public enum MatchSide
{
    Home,
    Away
}

public class GoalEvent
{
    public int minute;
    public MatchSide side;
    public PlayerData scorer;
    public PlayerData assist;

    public GoalEvent(int minute, MatchSide side, PlayerData scorer, PlayerData assist)
    {
        this.minute = minute;
        this.side = side;
        this.scorer = scorer;
        this.assist = assist;
    }
}

public enum CardType
{
    Yellow,
    Red
}

public class CardEvent
{
    public int minute;
    public MatchSide side;
    public PlayerData player;
    public CardType cardType;

    public CardEvent(int minute, MatchSide side, PlayerData player, CardType cardType)
    {
        this.minute = minute;
        this.side = side;
        this.player = player;
        this.cardType = cardType;
    }
}

public class InjuryEvent
{
    public int minute;
    public MatchSide side;
    public PlayerData player;

    public InjuryEvent(int minute, MatchSide side, PlayerData player)
    {
        this.minute = minute;
        this.side = side;
        this.player = player;
    }
}

public class MatchResult
{
    public int homeScore;
    public int awayScore;

    public List<GoalEvent> goals = new List<GoalEvent>();
    public List<CardEvent> cards = new List<CardEvent>();
    public List<InjuryEvent> injuries = new List<InjuryEvent>();

    // Every event in the match, ordered by minute, for timed playback.
    public List<MatchTimelineEntry> timeline = new List<MatchTimelineEntry>();

    public PlayerData manOfTheMatch;
}

public enum MatchTimelineEntryType
{
    Goal,
    Card,
    Injury,
    MinuteTick,
    MatchEnd
}

public class MatchTimelineEntry
{
    public int minute;
    public MatchTimelineEntryType type;
    public GoalEvent goal;
    public CardEvent card;
    public InjuryEvent injury;
    public MatchResult finalResult;
    public MatchSide possessingSide;

    public static MatchTimelineEntry MinuteTick(int minute, MatchSide possessingSide)
    {
        return new MatchTimelineEntry { minute = minute, type = MatchTimelineEntryType.MinuteTick, possessingSide = possessingSide };
    }

    public static MatchTimelineEntry Goal(GoalEvent goalEvent)
    {
        return new MatchTimelineEntry { minute = goalEvent.minute, type = MatchTimelineEntryType.Goal, goal = goalEvent };
    }

    public static MatchTimelineEntry Card(CardEvent cardEvent)
    {
        return new MatchTimelineEntry { minute = cardEvent.minute, type = MatchTimelineEntryType.Card, card = cardEvent };
    }

    public static MatchTimelineEntry Injury(InjuryEvent injuryEvent)
    {
        return new MatchTimelineEntry { minute = injuryEvent.minute, type = MatchTimelineEntryType.Injury, injury = injuryEvent };
    }

    public static MatchTimelineEntry MatchEnd(MatchResult result)
    {
        return new MatchTimelineEntry { minute = 90, type = MatchTimelineEntryType.MatchEnd, finalResult = result };
    }
}
