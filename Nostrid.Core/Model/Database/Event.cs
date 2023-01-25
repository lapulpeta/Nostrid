using Nostrid.Data.Relays;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nostrid.Model;

public class Event
{
    public string Id { get; set; }

    public string PublicKey { get; set; }

    public DateTime? CreatedAt { get; set; }

    public int Kind { get; set; }

    public string? Content { get; set; }

    public List<TagData> Tags { get; set; } = new();

    public string Signature { get; set; }

    public bool Deleted { get; set; }

    public long CreatedAtCurated { get; set; }

    public int Difficulty { get; set; }

    public bool HasPow { get; set; }

    public bool Broadcast { get; set; }

    public bool CanEcho { get; set; }

    private Lazy<string?> _replyToId = null;

    #region Properties (move to extension when supported by C#)
    [NotMapped]
    public string? ReplyToId
    {
        get
        {
            _replyToId ??= new(() =>
                {
                    if (Kind == NostrKind.Text)
                    {
                        var preferred = Tags
                            .Where(t => t.Data0 == "e" && t.Data3 == "reply")
                            .Select(t => t.Data1)
                            .FirstOrDefault();
                        if (preferred != null)
                        {
                            return preferred;
                        }
                        return Tags
                            .Where(t => t.Data0 == "e" && t.Data1 != null)
                            .Select(t => t.Data1)
                            .LastOrDefault();
                    }
                    return null;
                });
            return _replyToId.Value;
        }
    }

    private Lazy<string?> _rootId = null;

    [NotMapped]
    public string? ReplyToRootId
    {
        get
        {
            _rootId ??= new(() =>
            {
                if (Kind == NostrKind.Text)
                {
                    var preferred = Tags
                        .Where(t => t.Data0 == "e" && t.Data3 == "root")
                        .Select(t => t.Data1)
                        .FirstOrDefault();
                    if (preferred != null)
                    {
                        return preferred;
                    }
                    return Tags
                        .Where(t => t.Data0 == "e" && t.Data1 != null)
                        .Select(t => t.Data1)
                        .FirstOrDefault();
                }
                return null;
            });
            return _rootId.Value;
        }
    }

    private Lazy<string?> _repostEventId = null;

    [NotMapped]
    public string? RepostEventId
    {
        get
        {
            _repostEventId ??= new(() =>
            {
                if (Kind == NostrKind.Repost)
                {
                    return Tags
                        .Where(t => t.Data0 == "e" && t.Data1 != null)
                        .Select(t => t.Data1)
                        .FirstOrDefault();
                }
                return null;
            });
            return _repostEventId.Value;
        }
    }

    private Lazy<List<Mention>>? _mentions = null;

    public List<Mention> GetMentions()
    {
        _mentions ??= new(() =>
        {
            // NIP-08: https://github.com/nostr-protocol/nips/blob/master/08.md
            var mentions = new List<Mention>();
            for (int index = 0; index < Tags.Count; index++)
            {
                var tag = Tags[index];
                if (tag.DataCount > 1)
                {
                    switch (tag.Data0)
                    {
                        case "e":
                        case "p":
                            mentions.Add(new Mention() { Type = tag.Data0[0], Index = index, MentionId = tag.Data1.ToLower() });
                            break;
                    }
                }
            }
            return mentions;

        });
        return _mentions.Value;
    }
    #endregion

    //public Event()
    //{
    //}

    //public Event(NostrEvent ev)
    //{
    //    Content = ev.Content;
    //    CreatedAt = ev.CreatedAt?.UtcDateTime;
    //    Deleted = ev.Deleted;
    //    Id = ev.Id;
    //    Kind = ev.Kind;
    //    PublicKey = ev.PublicKey;
    //    Signature = ev.Signature;
    //    Tags = ev.Tags
    //        .Select((t, i) =>
    //            new TagData()
    //            {
    //                TagIndex = i,
    //                Data0 = t.TagIdentifier,
    //                DataCount = t.Data.Count + 1,
    //                Data1 = t.Data.Count > 0 ? t.Data[0] : null,
    //                Data2 = t.Data.Count > 1 ? t.Data[1] : null,
    //                Data3 = t.Data.Count > 2 ? t.Data[2] : null,
    //            })
    //        .ToList();
    //    Difficulty = CalculateDifficulty(ev.Id);
    //    HasPow = ev.Tags.Any(t => t.TagIdentifier == "nonce");
    //}

    //public NostrEvent ToNostrEvent()
    //{
    //    return new NostrEvent()
    //    {
    //        Content = this.Content,
    //        CreatedAt = this.CreatedAt,
    //        Deleted = this.Deleted,
    //        Id = this.Id,
    //        Kind = this.Kind,
    //        PublicKey = this.PublicKey,
    //        Signature = this.Signature,
    //        Tags = this.Tags
    //            .Select(t =>
    //                new NostrEventTag()
    //                {
    //                    TagIdentifier = t.Data0,
    //                    Data = new[] { t.Data1, t.Data2, t.Data3 }.Take(t.DataCount - 1).ToList(),
    //                })
    //            .ToList()
    //    };
    //}

    public override bool Equals(object obj)
    {
        return obj is Event @event &&
               Id == @event.Id;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Id);
    }

    //public bool CheckPowTarget(bool failIfTargetMissing)
    //{
    //    var nonce = Tags.FirstOrDefault(t => t.Data0 == "nonce");
    //    if (nonce == null)
    //        return false;

    //    if (nonce.DataCount < 3)
    //        return !failIfTargetMissing;

    //    if (!int.TryParse(nonce.Data2, out var target))
    //    {
    //        return !failIfTargetMissing;
    //    }

    //    return Difficulty >= target;
    //}

    //public static int CalculateDifficulty(string id)
    //{
    //    var bytes = Convert.FromHexString(id);
    //    Trace.Assert(bytes.Length == 32, "Id should be 256 bits long");

    //    int accumDiff = 0;
    //    for (int i = 0; i < 32; i += 4)
    //    {
    //        var part = ((uint)bytes[i] << 24) + ((uint)bytes[i + 1] << 16) + ((uint)bytes[i + 2] << 8) + ((uint)bytes[i + 3]);
    //        var diff = BitOperations.LeadingZeroCount(part);
    //        Trace.Assert(diff <= 32, $"Diff is {diff}");
    //        if (diff < 32)
    //            return diff + accumDiff;
    //        accumDiff += 32;
    //    }
    //    return 256; // Max
    //}

    //public static void MergeAndClear(ConcurrentDictionary<string, Event> destination, ConcurrentDictionary<string, Event> source)
    //{
    //    string id;
    //    while ((id = source.Keys.FirstOrDefault()) != null)
    //    {
    //        if (source.TryRemove(id, out var ev))
    //        {
    //            Merge(destination, ev);
    //        }
    //    }
    //}

    //public static bool Merge(ConcurrentDictionary<string, Event> destination, IEnumerable<Event> source, Func<Event, bool> copyIf = null, Action<Event> onCopy = null)
    //{
    //    var ret = false;
    //    foreach (var ev in source)
    //    {
    //        if (copyIf == null || copyIf(ev))
    //        {
    //            ret = true;
    //            Merge(destination, ev, onCopy);
    //        }
    //    }
    //    return ret;
    //}

    //public static void Merge(ConcurrentDictionary<string, Event> destination, Event ev, Action<Event> onCopy = null)
    //{
    //    destination.AddOrUpdate(
    //        ev.Id,
    //        addValueFactory: _ =>
    //        {
    //            onCopy?.Invoke(ev);
    //            return ev;
    //        },
    //        updateValueFactory: (_, old) =>
    //        {
    //            var copy = false; // !old.Processed && ev.Processed;
    //            if (copy)
    //                onCopy?.Invoke(ev);
    //            return copy ? ev : old;
    //        });
    //}
}

