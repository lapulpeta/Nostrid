using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Nostrid.Model;

public class AccountDetails
{
    [JsonIgnore]
    [ForeignKey("Account")]
    public string Id { get; set; }

    [JsonIgnore]
    public Account Account { get; set; }

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

    [JsonPropertyName("nip05")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? Nip05Id { get; set; }

    [JsonPropertyName("lud16")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? Lud16Id { get; set; }

    [JsonPropertyName("lud06")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? Lud06Url { get; set; }
    #endregion

    [JsonIgnore]
    public bool Nip05Valid { get; set; }

    [JsonIgnore]
    public DateTime DetailsLastUpdate { get; set; }
}

