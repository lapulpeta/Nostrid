using Newtonsoft.Json;

namespace Nostrid.Model
{
    public class Nip11Response
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("pubkey")]
        public string Pubkey { get; set; }

        [JsonProperty("contact")]
        public string Contact { get; set; }

        [JsonProperty("supported_nips")]
        public List<int> SupportedNips { get; set; }

        [JsonProperty("software")]
        public string Software { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }
    }
}