using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace Nostrid.Model;

public class Context : DbContext
{
    public DbSet<Account> Accounts { get; set; }
    public DbSet<AccountDetails> AccountDetails { get; set; }
    public DbSet<Relay> Relays { get; set; }
    public DbSet<Config> Configs { get; set; }
    public DbSet<Event> Events { get; set; }
    public DbSet<EventSeen> EventSeen { get; set; }
    public DbSet<FeedSource> FeedSources { get; set; }
    public DbSet<TagData> TagDatas { get; set; }

    public string _dbfile { get; }

    public Context()
    {
    }

    public Context(string dbfile)
    {
        _dbfile = dbfile;
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<Account>().Property(p => p.FollowList)
            .HasConversion(
                v => JsonConvert.SerializeObject(v),
                v => JsonConvert.DeserializeObject<List<string>>(v));
        builder.Entity<Account>().Property(p => p.FollowerList)
            .HasConversion(
                v => JsonConvert.SerializeObject(v),
                v => JsonConvert.DeserializeObject<List<string>>(v));

        builder.Entity<FeedSource>().Property(p => p.Hashtags)
            .HasConversion(
                v => JsonConvert.SerializeObject(v),
                v => JsonConvert.DeserializeObject<List<string>>(v));

        builder.Entity<TagData>().HasIndex(t => t.Data0);
        builder.Entity<TagData>().HasIndex(t => new { t.Data0, t.Data1 });
        builder.Entity<Event>().HasIndex(e => e.Kind);
        builder.Entity<Event>().HasIndex(e => e.PublicKey);
        builder.Entity<Event>().HasIndex(e => e.Id);
        builder.Entity<Event>().HasIndex(e => e.CreatedAtCurated);
        builder.Entity<Account>().HasIndex(e => e.Id);
        builder.Entity<AccountDetails>().HasIndex(e => e.Id);
        builder.Entity<EventSeen>().HasIndex(e => new { e.EventId, e.RelayId }).IsUnique();
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseSqlite($"Data Source={_dbfile}");
}