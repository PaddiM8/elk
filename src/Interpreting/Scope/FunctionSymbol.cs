using Elk.Parsing;

namespace Elk.Interpreting.Scope;

class FunctionSymbol
{
    public FunctionExpr Expr { get; set; }

    public FunctionSymbol(FunctionExpr expr)
    {
        Expr = expr;
    }
}
