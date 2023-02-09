using NNostr.Client;

namespace Nostrid.Data.Relays;

public interface IRelayFilter
{
    public NostrSubscriptionFilter[] GetFiltersForRelay(long relayId);
}

