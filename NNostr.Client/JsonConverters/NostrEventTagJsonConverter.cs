using Newtonsoft.Json;

namespace NNostr.Client.JsonConverters
{
    public class NostrEventTagJsonConverter : JsonConverter<NostrEventTag>
    {
        public override NostrEventTag? ReadJson(JsonReader reader, Type objectType, NostrEventTag? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var result = new NostrEventTag();
            if (reader.TokenType != JsonToken.StartArray)
            {
                throw new Exception("Nostr Event Tags are an array");
            }

            reader.Read();
            var i = 0;
            while (reader.TokenType != JsonToken.EndArray)
            {
                if (i == 0)
                {
                    result.TagIdentifier = StringEscaperJsonConverter.JavaScriptStringEncode(reader.Value.ToString(), false);
                }
                else
                {
                    result.Data.Add(StringEscaperJsonConverter.JavaScriptStringEncode(reader.Value.ToString(), false));
                }

                reader.Read();
                i++;
            }

            return result;
        }

        public override void WriteJson(JsonWriter writer, NostrEventTag? value, JsonSerializer serializer)
        {
            if (value is null)
            {
                writer.WriteNull();
                return;
            }

            writer.WriteStartArray();
            writer.WriteValue(value.TagIdentifier);
            value.Data?.ForEach(writer.WriteValue);

            writer.WriteEndArray();
        }
    }
}