using System.Collections.Generic;

// Runtime knockout bracket structures for the World Cup stage (Round of 16
// through the Final). Lives in CampaignState, which owns a List<BracketRound>
// and advances it round by round as Jordan's matches are played.

public class BracketMatch
{
    public NationalTeamData teamA;
    public NationalTeamData teamB;

    // True if either side of this match is Jordan - this is the match the
    // player actually plays; every other match in the round is simulated
    // instantly once Jordan's match for that round is recorded.
    public bool isJordanMatch;

    public bool played;
    public int scoreA;
    public int scoreB;

    // Set only for Jordan's own matches that went to a shootout. The
    // regulation score above is still recorded (and may be level) - this
    // is the separate penalty score shown alongside it in the UI.
    public bool hasPenalties;
    public int penaltyScoreA;
    public int penaltyScoreB;

    // Null until played. Knockout matches never end level - ties are
    // resolved (a coin-weighted tiebreak for simulated matches, a penalty
    // shootout for Jordan's own live matches) before being marked played.
    public NationalTeamData Winner
    {
        get
        {
            if (!played || teamA == null || teamB == null)
            {
                return null;
            }

            return scoreA >= scoreB ? teamA : teamB;
        }
    }
}

public class BracketRound
{
    public string roundName;
    public List<BracketMatch> matches = new List<BracketMatch>();

    public bool AllPlayed()
    {
        foreach (BracketMatch match in matches)
        {
            if (!match.played)
            {
                return false;
            }
        }

        return true;
    }
}
