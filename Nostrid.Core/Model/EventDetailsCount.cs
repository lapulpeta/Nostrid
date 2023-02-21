using System.Collections.Concurrent;

namespace Nostrid.Model;

public class EventDetailsCount
{
    public ConcurrentDictionary<string, int> ReactionGroups = new();

    public int Reposts;

    public int Zaps;

    public void Add(EventDetailsCount delta)
    {
        Interlocked.Add(ref Reposts, delta.Reposts);
        Interlocked.Add(ref Zaps, delta.Zaps);
        foreach (var (reaction, count) in delta.ReactionGroups)
        {
            ReactionGroups.AddOrUpdate(reaction, count, (_, oldv) => oldv + count);
        }
    }
}

