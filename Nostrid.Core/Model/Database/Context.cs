using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace Nostrid.Model;

public class Context : DbContext
{
    public DbSet<Account> Accounts { get; set; }
    public DbSet<AccountDetails> AccountDetails { get; set; }
    public DbSet<Follow> Follows { get; set; }
    public DbSet<Relay> Relays { get; set; }
    public DbSet<Config> Configs { get; set; }
    public DbSet<Event> Events { get; set; }
    public DbSet<EventSeen> EventSeen { get; set; }
    public DbSet<FeedSource> FeedSources { get; set; }
    public DbSet<TagData> TagDatas { get; set; }
    public DbSet<Channel> Channels { get; set; }
    public DbSet<ChannelDetails> ChannelDetails { get; set; }

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
        builder.Entity<FeedSource>().Property(p => p.Hashtags)
            .HasConversion(
                v => JsonConvert.SerializeObject(v),
                v => JsonConvert.DeserializeObject<List<string>>(v));

        builder.Entity<TagData>().HasIndex(t => t.Data0);
        builder.Entity<TagData>().HasIndex(t => new { t.Data0, t.Data1 });
        builder.Entity<TagData>().HasIndex(t => new { t.Data0, t.Data1, t.Data3 });
        builder.Entity<Event>().HasIndex(e => e.Kind);
        builder.Entity<Event>().HasIndex(e => e.PublicKey);
        builder.Entity<Event>().HasIndex(e => e.Id);
        builder.Entity<Event>().HasIndex(e => e.CreatedAtCurated);
        builder.Entity<Account>().HasIndex(e => e.Id);
        builder.Entity<AccountDetails>().HasIndex(e => e.Id);
        builder.Entity<AccountDetails>().HasIndex(e => new { e.Id, e.DetailsLastReceived });
        builder.Entity<EventSeen>().HasIndex(e => new { e.EventId, e.RelayId }).IsUnique();
        builder.Entity<Follow>().HasIndex(f => f.AccountId);
        builder.Entity<Follow>().HasIndex(f => f.FollowId);
        builder.Entity<Follow>().HasIndex(f => new { f.AccountId, f.FollowId });
        builder.Entity<Channel>().HasIndex(e => e.Id);
        builder.Entity<ChannelDetails>().HasIndex(e => e.Id);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseSqlite($"Data Source={_dbfile}");
}