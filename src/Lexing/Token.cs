using System;
using Newtonsoft.Json;

namespace Elk.Lexing;

record TextPos(int Line, int Column, string? FilePath)
{
    public static TextPos Default
        => new(1, 1, null);
}

[JsonConverter(typeof(TokenConverter))]
record Token(TokenKind Kind, string Value, TextPos Position);

class TokenConverter : JsonConverter<Token>
{
    public override Token ReadJson(JsonReader reader, Type objectType, Token? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        throw new InvalidOperationException();
    }

    public override void WriteJson(JsonWriter writer, Token? token, JsonSerializer serializer)
    {
        writer.WriteValue(token?.Value ?? "");
    }
}