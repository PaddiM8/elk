using Elk.Parsing;

namespace Elk.Interpreting.Scope;

public class FunctionSymbol(FunctionExpr expr)
{
    public FunctionExpr Expr { get; set; } = expr;
}
