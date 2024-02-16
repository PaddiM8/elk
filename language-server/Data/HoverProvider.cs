using System.Text;
using System.Text.RegularExpressions;
using Elk.Parsing;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Elk.LanguageServer.Data;

class HoverProvider
{
    private readonly int _line;
    private readonly int _column;

    private HoverProvider(int line, int column)
    {
        // Elk positions start at 1
        _line = line + 1;
        _column = column + 1;
    }

    public static MarkedString? GetInfo(SemanticDocument document, int line, int column)
    {
        var provider = new HoverProvider(line, column);
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

        var signatureStringBuilder = new StringBuilder();

        // Documentation
        var documentation = callExpr.StdFunction?.Documentation;
        if (documentation != null)
        {
            documentation = Regex.Replace(documentation, @"\n+\s*", "\n# ");
            signatureStringBuilder.AppendLine($"# {documentation}");
        }

        // Name
        signatureStringBuilder.Append($"fn {callExpr.Identifier.Value}(");

        // Parameters
        var parameterNames = callExpr.FunctionSymbol?.Expr.Parameters.Select(x => x.Identifier.Value)
            ?? callExpr.StdFunction?.Parameters.Select(x => x.Name)
            ?? [];
        signatureStringBuilder.Append(string.Join(", ", parameterNames));
        signatureStringBuilder.Append(')');

        if (callExpr.FunctionSymbol?.Expr.HasClosure is true)
            signatureStringBuilder.Append(" => closure");

        return new MarkedString("elk", signatureStringBuilder.ToString());
    }
}