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
}

