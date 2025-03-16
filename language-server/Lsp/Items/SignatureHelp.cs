namespace Elk.LanguageServer.Lsp.Items;

public class SignatureHelp
{
    public required IEnumerable<SignatureInformation> Signatures { get; set; }
}

public class SignatureInformation
{
    public required string Label { get; set; }

    public MarkupContent? Documentation { get; set; }
}