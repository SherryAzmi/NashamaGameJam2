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
    ThroughBall,
    LongBall,
    Tackle,
    Block,
    Press,
    Cover
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

    // Opponent team's overall ratings. The enemy team's effect on every
    // chance roll comes from these team-wide numbers (not a specific
    // synthesized player), per design: your team's odds are driven by
    // whichever of your own players is on the ball, the enemy's odds are
    // driven by their team as a whole.
    public int opponentTeamAttackRating;
    public int opponentTeamMidfieldRating;
    public int opponentTeamDefenseRating;

    public float attackBoost = 1f;

    // Live field context, read off the pitch the instant the moment
    // triggers: 0 = own box, 1 = right on the goal being attacked, and how
    // many defenders are currently within challenge range of the carrier.
    // These make the decision (and its odds) reflect what's actually
    // happening, instead of every Shoot/Tackle/etc. having a fixed chance.
    public float fieldProgress = 0.5f;
    public int nearbyDefenderCount;

    // Scales every "opponent side" stat the resolver checks against,
    // without ever mutating the shared PlayerData ScriptableObject. >1
    // makes the opponent tougher (Hard), <1 makes them easier (Easy).
    public float difficultyMultiplier = 1f;

    public DecisiveMoment(DecisiveMomentType type, PlayerData actor, PlayerData opponent, int opponentTeamAttackRating, int opponentTeamMidfieldRating, int opponentTeamDefenseRating)
    {
        this.type = type;
        this.actor = actor;
        this.opponent = opponent;
        this.opponentTeamAttackRating = opponentTeamAttackRating;
        this.opponentTeamMidfieldRating = opponentTeamMidfieldRating;
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
