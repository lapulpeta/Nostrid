using Microsoft.EntityFrameworkCore;
using Nostrid.Model;

namespace Nostrid.Data;

/// <summary>
/// Handles direct messages as per NIP-04 https://github.com/nostr-protocol/nips/blob/master/04.md
/// </summary>
public class DmService
{
    private readonly EventDatabase eventDatabase;

    public event EventHandler<(string accountL, string accountH)>? NewDmPair;
    public event EventHandler<(string senderId, string receiverId)>? NewDm;


    public DmService(EventDatabase eventDatabase)
    {
        this.eventDatabase = eventDatabase;
    }

    public void HandleDm(Event eventToProcess)
    {
        var senderId = eventToProcess.PublicKey;
        var receiverId = eventToProcess.Tags.FirstOrDefault(t => t.Data0 == "p")?.Data1;
        if (receiverId.IsNotNullOrEmpty())
        {
            using var db = eventDatabase.CreateContext();
            var (accountL, accountH) = senderId.CompareTo(receiverId) < 0 ? (senderId, receiverId) : (receiverId, senderId);
            var updates = db.DmPairs
                .Upsert(new DmPair() { AccountL = accountL, AccountH = accountH })
                .On(p => new { p.AccountL, p.AccountH })
                .NoUpdate()
                .Run();
            if (updates > 0)
            {
                NewDmPair?.Invoke(this, (accountL, accountH));
            }
            NewDm?.Invoke(this, (senderId, receiverId));
        }
    }

    public List<string> GetDmPartners(string accountId)
    {
        using var db = eventDatabase.CreateContext();
        return
            db.DmPairs.Where(p => p.AccountL == accountId).Select(p => p.AccountH).Union(
            db.DmPairs.Where(p => p.AccountH == accountId).Select(p => p.AccountL))
            .Distinct()
            .ToList();
    }

    public int GetUnreadCount(string accountId, string otherAccountId)
    {
        using var db = eventDatabase.CreateContext();
        var accountIsL = accountId.CompareTo(otherAccountId) < 0;

        DateTime lastRead;
        if (accountIsL)
        {
            lastRead = db.DmPairs.Where(p => p.AccountL == accountId && p.AccountH == otherAccountId).Select(p => p.LastReadL).FirstOrDefault();
        }
        else
        {
            lastRead = db.DmPairs.Where(p => p.AccountH == accountId && p.AccountL == otherAccountId).Select(p => p.LastReadH).FirstOrDefault();
        }
        return db.Events.Count(e => e.Kind == NostrKind.DM && e.CreatedAt > lastRead &&
            ((e.PublicKey == accountId && e.Tags.Any(t => t.Data1 == otherAccountId)) ||
             (e.PublicKey == otherAccountId && e.Tags.Any(t => t.Data1 == accountId))));
    }

    public void SetLastRead(string accountId, string otherAccountId)
    {
        using var db = eventDatabase.CreateContext();
        var accountIsL = accountId.CompareTo(otherAccountId) < 0;

        var now = DateTime.UtcNow;
        if (accountIsL)
        {
            db.DmPairs
                .Where(p => p.AccountL == accountId && p.AccountH == otherAccountId)
                .ExecuteUpdate(p => p.SetProperty(p => p.LastReadL, now));
        }
        else
        {
            db.DmPairs
                .Where(p => p.AccountH == accountId && p.AccountL == otherAccountId)
                .ExecuteUpdate(p => p.SetProperty(p => p.LastReadH, now));
        }
    }
}

