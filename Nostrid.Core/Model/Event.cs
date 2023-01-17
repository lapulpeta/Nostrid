using LiteDB;
using NNostr.Client;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;

namespace Nostrid.Model;

public class Event : NostrEvent
{
    public bool Processed { get; set; }

    public DateTimeOffset CreatedAtCurated { get; set; }

    public NoteMetadata NoteMetadata { get; set; } = new();

    public int Difficulty { get; set; }

    public bool HasPow { get; set; }

    public Event()
    {
    }

    public Event(NostrEvent ev)
    {
        Content = ev.Content;
        CreatedAt = ev.CreatedAt;
        Deleted = ev.Deleted;
        Id = ev.Id;
        Kind = ev.Kind;
        PublicKey = ev.PublicKey;
        Signature = ev.Signature;
        Tags = ev.Tags;

        Difficulty = CalculateDifficulty(Id);
        HasPow = Tags.Any(t => t.TagIdentifier == "nonce");
    }

    public override bool Equals(object obj)
    {
        return obj is Event @event &&
               Id == @event.Id;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Id);
    }

    public bool CheckPowTarget(bool failIfTargetMissing)
    {
        var nonce = Tags.FirstOrDefault(t => t.TagIdentifier == "nonce");
        if (nonce == null)
            return false;

        if (nonce.Data.Count < 2)
            return !failIfTargetMissing;

        if (!int.TryParse(nonce.Data[1], out var target))
        {
            return !failIfTargetMissing;
        }

        return Difficulty >= target;
    }

    public static int CalculateDifficulty(string id)
    {
        var bytes = Convert.FromHexString(id);
        Trace.Assert(bytes.Length == 32, "Id should be 256 bits long");

        int accumDiff = 0;
        for (int i = 0; i < 32; i += 4)
        {
            var part = ((uint)bytes[i] << 24) + ((uint)bytes[i + 1] << 16) + ((uint)bytes[i + 2] << 8) + ((uint)bytes[i + 3]);
            var diff = BitOperations.LeadingZeroCount(part);
            Trace.Assert(diff <= 32, $"Diff is {diff}");
            if (diff < 32)
                return diff + accumDiff;
            accumDiff += 32;
        }
        return 256; // Max
    }

    public static void MergeAndClear(ConcurrentDictionary<string, Event> destination, ConcurrentDictionary<string, Event> source)
    {
        string id;
        while ((id = source.Keys.FirstOrDefault()) != null)
        {
            if (source.TryRemove(id, out var ev))
            {
                Merge(destination, ev);
            }
        }
    }

    public static bool Merge(ConcurrentDictionary<string, Event> destination, IEnumerable<Event> source, Func<Event, bool> copyIf = null, Action<Event> onCopy = null)
    {
        var ret = false;
        foreach (var ev in source)
        {
            if (copyIf == null || copyIf(ev))
            {
                ret = true;
                Merge(destination, ev, onCopy);
            }
        }
        return ret;
    }

    public static void Merge(ConcurrentDictionary<string, Event> destination, Event ev, Action<Event> onCopy = null)
    {
        destination.AddOrUpdate(
            ev.Id,
            addValueFactory: _ =>
            {
                onCopy?.Invoke(ev);
                return ev;
            },
            updateValueFactory: (_, old) =>
            {
                var copy = !old.Processed && ev.Processed;
                if (copy)
                    onCopy?.Invoke(ev);
                return copy ? ev : old;
            });
    }
}

