using Elk.Parsing;

namespace Elk.Scoping;

public class FunctionSymbol(FunctionExpr expr) : ISymbol
{
    public FunctionExpr Expr { get; set; } = expr;
}
