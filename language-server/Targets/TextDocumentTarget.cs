using System.Text;
using Elk.LanguageServer.Data;
using Elk.Std.Bindings;
using Elk.Vm;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using StreamJsonRpc;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Elk.LanguageServer.Targets;

class TextDocumentTarget(JsonRpc rpc)
{
    [JsonRpcMethod("textDocument/didOpen")]
    public void DidOpen(JToken token)
    {
        var parameters = token.ToObject<DidOpenTextDocumentParams>();
        if (parameters == null)
            return;

        var document = new SemanticDocument(
            parameters.TextDocument.Uri.Path,
            parameters.TextDocument.Text
        );
        document.RefreshSemantics();
        DocumentStorage.Add(document);
    }

    [JsonRpcMethod("textDocument/didClose")]
    public void DidClose(JToken token)
    {
        var parameters = token.ToObject<DidOpenTextDocumentParams>()!;
        DocumentStorage.Remove(parameters.TextDocument.Uri.Path);
    }

    [JsonRpcMethod("textDocument/didChange")]
    public async Task DidChangeAsync(JToken token)
    {
        var parameters = token.ToObject<DidChangeTextDocumentParams>();
        if (parameters == null)
            return;

        var newText = parameters.ContentChanges.FirstOrDefault()?.Text;
        if (newText == null)
            return;

        var document = DocumentStorage.Get(parameters.TextDocument.Uri.Path);
        document.Text = newText;
        document.RefreshSemantics();

        await rpc.NotifyWithParameterObjectAsync(
            "textDocument/publishDiagnostics",
            new PublishDiagnosticsParams
            {
                Uri = parameters.TextDocument.Uri,
                Diagnostics = new Container<Diagnostic>(document.Diagnostics),
            }
        );
    }

    [JsonRpcMethod("textDocument/completion")]
    public CompletionList Completion(JToken token)
    {
        var parameters = token.ToObject<CompletionParams>()!;
        var document = DocumentStorage.Get(parameters.TextDocument.Uri.Path);
        if (document.Ast == null)
            return new CompletionList();

        var lineContent = document.GetLineAtCaret(parameters.Position.Line, parameters.Position.Character);
        if (lineContent == null)
            return new CompletionList();

        var fullIdentifierChars = lineContent
            .Reverse()
            .TakeWhile(x => char.IsLetterOrDigit(x) || x is '_' or '-' or '$' or ':')
            .Reverse()
            .ToArray();
        if (fullIdentifierChars.Length == 0)
            return new CompletionList();

        var modulePath = new string(fullIdentifierChars).Split("::");
        if (modulePath.Length == 0)
            return new CompletionList();

        var expr = document.Ast.FindExpressionAt(
            parameters.Position.Line + 1,
            parameters.Position.Character + 1
        );
        var scope = modulePath.Length > 1
            ? expr?.Scope.ModuleScope.FindModule(modulePath.SkipLast(1), lookInImports: true)
            : expr?.Scope;

        var query = modulePath.Last();
        var stdTypes = StdBindings.Types
            .Where(x => x.Contains(query))
            .Select(x => new CompletionItem
            {
                Label = x,
                Kind = CompletionItemKind.Struct,
            });
        var modulePathString = string.Join("::", modulePath);
        var stdModules = StdBindings.Modules
            .Where(x => x.Contains(modulePathString))
            .Select(x => new CompletionItem
            {
                Label = x.Split("::")[^1],
                Kind = CompletionItemKind.Module,
            });
        // If it ends with just a single colon, don't show any completions
        // yet. If it would have had two colons, it would have split
        // successfully earlier and not ended up with a trailing colon like
        // this.
        if (modulePath.LastOrDefault()?.EndsWith(':') is true)
            stdModules = Array.Empty<CompletionItem>();

        var modulePathWithoutLast = string.Join("::", modulePath.SkipLast(1));
        var stdFunctions = StdBindings.Functions
            .Where(x => x.ModuleName == modulePathWithoutLast)
            .Where(x => x.Name.Contains(query))
            .Select(x => new CompletionItem
            {
                Label = x.Name,
                Documentation = x.Documentation,
                Kind = CompletionItemKind.Function,
            });

        if (scope == null && modulePath.Length == 1)
            scope = document.Module;

        IEnumerable<CompletionItem> completions = Array.Empty<CompletionItem>();
        if (scope != null)
        {
            completions = scope
                .Query(query, includePrivate: modulePath.Length == 1)
                .Select(CompletionBuilder.FromSymbol);
        }

        if (modulePath.Length == 1)
            completions = completions.Concat(stdTypes);

        completions = completions
            .Concat(stdFunctions)
            .Concat(stdModules);

        return new CompletionList(completions);
    }

    [JsonRpcMethod("textDocument/semanticTokens/full")]
    public SemanticTokens SemanticTokensFull(JToken token)
    {
        var parameters = token.ToObject<SemanticTokensParams>()!;

        return DocumentStorage.Get(parameters.TextDocument.Uri.Path).SemanticTokens;
    }

    [JsonRpcMethod("textDocument/hover")]
    public Hover Hover(JToken token)
    {
        var parameters = token.ToObject<HoverParams>()!;
        var document = DocumentStorage.Get(parameters.TextDocument.Uri.Path);
        if (document.Ast == null)
            return new Hover();

        var hoverInfo = SymbolInformationProvider.GetInfo(
            document,
            parameters.Position.Line,
            parameters.Position.Character
        );
        if (hoverInfo == null)
            return new Hover();

        return new Hover
        {
            Contents = new MarkedStringsOrMarkupContent(hoverInfo),
        };
    }

    [JsonRpcMethod("textDocument/signatureHelp")]
    public SignatureHelp SignatureHelp(JToken token)
    {
        var parameters = token.ToObject<SignatureHelpParams>()!;
        var document = DocumentStorage.Get(parameters.TextDocument.Uri.Path);

        var lineContent = document.GetLineAtCaret(parameters.Position.Line, parameters.Position.Character);
        if (lineContent == null || document.Ast == null)
            return new SignatureHelp();

        bool IsStartIdentifierChar(char c)
            => char.IsLetterOrDigit(c) || c == '_';

        StringBuilder? identifierBuilder = null;
        for (var i = lineContent.Length - 1; i > 1; i--)
        {
            var next = lineContent[i - 1];
            if (lineContent[i] == '(' && IsStartIdentifierChar(next))
            {
                identifierBuilder = new StringBuilder();
                continue;
            }

            if (identifierBuilder == null)
                continue;

            var current = lineContent[i];
            if (!IsStartIdentifierChar(current) && current != ':')
                break;

            identifierBuilder.Insert(0, current);
        }

        if (identifierBuilder == null || identifierBuilder.Length == 0)
            return new SignatureHelp();

        var modulePath = identifierBuilder.ToString().Split("::").ToList();
        var identifier = modulePath.Last();
        modulePath.RemoveAt(modulePath.Count - 1);

        var expr = document.Ast.FindExpressionAt(
            parameters.Position.Line + 1,
            parameters.Position.Character + 1
        );
        var scope = modulePath.Count > 0
            ? expr?.Scope.ModuleScope.FindModule(modulePath, lookInImports: true)
            : expr?.Scope;
        scope ??= document.Module;

        MarkedString documentation;
        var functionSymbol = scope.ModuleScope.FindFunction(identifier, lookInImports: false);
        if (functionSymbol != null)
        {
            documentation = DocumentationBuilder.BuildSignature(
                identifier,
                functionSymbol.Expr.Parameters.Select(x => x.Identifier.Value),
                null,
                functionSymbol.Expr.HasClosure
            );
        }
        else
        {
            var stdFunction = StdBindings.GetFunction(identifier, modulePath);

            if (stdFunction == null)
                return new SignatureHelp();

            documentation = DocumentationBuilder.BuildSignature(
                identifier,
                stdFunction.Parameters
                    .Where(x => x.Type != typeof(ShellEnvironment))
                    .Select(x => x.Name),
                stdFunction.Documentation,
                stdFunction.HasClosure
            );
        }

        var signature = new SignatureInformation
        {
            Label = identifier,
            Documentation = new StringOrMarkupContent(
                new MarkupContent
                {
                    Kind = MarkupKind.Markdown,
                    Value = $"```elk\n{documentation.Value}\n```",
                }
            ),
        };

        return new SignatureHelp
        {
            Signatures = new Container<SignatureInformation>(signature)
        };
    }

    [JsonRpcMethod("textDocument/definition")]
    public Location? Definition(JToken token)
    {
        var parameters = token.ToObject<DefinitionParams>()!;
        var document = DocumentStorage.Get(parameters.TextDocument.Uri.Path);
        if (document.Ast == null)
            return null;

        var position = SymbolInformationProvider.GetDefinition(
            document,
            parameters.Position.Line,
            parameters.Position.Character
        );
        if (position?.FilePath == null)
            return null;

        return new Location
        {
            Uri = position.FilePath,
            Range = new Range(
                position.Line - 1,
                position.Column - 1,
                position.Line - 1,
                position.Column - 1
            ),
        };
    }
}
