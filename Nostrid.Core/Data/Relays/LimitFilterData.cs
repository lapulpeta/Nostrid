namespace Nostrid.Data.Relays;

public class LimitFilterData
{
	public int? Limit { get; set; }
	public DateTimeOffset? Since { get; set; }
	public DateTimeOffset? Until { get; set; }
}

