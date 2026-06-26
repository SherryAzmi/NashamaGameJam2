using System.Collections.Generic;
using UnityEngine;

// Ambient minute-by-minute simulation: possession flow, cards, and
// injuries. Goals are NOT resolved here - they only happen through
// Decisive Moments (see MatchDecisiveMomentController), where the player
// chooses Shoot/Pass/Dribble or Tackle/Block/Press. This still produces a
// MatchResult + minute-ordered timeline for MatchPlaybackController to
// replay over real time.
public class MatchSimulator
{
    private const int MatchMinutes = 90;

    public MatchResult SimulateMatch(MatchSetup setup)
    {
        MatchResult result = new MatchResult();

        List<PlayerData> homeAvailable = new List<PlayerData>(setup.home.startingEleven);
        List<PlayerData> awayAvailable = new List<PlayerData>(setup.away.startingEleven);

        for (int minute = 1; minute <= MatchMinutes; minute++)
        {
            MatchSide possessingSide = SimulateMinute(minute, setup, homeAvailable, awayAvailable, result);

            result.timeline.Add(MatchTimelineEntry.MinuteTick(minute, possessingSide));
        }

        result.timeline.Add(MatchTimelineEntry.MatchEnd(result));

        return result;
    }

    private MatchSide SimulateMinute(
        int minute,
        MatchSetup setup,
        List<PlayerData> homeAvailable,
        List<PlayerData> awayAvailable,
        MatchResult result)
    {
        bool homeAttacking = RollPossession(setup.home.midfield, setup.away.midfield);

        List<PlayerData> attackingAvailable = homeAttacking ? homeAvailable : awayAvailable;
        MatchSide attackingSide = homeAttacking ? MatchSide.Home : MatchSide.Away;

        TryCard(minute, attackingSide, attackingAvailable, result);
        TryCard(minute, homeAttacking ? MatchSide.Away : MatchSide.Home, homeAttacking ? awayAvailable : homeAvailable, result);
        TryInjury(minute, MatchSide.Home, homeAvailable, result);
        TryInjury(minute, MatchSide.Away, awayAvailable, result);

        return attackingSide;
    }

    private bool RollPossession(int homeMidfield, int awayMidfield)
    {
        float homeShare = 0.5f + (homeMidfield - awayMidfield) * 0.004f;
        homeShare = Mathf.Clamp(homeShare, 0.25f, 0.75f);

        return Random.value < homeShare;
    }

    private void TryCard(int minute, MatchSide side, List<PlayerData> available, MatchResult result)
    {
        if (available.Count == 0)
        {
            return;
        }

        if (Random.value >= 0.003f)
        {
            return;
        }

        PlayerData player = available[Random.Range(0, available.Count)];
        CardType cardType = Random.value < 0.1f ? CardType.Red : CardType.Yellow;

        CardEvent cardEvent = new CardEvent(minute, side, player, cardType);
        result.cards.Add(cardEvent);
        result.timeline.Add(MatchTimelineEntry.Card(cardEvent));

        if (cardType == CardType.Red)
        {
            available.Remove(player);
        }
    }

    private void TryInjury(int minute, MatchSide side, List<PlayerData> available, MatchResult result)
    {
        if (available.Count == 0)
        {
            return;
        }

        if (Random.value >= 0.001f)
        {
            return;
        }

        PlayerData player = available[Random.Range(0, available.Count)];

        InjuryEvent injuryEvent = new InjuryEvent(minute, side, player);
        result.injuries.Add(injuryEvent);
        result.timeline.Add(MatchTimelineEntry.Injury(injuryEvent));

        available.Remove(player);
    }
}
