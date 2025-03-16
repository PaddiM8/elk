using Elk.LanguageServer.Lsp.Items;
using Elk.Lexing;
using Elk.Parsing;

namespace Elk.LanguageServer.Data;

class SymbolInformationProvider
{
    private readonly SemanticDocument _document;
    private readonly int _line;
    private readonly int _column;

    private SymbolInformationProvider(SemanticDocument document, int line, int column)
    {
        _document = document;
        // Elk positions start at 1
        _line = line + 1;
        _column = column + 1;
    }

    public static MarkedString? GetInfo(SemanticDocument document, int line, int column)
    {
        var provider = new SymbolInformationProvider(document, line, column);
        if (document.Ast == null)
            return null;

        return document.Ast.FindExpressionAt(provider._line, provider._column) switch
        {
            CallExpr callExpr => provider.GetCallInfo(callExpr),
            _ => null,
        };
    }

    private MarkedString? GetCallInfo(CallExpr callExpr)
    {
        if (!callExpr.Identifier.IsByPosition(_line, _column))
            return null;

        if (callExpr.CallType is not (CallType.Function or CallType.StdFunction))
            return null;

        var parameterNames = callExpr.FunctionSymbol?.Expr.Parameters.Select(x => x.Identifier.Value)
            ?? callExpr.StdFunction?.Parameters.Select(x => x.Name)
            ?? [];

        return DocumentationBuilder.BuildSignature(
            callExpr.Identifier.Value,
            parameterNames,
            callExpr.StdFunction?.Documentation,
            hasClosure: callExpr.FunctionSymbol?.Expr.HasClosure
                ?? callExpr.StdFunction?.HasClosure is true
        );
    }

    public static TextPos? GetDefinition(SemanticDocument document, int line, int column)
    {
        var provider = new SymbolInformationProvider(document, line, column);
        if (document.Ast == null)
            return null;

        return document.Ast.FindExpressionAt(provider._line, provider._column) switch
        {
            CallExpr callExpr => provider.GetCallDefinition(callExpr),
            NewExpr newExpr => provider.GetNewDefinition(newExpr),
            _ => null,
        };
    }

    private TextPos? GetCallDefinition(CallExpr callExpr)
    {
        return callExpr.Identifier.IsByPosition(_line, _column)
            ? callExpr.FunctionSymbol?.Expr.StartPosition
            : GetModuleDefinition(callExpr.ModulePath);
    }

    private TextPos? GetNewDefinition(NewExpr newExpr)
    {
        return newExpr.Identifier.IsByPosition(_line, _column)
            ? newExpr.StructSymbol?.Expr?.StartPosition
            : GetModuleDefinition(newExpr.ModulePath);
    }

    private TextPos? GetModuleDefinition(IList<Token> fullModulePath)
    {
        // If _line and _column align with one of the tokens in the given
        // module path, find the module corresponding to that token.
        // Otherwise, symbol information wasn't requested for the module path
        // at all and null will be returned.
        var queryModulePath = new List<Token>();
        var foundSelectedToken = false;
        foreach (var token in fullModulePath)
        {
            queryModulePath.Add(token);
            if (token.IsByPosition(_line, _column))
            {
                foundSelectedToken = true;

                break;
            }
        }

        if (!foundSelectedToken)
            return null;

        var foundModule = _document.Module
            .FindModule(queryModulePath, lookInImports: true);
        if (foundModule == null)
            return null;

        return foundModule.Ast.Expressions.FirstOrDefault()?.StartPosition
            ?? TextPos.Default with
            {
                FilePath = foundModule.FilePath,
            };
    }
}