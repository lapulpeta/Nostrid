using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Nostrid.Model;

public class ChannelDetails
{
    #region Json
    [JsonPropertyName("name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? Name { get; set; }

    [JsonPropertyName("about")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? About { get; set; }

    [JsonPropertyName("picture")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? PictureUrl { get; set; }
    #endregion

    [JsonIgnore]
    [ForeignKey("Channel")]
    public string Id { get; set; }

    [JsonIgnore]
    public Channel Channel { get; set; }

    [JsonIgnore]
    public DateTime DetailsLastUpdate { get; set; }
}

