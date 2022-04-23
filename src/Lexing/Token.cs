using System;
using System.Linq;
using Newtonsoft.Json;

namespace Elk.Lexing;

internal record struct TextPos(int Line, int Column);

[JsonConverter(typeof(TokenConverter))]
internal record Token(TokenKind Kind, string Value, TextPos Position);

class TokenConverter : JsonConverter<Token>
{
    public override Token? ReadJson(JsonReader reader, Type objectType, Token? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        throw new NotImplementedException();
    }

    public override void WriteJson(JsonWriter writer, Token? token, JsonSerializer serializer)
    {
        writer.WriteValue(token?.Value ?? "");
    }
}