using System.Text.Json.Nodes;
using Elk.LanguageServer.Data;
using Elk.LanguageServer.Lsp;
using Elk.LanguageServer.Lsp.Options;

namespace Elk.LanguageServer.Targets;

class RootTarget : Target
{
    public RootTarget()
    {
        RegisterMethod("initialize", Initialize);
        RegisterNotification("initialized", Initialized);
    }

    public static InitializeResult Initialize(JsonNode parameters)
    {
        return new InitializeResult
        {
            Capabilities = new ServerCapabilities
            {
                TextDocumentSync = TextDocumentSyncKind.Full,
                CompletionProvider = new CompletionOptions
                {
                    TriggerCharacters = [":"],
                },
                SemanticTokensProvider = new SemanticTokensOptions
                {
                    Legend = new SemanticTokensLegend
                    {
                        TokenTypes = TokenBuilder.GetSemanticTokenTypeLegend(),
                    },
                    Full = true,
                },
                HoverProvider = true,
                SignatureHelpProvider = new SignatureHelpOptions
                {
                    TriggerCharacters = ["(", ","],
                },
                DefinitionProvider = true,
            },
        };
    }

    public static void Initialized(JsonNode parameters)
    {
    }
}
