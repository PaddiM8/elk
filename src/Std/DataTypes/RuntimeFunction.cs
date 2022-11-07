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
public abstract class RuntimeFunction : RuntimeObject
{
    public override RuntimeObject As(Type toType)
        => toType switch
        {
            var type when type == typeof(RuntimeFunction)
                => this,
            var type when type == typeof(RuntimeBoolean)
                => RuntimeBoolean.True,
            var type when type == typeof(RuntimeString)
                => new RuntimeString(ToString() ?? "Function"),
            _
                => throw new RuntimeCastException<RuntimeBoolean>(toType),
        };

    public override RuntimeObject Operation(OperationKind kind)
        => throw new RuntimeInvalidOperationException(kind.ToString(), "Function");

    public override RuntimeObject Operation(OperationKind kind, RuntimeObject other)
        => throw new RuntimeInvalidOperationException(kind.ToString(), "Function");
}

internal class RuntimeStdFunction : RuntimeFunction
{
    public MethodInfo StdFunction { get; }

    public RuntimeStdFunction(MethodInfo stdFunction)
    {
        StdFunction = stdFunction;
    }

    public override int GetHashCode()
        => StdFunction.GetHashCode();

    public override string ToString()
        => StdFunction.Name;
}

internal class RuntimeSymbolFunction : RuntimeFunction
{
    public FunctionSymbol FunctionSymbol { get; }

    public RuntimeSymbolFunction(FunctionSymbol functionSymbol)
    {
        FunctionSymbol = functionSymbol;
    }

    public override int GetHashCode()
        => FunctionSymbol.GetHashCode();

    public override string ToString()
        => FunctionSymbol.Expr.Identifier.Value;
}

internal class RuntimeProgramFunction : RuntimeFunction
{
    public string ProgramName { get; }

    public RuntimeProgramFunction(string programName)
    {
        ProgramName = programName;
    }

    public override int GetHashCode()
        => ProgramName.GetHashCode();

    public override string ToString()
        => ProgramName;
}

internal class RuntimeClosureFunction : RuntimeFunction
{
    public ClosureExpr Closure { get; }

    public RuntimeClosureFunction(ClosureExpr closure)
    {
        Closure = closure;
    }

    public override int GetHashCode()
        => Closure.GetHashCode();

    public override string ToString()
        => "<closure>";
}