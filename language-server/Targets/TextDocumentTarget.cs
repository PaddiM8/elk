using Elk.LanguageServer.Data;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using StreamJsonRpc;

namespace Elk.LanguageServer.Targets;

class TextDocumentTarget
{
    [JsonRpcMethod("textDocument/didOpen")]
    public static void DidOpen(JToken token)
    {
        var parameters = token.ToObject<DidOpenTextDocumentParams>();
        if (parameters == null || Path.GetExtension(parameters.TextDocument.Uri.Path) != ".elk")
            return;

        var document = new SemanticDocument(
            parameters.TextDocument.Uri.Path,
            parameters.TextDocument.Text
        );
        DocumentStorage.Add(document);
    }

    [JsonRpcMethod("textDocument/didClose")]
    public static void DidClose(JToken token)
    {
        var parameters = token.ToObject<DidOpenTextDocumentParams>()!;
        DocumentStorage.Remove(parameters.TextDocument.Uri.Path);
    }

    [JsonRpcMethod("textDocument/didChange")]
    public static void DidChange(JToken token)
    {
        var parameters = token.ToObject<DidChangeTextDocumentParams>();
        if (parameters == null || Path.GetExtension(parameters.TextDocument.Uri.Path) != ".elk")
            return;

        var newText = parameters.ContentChanges.FirstOrDefault()?.Text;
        if (newText != null)
            DocumentStorage.Update(parameters.TextDocument.Uri.Path, newText);
    }

    [JsonRpcMethod("textDocument/semanticTokens/full")]
    public static SemanticTokens SemanticTokensFull(JToken token)
    {
        var parameters = token.ToObject<SemanticTokensParams>()!;
        var document = DocumentStorage.Get(parameters.TextDocument.Uri.Path);
        var semanticTokens = ElkProgram.GetSemanticInformation(document.Text, document.Module);

        return TokenBuilder.BuildSemanticTokens(semanticTokens);
    }
}
