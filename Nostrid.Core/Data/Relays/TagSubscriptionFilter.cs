using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NNostr.Client;
using Nostrid.Model;

namespace Nostrid.Data.Relays;

public class TagSubscriptionFilter : SubscriptionFilter
{
    private readonly string[] tags;

    public TagSubscriptionFilter(string tag) : this(new[] { tag })
    {
    }

    public TagSubscriptionFilter(string[] tags)
    {
        this.tags = tags;
    }

    public override NostrSubscriptionFilter[] GetFilters()
    {
        return new[] {
            new NostrSubscriptionFilter(NostrNip.NostrNipGenericTag) { ExtensionData = new Dictionary<string, JToken>(){["#t"] = ConvertStringArrayToJsonElement(tags) },
                Kinds = new[]{ NostrKind.Text }, Limit = LimitFilterData?.Limit, Since = LimitFilterData?.Since, Until = LimitFilterData?.Until },
            new NostrSubscriptionFilter(NostrNip.NostrNipSearch) { Search = string.Join(" ", tags),
                Kinds = new[]{ NostrKind.Text }, Limit = LimitFilterData?.Limit, Since = LimitFilterData?.Since, Until = LimitFilterData?.Until },
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

