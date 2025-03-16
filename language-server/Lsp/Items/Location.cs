using Elk.LanguageServer.Lsp.Documents;

namespace Elk.LanguageServer.Lsp.Items;

public class Location
{
    public required DocumentUri Uri { get; set; }

    public required DocumentRange Range { get; set; }
}