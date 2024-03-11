#region

using System;
using System.Collections.Generic;
using Elk.Interpreting.Exceptions;
using Elk.Interpreting.Scope;
using Elk.Parsing;
using Elk.Std.Attributes;
using Elk.Std.Bindings;
using Elk.Vm;

#endregion

namespace Elk.Std.DataTypes;

public delegate RuntimeObject Invoker(List<RuntimeObject> arguments, bool isRoot);

[ElkType("Function")]
public abstract class RuntimeFunction : RuntimeObject
{
    public IEnumerable<RuntimeObject> Arguments { get; }

    public Plurality Plurality { get; }

    internal Page? Page { get; set; }

    public Invoker Invoker { get; }


    internal RuntimeFunction(
        IEnumerable<RuntimeObject>? arguments,
        Plurality plurality,
        Func<RuntimeFunction, Invoker> createInvoker)
    {
        Arguments = arguments ?? Array.Empty<RuntimeObject>();
        Plurality = plurality;
        Invoker = createInvoker(this);
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
        Plurality plurality,
        Func<RuntimeFunction, Invoker> createInvoker)
        : base(arguments, plurality, createInvoker)
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
        Plurality plurality,
        Func<RuntimeFunction, Invoker> createInvoker)
        : base(arguments, plurality, createInvoker)
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
        Plurality plurality,
        Func<RuntimeFunction, Invoker> createInvoker)
        : base(arguments, plurality, createInvoker)
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
        LocalScope environment,
        Func<RuntimeFunction, Invoker> createInvoker)
        : base(null, Plurality.Singular, createInvoker)
    {
        Expr = expr;
        Environment = environment;
    }

    public override int GetHashCode()
        => Expr.GetHashCode();

    public override string ToString()
        => "<closure>";
}