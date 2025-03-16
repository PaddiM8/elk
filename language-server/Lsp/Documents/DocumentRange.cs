namespace Elk.LanguageServer.Lsp.Documents;

public class DocumentRange
{
    public required Position Start { get; set; }

    public required Position End { get; set; }
}

