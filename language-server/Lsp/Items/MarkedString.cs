namespace Elk.LanguageServer.Lsp.Items;

public class MarkedString(string language, string value)
{
    public string? Language { get; set; } = language;

    public string Value { get; set; } = value;
}