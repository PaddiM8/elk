namespace Elk.LanguageServer.Lsp.Items;

public class CompletionItem
{
    public required string Label { get; set; }

    public CompletionItemKind? Kind { get; set; }

    public string? Documentation { get; set; }
}