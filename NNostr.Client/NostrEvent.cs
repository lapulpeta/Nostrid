using Newtonsoft.Json;
using NNostr.Client.JsonConverters;

namespace NNostr.Client
{
    public class NostrEvent: IEqualityComparer<NostrEvent>
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("pubkey")]
        public string PublicKey { get; set; }

        [JsonProperty("created_at")]
        [JsonConverter(typeof(UnixTimestampSecondsJsonConverter))]
        public DateTimeOffset? CreatedAt { get; set; }

        [JsonProperty("kind")]
        public int Kind { get; set; }

        [JsonProperty("content")]
        public string Content { get; set; }
        
        [JsonProperty("tags")]
        public List<NostrEventTag> Tags { get; set; }
        
        [JsonProperty("sig")]
        public string Signature { get; set; }
        
        [JsonIgnore]
        public bool Deleted { get; set; }

        public bool Equals(NostrEvent? x, NostrEvent? y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x?.GetType() != y?.GetType()) return false;
            return x?.Id == y?.Id;
        }

        public int GetHashCode(NostrEvent obj)
        {
            return obj.Id.GetHashCode();
        }
    }
}