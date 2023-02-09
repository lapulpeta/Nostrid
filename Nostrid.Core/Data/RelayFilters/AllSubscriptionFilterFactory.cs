namespace Nostrid.Data.Relays;

public class AllSubscriptionFilterFactory
{
    private EventDatabase eventDatabase;

    public AllSubscriptionFilterFactory(EventDatabase eventDatabase)
    {
        this.eventDatabase = eventDatabase;
    }

    public AllSubscriptionFilter Create()
    {
        return new AllSubscriptionFilter(eventDatabase);
    }
}

