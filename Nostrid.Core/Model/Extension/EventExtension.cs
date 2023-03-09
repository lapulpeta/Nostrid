using Newtonsoft.Json.Linq;
using NNostr.Client;
using Nostrid.Misc;
using System.Diagnostics;
using System.Numerics;

namespace Nostrid.Model;

public static class EventExtension
{
	public static string? TagData(this Event ev, string tag)
	{
		return ev.Tags.FirstOrDefault(t => t.Data0 == tag)?.Data1;
	}

	public static Event ToEvent(this string json)
	{
		return JToken.Parse(json).ToObject<NostrEvent>()!.FromNostrEvent();
	}

	public static Event FromNostrEvent(this NostrEvent ev)
	{
		var now = DateTimeOffset.UtcNow;
		var ret = new Event()
		{
			Content = ev.Content,
			CreatedAt = ev.CreatedAt?.UtcDateTime,
			CreatedAtCurated = (!ev.CreatedAt.HasValue || ev.CreatedAt > now ? now : ev.CreatedAt.Value).ToUnixTimeSeconds(),
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
		ret.DmToId = GetDmToId(ret);
		ret.ReplaceableId = GetReplaceableId(ret);
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
		return GetReplyToId(ev, "a") ?? GetReplyToId(ev, "e");
	}

	private static string? GetReplyToId(Event ev, string tag)
	{
		if (ev.Kind == NostrKind.Text || ev.Kind == NostrKind.ChannelMessage)
		{
			var preferred = ev.Tags
				.Where(t => t.Data0 == tag && t.Data3 == "reply")
				.Select(t => t.Data1)
				.FirstOrDefault();
			if (preferred != null)
			{
				return preferred;
			}
			if (ev.Kind == NostrKind.ChannelMessage && ev.Tags.Count(t => t.Data0 == tag) <= 1)
			{
				return null;
			}
			return ev.Tags
				.Where(t => t.Data0 == tag && t.Data1 != null)
				.Select(t => t.Data1)
				.LastOrDefault();
		}
		else if (ev.Kind == NostrKind.DM)
		{
			return ev.Tags
				.Where(t => t.Data0 == tag && t.Data1 != null)
				.Select(t => t.Data1)
				.FirstOrDefault();
		}
		return null;
	}

	public static string? GetReplyToRootId(Event ev)
	{
		return GetReplyToRootId(ev, "a") ?? GetReplyToRootId(ev, "e");
	}

	private static string? GetReplyToRootId(Event ev, string tag)
	{
		if (ev.Kind == NostrKind.Text)
		{
			var preferred = ev.Tags
				.Where(t => t.Data0 == tag && t.Data3 == "root")
				.Select(t => t.Data1)
				.FirstOrDefault();
			if (preferred != null)
			{
				return preferred;
			}
			return ev.Tags
				.Where(t => t.Data0 == tag && t.Data1 != null)
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

	public static bool IsReplaceableId(this string id)
	{
		return id?.Contains(':') ?? false;
	}

    public static bool IsReplaceableIdStrict(this string id)
    {
		var exploded = ExplodeReplaceableId(id);
		if (exploded == null)
		{
			return false;
		}
		if (!Utils.IsValidNostrId(exploded.Value.pubkey))
		{
			return false;
		}
		return true;
    }

    public static (string pubkey, int kind, string dstr)? ExplodeReplaceableId(string? naddr)
	{
		if (naddr == null)
		{
			return null;
		}
		var parts = naddr.Split(":");
		if (parts.Length != 3 || !int.TryParse(parts[0], out int kind))
		{
			return null;
		}
		return (parts[1], kind, parts[2]);
	}

	public static string? GetReplaceableId(string pubkey, int kindd, string? dstr)
	{
		if (dstr == null)
		{
			return $"{kindd}:{pubkey}";
		}
		else
		{
			return $"{kindd}:{pubkey}:{dstr}";
		}
	}

	public static string? GetReplaceableId(Event ev)
	{
		switch (ev.KindClass)
		{
			case NostrKindClass.Replaceable:
				return GetReplaceableId(ev.PublicKey, ev.Kind, null);
			case NostrKindClass.ReplaceableParams:
				string dstr = ev.Tags.Where(t => t.Data0 == "d").Select(t => t.Data1).FirstOrDefault() ?? string.Empty;
				return GetReplaceableId(ev.PublicKey, ev.Kind, dstr);
		}
		return null;
	}

	public static string? GetDmToId(Event ev)
	{
		if (ev.Kind == NostrKind.DM)
		{
			return ev.Tags
				.Where(t => t.Data0 == "p" && t.Data1 != null)
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

	public static bool Merge(List<NoteTree> list, IEnumerable<Event> source)
	{
		bool added = false;
		foreach (var ev in source)
		{
			if (list.Exists(ev.Id))
			{
				continue;
			}

			int insertAt = list.Count;
			for (int i = 0; i < list.Count; i++)
			{
				if (list[i].Note.CreatedAtCurated < ev.CreatedAtCurated)
				{
					insertAt = i;
					break;
				}
			}
			list.Insert(insertAt, new NoteTree(ev));
			added = true;
		}
		return added;
	}
}

