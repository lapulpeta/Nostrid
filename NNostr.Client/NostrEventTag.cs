using Newtonsoft.Json;
using NNostr.Client.JsonConverters;
using System.ComponentModel.DataAnnotations.Schema;

namespace NNostr.Client
{
    [JsonConverter(typeof(NostrEventTagJsonConverter))]
    public class NostrEventTag
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public string Id { get; set; }

        public string EventId { get; set; }

        public string TagIdentifier { get; set; }

        public List<string> Data { get; set; } = new();

        [JsonIgnore]
        public NostrEvent Event { get; set; }

        public override string ToString()
        {
            var d = TagIdentifier is null ? Data : Data.Prepend(TagIdentifier);
            return JsonConvert.SerializeObject(d);
        }
    }
}