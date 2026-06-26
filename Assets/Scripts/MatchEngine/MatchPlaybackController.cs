using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Plays back an already-simulated MatchResult over real time, instead of
// resolving it instantly. Maps the 90 in-game minutes onto a configurable
// real-time duration (default ~3 minutes) so events land progressively.
public class MatchPlaybackController : MonoBehaviour
{
    [Tooltip("How many real seconds the full 90-minute match should take.")]
    public float realSecondsForFullMatch = 180f;

    private Coroutine playbackRoutine;
    private bool isPaused;

    public void PlayMatch(MatchResult result)
    {
        if (playbackRoutine != null)
        {
            StopCoroutine(playbackRoutine);
        }

        isPaused = false;
        playbackRoutine = StartCoroutine(PlaybackRoutine(result));
    }

    // Used by MatchDecisiveMomentController to freeze minute progression
    // while the player is looking at a decision panel.
    public void Pause()
    {
        isPaused = true;
    }

    public void Resume()
    {
        isPaused = false;
    }

    private IEnumerator PlaybackRoutine(MatchResult result)
    {
        float secondsPerMinute = realSecondsForFullMatch / 90f;

        Dictionary<int, List<MatchTimelineEntry>> entriesByMinute = GroupByMinute(result.timeline);

        for (int minute = 1; minute <= 90; minute++)
        {
            yield return new WaitUntil(() => !isPaused);

            if (entriesByMinute.TryGetValue(minute, out List<MatchTimelineEntry> entries))
            {
                foreach (MatchTimelineEntry entry in entries)
                {
                    RaiseEntry(entry);
                }
            }

            yield return new WaitForSeconds(secondsPerMinute);
        }

        MatchEvents.RaiseMatchEnd(result);

        playbackRoutine = null;
    }

    private Dictionary<int, List<MatchTimelineEntry>> GroupByMinute(List<MatchTimelineEntry> timeline)
    {
        Dictionary<int, List<MatchTimelineEntry>> grouped = new Dictionary<int, List<MatchTimelineEntry>>();

        foreach (MatchTimelineEntry entry in timeline)
        {
            if (entry.type == MatchTimelineEntryType.MatchEnd)
            {
                continue;
            }

            if (!grouped.ContainsKey(entry.minute))
            {
                grouped[entry.minute] = new List<MatchTimelineEntry>();
            }

            grouped[entry.minute].Add(entry);
        }

        return grouped;
    }

    private void RaiseEntry(MatchTimelineEntry entry)
    {
        switch (entry.type)
        {
            case MatchTimelineEntryType.MinuteTick:
                MatchEvents.RaiseMinuteTick(entry.minute);
                MatchEvents.RaisePossessionTick(entry.minute, entry.possessingSide);
                break;

            case MatchTimelineEntryType.Goal:
                MatchEvents.RaiseGoal(entry.goal);
                break;

            case MatchTimelineEntryType.Card:
                MatchEvents.RaiseCard(entry.card);
                break;

            case MatchTimelineEntryType.Injury:
                MatchEvents.RaiseInjury(entry.injury);
                break;
        }
    }
}
