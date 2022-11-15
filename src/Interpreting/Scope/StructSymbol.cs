using Elk.Parsing;

namespace Elk.Interpreting.Scope;

class StructSymbol
{
    public StructExpr Expr { get; set; }

    public StructSymbol(StructExpr expr)
    {
        Expr = expr;
    }
}
