using Nostrid.Misc;
using NNostr.Client;
using System;
using System.Text.Json;

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
        ParamsId = Utils.HashWithSHA256("tsf:" + tags.OrderBy(x => x).Aggregate((a, b) => $"{a}:{b}"));
    }

    public override NostrSubscriptionFilter[] GetFilters()
    {
        return new[] {
            new NostrSubscriptionFilter() { ExtensionData = new Dictionary<string, JsonElement>(){["#t"] = ConvertStringArrayToJsonElement(tags) },
                Kinds = new[]{ NostrKind.Text }, Limit = limitFilterData?.Limit, Since = limitFilterData?.Since, Until = limitFilterData?.Until }
        };
    }

    private static JsonElement ConvertStringArrayToJsonElement(string[] array)
    {
        string jsonString = JsonSerializer.Serialize(array);
        return JsonDocument.Parse(jsonString).RootElement;
    }

    public override SubscriptionFilter Clone()
    {
        return new TagSubscriptionFilter(tags);
    }
}

