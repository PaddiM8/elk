using Elk.Scoping;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Elk.LanguageServer.Data;

public static class CompletionBuilder
{
    public static CompletionItem FromSymbol(ISymbol symbol)
    {
        return symbol switch
        {
            ModuleScope module => new CompletionItem
            {
                Label = module.Name ?? "",
                Kind = CompletionItemKind.Module,
            },
            StructSymbol structSymbol => new CompletionItem
            {
                Label = structSymbol.Name,
                Kind = CompletionItemKind.Struct,
            },
            FunctionSymbol functionSymbol => new CompletionItem
            {
                Label = functionSymbol.Expr.Identifier.Value,
                Kind = CompletionItemKind.Function,
            },
            VariableSymbol variableSymbol => new CompletionItem
            {
                Label = variableSymbol.Name,
                Kind = CompletionItemKind.Variable,
            },
            _ => new CompletionItem(),
        };
    }
}