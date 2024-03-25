using Elk.Parsing;
using Elk.Std.Bindings;

namespace Elk.Scoping;

public class StructSymbol : ISymbol
{
    public string Name
        => Expr?.Identifier.Value ?? StdStruct!.Name;

    public StructExpr? Expr { get; set; }

    public StdStruct? StdStruct { get; }

    public StructSymbol(StructExpr expr)
    {
        Expr = expr;
    }

    public StructSymbol(StdStruct stdStruct)
    {
        StdStruct = stdStruct;
    }
}