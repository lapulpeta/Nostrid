using Newtonsoft.Json.Linq;
using NNostr.Client;
using Nostrid.Model;

namespace Nostrid.Data.Relays;

public class ReplaceableEventSubscriptionFilter : SubscriptionFilter
{
	private readonly string[] rids;

	public ReplaceableEventSubscriptionFilter(string rid) : this(new[] { rid })
	{
	}

	public ReplaceableEventSubscriptionFilter(string[] rids)
	{
		this.rids = rids;
	}

	public override NostrSubscriptionFilter[] GetFilters()
	{
		return rids
			.Select(EventExtension.ExplodeReplaceableId)
			.Where(naddr => naddr.HasValue)
			.Select(naddr =>
				new NostrSubscriptionFilter(NostrNip.NostrNipGenericTag)
				{
					Kinds = new[] { naddr!.Value.kind },
					Authors = new[] { naddr!.Value.pubkey },
					ExtensionData = new Dictionary<string, JToken>() { ["#d"] = ConvertStringArrayToJsonElement(naddr!.Value.dstr) }
				})
			.ToArray();
	}

	public override SubscriptionFilter Clone()
	{
		return new ReplaceableEventSubscriptionFilter(rids);
	}
}

