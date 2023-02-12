using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NNostr.Client.JsonConverters;

namespace NNostr.Client
{
    public class NostrSubscriptionFilter
    {
        public NostrSubscriptionFilter(params int[] requiredNips)
        {
            RequiredNips = new(requiredNips);
        }

        [JsonIgnore] public List<int> RequiredNips { get; set; }

        [JsonProperty("ids")] public string[]? Ids { get; set; }
        [JsonProperty("authors")] public string[]? Authors { get; set; }
        [JsonProperty("kinds")] public int[]? Kinds { get; set; }
        [JsonProperty("#e")] public string[]? EventId { get; set; }
        [JsonProperty("#p")] public string[]? PublicKey { get; set; }
        [JsonProperty("since")][JsonConverter(typeof(UnixTimestampSecondsJsonConverter))] public DateTimeOffset? Since { get; set; }
        [JsonProperty("until")][JsonConverter(typeof(UnixTimestampSecondsJsonConverter))] public DateTimeOffset? Until { get; set; }
        [JsonProperty("limit")] public int? Limit { get; set; }
        [JsonProperty("search")] public string? Search { get; set; }

        [JsonExtensionData]
        public IDictionary<string, JToken>? ExtensionData { get; set; }

        public Dictionary<string, string[]> GetAdditionalTagFilters()
        {
            var tagFilters = ExtensionData?.Where(pair => pair.Key.StartsWith("#") && pair.Value.Type == JTokenType.Array);
            return tagFilters?.ToDictionary(tagFilter => tagFilter.Key.Substring(1),
                tagFilter => tagFilter.Value.Select(element => element.Value<string>())
                    .ToArray())! ?? new Dictionary<string, string[]>();
        }

    }
}