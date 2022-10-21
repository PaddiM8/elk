#region

using System;
using System.Reflection;
using Elk.Interpreting.Exceptions;
using Elk.Interpreting.Scope;
using Elk.Parsing;
using Elk.Std.Attributes;

#endregion

namespace Elk.Std.DataTypes;

[ElkType("Function")]
public class RuntimeFunction : IRuntimeValue
{
    internal MethodInfo? StdFunction { get; }

    internal FunctionSymbol? FunctionSymbol { get; }

    internal RuntimeFunction(MethodInfo stdFunction)
    {
        StdFunction = stdFunction;
    }

    internal RuntimeFunction(FunctionSymbol functionSymbol)
    {
        FunctionSymbol = functionSymbol;
    }

    public IRuntimeValue As(Type toType)
        => toType switch
        {
            var type when type == typeof(RuntimeFunction)
                => this,
            var type when type == typeof(RuntimeBoolean)
                => RuntimeBoolean.True,
            var type when type == typeof(RuntimeString)
                => new RuntimeString(ToString()),
            _
                => throw new RuntimeCastException<RuntimeBoolean>(toType),
        };

    public IRuntimeValue Operation(OperationKind kind)
        => throw new RuntimeInvalidOperationException(kind.ToString(), "Function");

    public IRuntimeValue Operation(OperationKind kind, IRuntimeValue other)
        => throw new RuntimeInvalidOperationException(kind.ToString(), "Function");

    public override int GetHashCode()
        => StdFunction?.GetHashCode() ?? FunctionSymbol!.GetHashCode();

    public override string ToString()
        => StdFunction?.Name ?? FunctionSymbol!.Expr.Identifier.Value;
}