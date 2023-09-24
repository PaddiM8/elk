#region

using System;
using System.Collections.Generic;
using Elk.Interpreting.Exceptions;
using Elk.Interpreting.Scope;
using Elk.Parsing;
using Elk.Std.Attributes;
using Elk.Std.Bindings;

#endregion

namespace Elk.Std.DataTypes;

[ElkType("Function")]
public abstract class RuntimeFunction : RuntimeObject
{
    public IEnumerable<RuntimeObject> Arguments { get; }

    public Plurality Plurality { get; }

    internal RuntimeFunction(IEnumerable<RuntimeObject>? arguments, Plurality plurality)
    {
        Arguments = arguments ?? Array.Empty<RuntimeObject>();
        Plurality = plurality;
    }

    public override RuntimeObject As(Type toType)
        => toType switch
        {
            _ when toType == typeof(RuntimeFunction)
                => this,
            _ when toType == typeof(RuntimeBoolean)
                => RuntimeBoolean.True,
            _ when toType == typeof(RuntimeString)
                => new RuntimeString(ToString() ?? "Function"),
            _
                => throw new RuntimeCastException<RuntimeBoolean>(toType),
        };
}

internal class RuntimeStdFunction : RuntimeFunction
{
    public StdFunction StdFunction { get; }

    public RuntimeStdFunction(
        StdFunction stdFunction,
        IEnumerable<RuntimeObject>? arguments,
        Plurality plurality)
        : base(arguments, plurality)
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

    public RuntimeSymbolFunction(
        FunctionSymbol functionSymbol,
        IEnumerable<RuntimeObject>? arguments,
        Plurality plurality)
        : base(arguments, plurality)
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

    public RuntimeProgramFunction(
        string programName,
        IEnumerable<RuntimeObject>? arguments,
        Plurality plurality)
        : base(arguments, plurality)
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
    public ClosureExpr Expr { get; }

    public LocalScope Environment { get; }

    public RuntimeClosureFunction(
        ClosureExpr expr,
        LocalScope environment)
        : base(null, Plurality.Singular)
    {
        Expr = expr;
        Environment = environment;
    }

    public override int GetHashCode()
        => Expr.GetHashCode();

    public override string ToString()
        => "<closure>";
}