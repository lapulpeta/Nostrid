namespace Nostrid.Model;

public class Relay
{
    public long Id { get; set; }

    public string Uri { get; set; }

    public int Priority { get; set; }

    public override bool Equals(object obj)
    {
        return obj is Relay relay &&
               Id == relay.Id &&
               Uri == relay.Uri;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Id, Uri);
    }
}

