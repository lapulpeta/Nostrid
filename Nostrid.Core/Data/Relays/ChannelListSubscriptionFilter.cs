using Nostrid.Misc;
using NNostr.Client;
using Nostrid.Model;

namespace Nostrid.Data.Relays;

/// <summary>
/// Returns all channels
/// </summary>
public class ChannelListSubscriptionFilter : SubscriptionFilter
{
	public ChannelListSubscriptionFilter()
    {
    }

    public override NostrSubscriptionFilter[] GetFilters()
    {
        return new[] {
            new NostrSubscriptionFilter() { Kinds = new[]{ NostrKind.ChannelCreation, NostrKind.ChannelMetadata }, Limit = limitFilterData?.Limit, Since = limitFilterData?.Since, Until = limitFilterData?.Until },
        };
    }

    public override SubscriptionFilter Clone()
    {
        return new ChannelListSubscriptionFilter();
    }
}

