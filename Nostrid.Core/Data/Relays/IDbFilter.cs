using Nostrid.Model;

namespace Nostrid.Data.Relays;

public interface IDbFilter
{
    public IQueryable<Event> ApplyDbFilter(IQueryable<Event> events);
}

