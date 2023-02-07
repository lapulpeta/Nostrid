using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NNostr.Client;
using Nostrid.Misc;
using Nostrid.Model;

namespace Nostrid.Data.Relays;

public class TagSubscriptionFilter : SubscriptionFilter
{
    private readonly string[] tags;

    public TagSubscriptionFilter(string tag) : this(new[] { tag })
    {
    }

    public TagSubscriptionFilter(string[] tags) : base(12)
    {
        this.tags = tags;
    }

    public override NostrSubscriptionFilter[] GetFilters()
    {
        return new[] {
            new NostrSubscriptionFilter() { ExtensionData = new Dictionary<string, JToken>(){["#t"] = ConvertStringArrayToJsonElement(tags) },
                Kinds = new[]{ NostrKind.Text }, Limit = limitFilterData?.Limit, Since = limitFilterData?.Since, Until = limitFilterData?.Until }
        };
    }

    private static JToken ConvertStringArrayToJsonElement(string[] array)
    {
        string jsonString = JsonConvert.SerializeObject(array);
        return JToken.Parse(jsonString).Root;
    }

    public override SubscriptionFilter Clone()
    {
        return new TagSubscriptionFilter(tags);
    }
}

