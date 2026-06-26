// "Decisive Moment" data, per the game-jam design doc: every so often the
// match pauses and the player chooses an action instead of everything
// being resolved automatically in the background. PlayerData only has
// speed/shoot/defense (no separate passing/dribbling stats), so Pass and
// Dribble use speed as a tempo/agility stand-in.
public enum DecisiveMomentType
{
    Attack,
    Defense
}

public enum DecisiveAction
{
    Shoot,
    Pass,
    Dribble,
    Tackle,
    Block,
    Press
}

public class DecisiveMoment
{
    public DecisiveMomentType type;

    // Your player taking the action (the carrier on Attack, the responding
    // defender on Defense).
    public PlayerData actor;

    // The relevant opposing player: their goalkeeper on Attack-Shoot, the
    // attacking threat itself on Defense.
    public PlayerData opponent;

    // Opponent team's overall defense rating, used for the Attack-Dribble
    // check (beating "a defender" in the abstract, not one specific player).
    public int opponentTeamDefenseRating;

    public float attackBoost = 1f;

    // Scales every "opponent side" stat the resolver checks against,
    // without ever mutating the shared PlayerData ScriptableObject. >1
    // makes the opponent tougher (Hard), <1 makes them easier (Easy).
    public float difficultyMultiplier = 1f;

    public DecisiveMoment(DecisiveMomentType type, PlayerData actor, PlayerData opponent, int opponentTeamDefenseRating)
    {
        this.type = type;
        this.actor = actor;
        this.opponent = opponent;
        this.opponentTeamDefenseRating = opponentTeamDefenseRating;
    }
}

public class DecisiveMomentOutcome
{
    public DecisiveAction action;
    public bool success;
    public bool isGoal;
    public bool possessionTurnover;
    public int bonusPoints;
    public float nextAttackBoost = 1f;
    public string message;
}
