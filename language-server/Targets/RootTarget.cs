using System.Text.Json.Nodes;
using Elk.LanguageServer.Data;
using Elk.LanguageServer.Lsp;
using Elk.LanguageServer.Lsp.Options;

namespace Elk.LanguageServer.Targets;

class RootTarget : Target
{
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly ILogger _logger;

    public RootTarget(CancellationTokenSource cancellationTokenSource, ILogger logger)
    {
        _cancellationTokenSource = cancellationTokenSource;
        _logger = logger;
        RegisterMethod("initialize", Initialize);
        RegisterMethod("shutdown", Shutdown);
        RegisterNotification("initialized", Initialized);
        RegisterNotification("exit", Exit);
    }

    public InitializeResult Initialize(JsonNode parameters)
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

    public void Initialized(JsonNode parameters)
    {
    }

    public object? Shutdown(JsonNode parameters)
    {
        _logger.LogInfo("Shutdown request received");

        return null;
    }

    public void Exit(JsonNode parameters)
    {
        _logger.LogInfo("Exit notification received");
        _cancellationTokenSource.Cancel();
    }
}
