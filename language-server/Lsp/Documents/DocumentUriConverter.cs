using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Elk.LanguageServer.Lsp.Documents;

public class DocumentUriConverter : JsonConverter<DocumentUri>
{
    public override DocumentUri? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType == JsonTokenType.String)
        {
            try
            {
                return DocumentUri.Parse(reader.GetString() ?? string.Empty);
            }
            catch (ArgumentException ex)
            {
                throw new SerializationException("Could not deserialize document uri", ex);
            }
        }

        throw new SerializationException("The JSON value must be a string.");
    }

    public override void Write(Utf8JsonWriter writer, DocumentUri value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}