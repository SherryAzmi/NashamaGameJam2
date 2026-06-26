// Shared "normal speed" baseline. MatchDayController writes this from its
// Inspector-tunable gameSpeed field; MatchDecisiveMomentController reads it
// so slow-motion during a decision is relative to your chosen speed
// instead of always resetting back to 1x.
public static class MatchSpeed
{
    public static float Normal = 1f;
}
