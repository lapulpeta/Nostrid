using System.Collections.Concurrent;

namespace Nostrid.Model;

public class EventDetailsCount
{
    public ConcurrentBag<ReactionGroup> ReactionGroups = new();

    public int Reposts;

    public int Zaps;

    public void Add(EventDetailsCount delta)
    {
        Interlocked.Add(ref Reposts, delta.Reposts);
        Interlocked.Add(ref Zaps, delta.Zaps);
        foreach (var reactionGroup in delta.ReactionGroups)
        {
            ReactionGroups.Add(reactionGroup);
        }
    }
}

