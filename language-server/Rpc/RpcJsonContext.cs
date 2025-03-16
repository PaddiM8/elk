using System.Text.Json.Serialization;
using Elk.LanguageServer.Lsp;
using Elk.LanguageServer.Lsp.Events;
using Elk.LanguageServer.Lsp.Items;
using Elk.LanguageServer.Lsp.Params;

namespace Elk.LanguageServer.Rpc;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(JsonRpcRequest))]
[JsonSerializable(typeof(JsonRpcResponse))]
[JsonSerializable(typeof(InitializeResult))]
[JsonSerializable(typeof(CompletionParams))]
[JsonSerializable(typeof(DefinitionParams))]
[JsonSerializable(typeof(DidChangeTextDocumentParams))]
[JsonSerializable(typeof(DidOpenTextDocumentParams))]
[JsonSerializable(typeof(HoverParams))]
[JsonSerializable(typeof(PublishDiagnosticsParams))]
[JsonSerializable(typeof(SemanticTokensParams))]
[JsonSerializable(typeof(SignatureHelpParams))]
[JsonSerializable(typeof(Diagnostic))]
[JsonSerializable(typeof(CompletionList))]
[JsonSerializable(typeof(Hover))]
[JsonSerializable(typeof(Location))]
[JsonSerializable(typeof(SemanticTokens))]
[JsonSerializable(typeof(SignatureHelp))]
[JsonSerializable(typeof(TextDocumentContentChangeEvent))]
public partial class RpcJsonContext : JsonSerializerContext
{
}