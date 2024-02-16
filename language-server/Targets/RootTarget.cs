using Elk.LanguageServer.Data;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using StreamJsonRpc;

namespace Elk.LanguageServer.Targets;

class RootTarget
{
    [JsonRpcMethod("initialize")]
    public static InitializeResult Initialize(JToken token)
    {
        return new InitializeResult
        {
            Capabilities = new ServerCapabilities
            {
                PositionEncoding = PositionEncodingKind.UTF16,
                TextDocumentSync = new TextDocumentSync(TextDocumentSyncKind.Full),
                CompletionProvider = new CompletionRegistrationOptions.StaticOptions
                {
                    TriggerCharacters = new Container<string>(":"),
                },
                SemanticTokensProvider = new SemanticTokensRegistrationOptions.StaticOptions
                {
                    Legend = new SemanticTokensLegend
                    {
                        TokenTypes = TokenBuilder.SemanticTokenTypeLegend,
                    },
                    Full = true,
                },
                HoverProvider = new HoverRegistrationOptions.StaticOptions(),
            },
        };
    }
}
