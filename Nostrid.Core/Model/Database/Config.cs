namespace Nostrid.Model;

public class Config
{
    public long Id { get; set; }

    public bool ShowDifficulty { get; set; }

    public int MinDiffIncoming { get; set; }

    public bool StrictDiffCheck { get; set; }

    public int TargetDiffOutgoing { get; set; }
    
    public string? MainAccountId { get; set; } 
    
    public string? Theme { get; set; }

    public bool ManualRelayManagement { get; set; }

    public string? ProxyUri { get; set; }

    public string? ProxyUser { get; set; }

    public string? ProxyPassword { get; set; }
}

