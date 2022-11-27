using System;
using Elk.Parsing;
using Elk.Std.Bindings;

namespace Elk.Interpreting.Scope;

class StructSymbol
{
    public string Name
        => Expr?.Identifier.Value ?? StdStruct!.Name;

    public StructExpr? Expr { get; set; }

    public StdStruct? StdStruct { get; set; }

    public StructSymbol(StructExpr expr)
    {
        Expr = expr;
    }

    public StructSymbol(StdStruct stdStruct)
    {
        StdStruct = stdStruct;
    }
}