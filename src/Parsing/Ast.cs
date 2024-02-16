using System.Collections.Generic;
using Elk.Services;

namespace Elk.Parsing;

public class Ast(IList<Expr> expressions)
{
    public IList<Expr> Expressions { get; } = expressions;

    public Expr? FindExpressionAt(int line, int column)
        => FindExpressionAt(line, column, Expressions);

    private Expr? FindExpressionAt(int line, int column, IEnumerable<Expr> children)
    {
        foreach (var expr in children)
        {
            if (line < expr.StartPosition.Line || line > expr.EndPosition.Line)
                continue;

            var isSameLine = expr.StartPosition.Line == expr.EndPosition.Line;
            if (isSameLine && (column < expr.StartPosition.Column || column > expr.EndPosition.Column))
                continue;

            return FindExpressionAt(line, column, expr.ChildExpressions)
                ?? expr;
        }

        return null;
    }

    public IEnumerable<SemanticToken> GetSemanticTokens()
        => SemanticTokenGenerator.GetSemanticTokens(Expressions);
}