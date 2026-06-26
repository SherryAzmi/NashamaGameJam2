using System;

// Static event hub. Dev C raises these during match playback,
// Dev B's match-day UI listens to animate goals/cards/injuries.
public static class MatchEvents
{
    public static event Action<GoalEvent> OnGoal;
    public static event Action<CardEvent> OnCard;
    public static event Action<InjuryEvent> OnInjury;
    public static event Action<int> OnMinuteTick;
    public static event Action<MatchResult> OnMatchEnd;
    public static event Action<int, MatchSide> OnPossessionTick;
    public static event Action OnHalfTime;
    public static event Action OnHalftimeEditComplete;

    public static void RaiseGoal(GoalEvent goalEvent) => OnGoal?.Invoke(goalEvent);
    public static void RaiseCard(CardEvent cardEvent) => OnCard?.Invoke(cardEvent);
    public static void RaiseInjury(InjuryEvent injuryEvent) => OnInjury?.Invoke(injuryEvent);
    public static void RaiseMinuteTick(int minute) => OnMinuteTick?.Invoke(minute);
    public static void RaiseMatchEnd(MatchResult result) => OnMatchEnd?.Invoke(result);
    public static void RaisePossessionTick(int minute, MatchSide side) => OnPossessionTick?.Invoke(minute, side);
    public static void RaiseHalfTime() => OnHalfTime?.Invoke();
    public static void RaiseHalftimeEditComplete() => OnHalftimeEditComplete?.Invoke();
}
