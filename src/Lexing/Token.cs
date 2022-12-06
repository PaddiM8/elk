#region

using System;
using Newtonsoft.Json;

#endregion

namespace Elk.Lexing;

public record TextPos(int Line, int Column, int Index, string? FilePath)
{
    public static TextPos Default
        => new(1, 1, 0, null);

    public override string ToString()
        => FilePath == null
            ? $"[{Line}:{Column}]"
            : $"{FilePath} [{Line}:{Column}]";
}

[JsonConverter(typeof(TokenConverter))]
public record Token(TokenKind Kind, string Value, TextPos Position);

class TokenConverter : JsonConverter<Token>
{
    public override Token ReadJson(
        JsonReader reader,
        Type objectType,
        Token? existingValue,
        bool hasExistingValue,
        JsonSerializer serializer)
    {
        throw new InvalidOperationException();
    }

    public override void WriteJson(JsonWriter writer, Token? token, JsonSerializer serializer)
    {
        writer.WriteValue(token?.Value ?? "");
    }
}