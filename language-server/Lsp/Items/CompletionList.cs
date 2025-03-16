namespace Elk.LanguageServer.Lsp.Items;

public class CompletionList(IEnumerable<CompletionItem> items)
{
    public IEnumerable<CompletionItem> Items { get; set; } = items;

    public CompletionList()
        : this([])
    {
    }
}