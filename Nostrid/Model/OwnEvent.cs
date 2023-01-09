using NNostr.Client;

namespace Nostrid.Model;

public class OwnEvent
{
	public string Id { get; set; }

    public NostrEvent Event { get; set; }

	public List<long> SeenByRelay { get; set; } = new();

	public OwnEvent() 
	{
	}

	public OwnEvent(NostrEvent ev)
	{
		Id = ev.Id;
		Event = ev;
	}
}

