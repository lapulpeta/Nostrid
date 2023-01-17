namespace Nostrid.Model;

public class Config
{
    public int Id { get; set; }

    public bool ShowDifficulty { get; set; }

    public int MinDiffIncoming { get; set; }

    public bool StrictDiffCheck { get; set; }

    public int TargetDiffOutgoing { get; set; }

    public Config Clone()
    {
        return new Config()
        {
            Id = Id,
            ShowDifficulty = ShowDifficulty,
            MinDiffIncoming = MinDiffIncoming,
            StrictDiffCheck = StrictDiffCheck,
            TargetDiffOutgoing = TargetDiffOutgoing
        };
    }
}

