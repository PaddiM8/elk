#region

using System;
using System.Text;
using Elk.ReadLine.Render.Formatting;
using Newtonsoft.Json;

#endregion

namespace Elk.Lexing;

public record TextPos(int Line, int Column, int Index, string? FilePath)
{
    public static TextPos Default
        => new(1, 1, 0, null);

    public override string ToString()
    {
        var builder = new StringBuilder();
        if (FilePath != null)
        {
            builder.Append(FormatPath(FilePath));
            builder.Append(' ');
        }

        builder.Append('(');
        builder.Append(Ansi.Format($"{Line}:{Column}", AnsiForeground.DarkYellow));
        builder.Append(')');

        return builder.ToString();
    }

    private string FormatPath(string path)
    {
        var homePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (path.StartsWith(homePath))
            path = "~" + path[homePath.Length..];

        return Ansi.Format(path, AnsiForeground.DarkGray);
    }
}

[JsonConverter(typeof(TokenConverter))]
public record Token(TokenKind Kind, string Value, TextPos Position)
{
    public TextPos EndPosition
        => Position with
        {
            Column = Position.Column + Value.Length,
            Index = Position.Index + Value.Length,
        };
}

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