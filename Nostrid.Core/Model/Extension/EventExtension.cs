using NNostr.Client;
using Nostrid.Data.Relays;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;

namespace Nostrid.Model;

public static class EventExtension
{
    public static Event FromNostrEvent(this NostrEvent ev)
    {
        var ret = new Event()
        {
            Content = ev.Content,
            CreatedAt = ev.CreatedAt?.UtcDateTime,
            Deleted = ev.Deleted,
            Id = ev.Id,
            Kind = ev.Kind,
            PublicKey = ev.PublicKey,
            Signature = ev.Signature,
            Tags = ev.Tags
            .Select((t, i) =>
                new TagData()
                {
                    TagIndex = i,
                    Data0 = t.TagIdentifier,
                    DataCount = t.Data.Count + 1,
                    Data1 = t.Data.Count > 0 ? t.Data[0] : null,
                    Data2 = t.Data.Count > 1 ? t.Data[1] : null,
                    Data3 = t.Data.Count > 2 ? t.Data[2] : null,
                })
            .ToList(),
            Difficulty = CalculateDifficulty(ev.Id),
            HasPow = ev.Tags.Any(t => t.TagIdentifier == "nonce"),
        };
        ret.ReplyToId = GetReplyToId(ret);
        ret.ReplyToRootId = GetReplyToRootId(ret);
        ret.ChannelId = GetChannelId(ret);
        ret.RepostEventId = GetRepostEventId(ret);
        return ret;
    }

    public static NostrEvent ToNostrEvent(this Event ev)
    {
        return new NostrEvent()
        {
            Content = ev.Content,
            CreatedAt = ev.CreatedAt,
            Deleted = ev.Deleted,
            Id = ev.Id,
            Kind = ev.Kind,
            PublicKey = ev.PublicKey,
            Signature = ev.Signature,
            Tags = ev.Tags
                .Select(t =>
                    new NostrEventTag()
                    {
                        TagIdentifier = t.Data0,
                        Data = new[] { t.Data1, t.Data2, t.Data3 }.Take(t.DataCount - 1).ToList(),
                    })
                .ToList()
        };
    }


    public static bool CheckPowTarget(this Event ev, bool failIfTargetMissing)
    {
        var nonce = ev.Tags.FirstOrDefault(t => t.Data0 == "nonce");
        if (nonce == null)
            return false;

        if (nonce.DataCount < 3)
            return !failIfTargetMissing;

        if (!int.TryParse(nonce.Data2, out var target))
        {
            return !failIfTargetMissing;
        }

        return ev.Difficulty >= target;
    }

    public static string? GetReplyToId(Event ev)
    {
        if (ev.Kind == NostrKind.Text || ev.Kind == NostrKind.ChannelMessage)
        {
            var preferred = ev.Tags
                .Where(t => t.Data0 == "e" && t.Data3 == "reply")
                .Select(t => t.Data1)
                .FirstOrDefault();
            if (preferred != null)
            {
                return preferred;
            }
            if (ev.Kind == NostrKind.ChannelMessage && ev.Tags.Count(t => t.Data0 == "e") <= 1)
            {
                return null;
            }
            return ev.Tags
                .Where(t => t.Data0 == "e" && t.Data1 != null)
                .Select(t => t.Data1)
                .LastOrDefault();
        }
        return null;
    }

    public static string? GetReplyToRootId(Event ev)
    {
        if (ev.Kind == NostrKind.Text)
        {
            var preferred = ev.Tags
                .Where(t => t.Data0 == "e" && t.Data3 == "root")
                .Select(t => t.Data1)
                .FirstOrDefault();
            if (preferred != null)
            {
                return preferred;
            }
            return ev.Tags
                .Where(t => t.Data0 == "e" && t.Data1 != null)
                .Select(t => t.Data1)
                .FirstOrDefault();
        }
        return null;
    }

    public static string? GetRepostEventId(Event ev)
    {
        if (ev.Kind == NostrKind.Repost)
        {
            return ev.Tags
                .Where(t => t.Data0 == "e" && t.Data1 != null)
                .Select(t => t.Data1)
                .FirstOrDefault();
        }
        return null;
    }

    public static string? GetChannelId(Event ev)
    {
        if (ev.Kind == NostrKind.ChannelMessage)
        {
            var preferred = ev.Tags
                .Where(t => t.Data0 == "e" && t.Data3 == "root")
                .Select(t => t.Data1)
                .FirstOrDefault();
            if (preferred != null)
            {
                return preferred;
            }
            return ev.Tags
                .Where(t => t.Data0 == "e" && t.Data1 != null)
                .Select(t => t.Data1)
                .FirstOrDefault();
        }
        return null;
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
                var copy = false; // !old.Processed && ev.Processed;
                if (copy)
                    onCopy?.Invoke(ev);
                return copy ? ev : old;
            });
    }
}

