#region

using System;
using System.Collections.Generic;
using Elk.Exceptions;
using Elk.Std.Attributes;
using Elk.Std.Bindings;
using Elk.Vm;

#endregion

namespace Elk.Std.DataTypes;

public delegate RuntimeObject Invoker(List<RuntimeObject> arguments, bool isRoot);

[ElkType("Function")]
public abstract class RuntimeFunction : RuntimeObject
{
    public IList<object> Arguments { get; set; }

    public object? Closure { get; set; }

    public required byte ParameterCount { get; init;  }

    public required byte? VariadicStart { get; init;  }

    public required List<RuntimeObject>? DefaultParameters { get; init; }

    public Invoker Invoker { get; }

    internal RuntimeFunction(
        IList<object>? arguments,
        Func<RuntimeFunction, Invoker> createInvoker)
    {
        Arguments = arguments ?? Array.Empty<object>();
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

internal class RuntimeStdFunction(
    StdFunction stdFunction,
    IList<object>? arguments,
    Func<RuntimeFunction, Invoker> createInvoker)
    : RuntimeFunction(arguments, createInvoker)
{
    public StdFunction StdFunction { get; } = stdFunction;

    public override int GetHashCode()
        => StdFunction.GetHashCode();

    public override string ToString()
        => StdFunction.Name;
}

internal class RuntimeUserFunction(
    Page page,
    IList<object>? arguments,
    Func<RuntimeFunction, Invoker> createInvoker)
    : RuntimeFunction(arguments, createInvoker)
{
    internal Page Page { get; } = page;

    public override int GetHashCode()
        => Page.GetHashCode();

    public override string ToString()
        => "<function>";
}

internal class RuntimeProgramFunction(
    string programName,
    IList<object>? arguments,
    Func<RuntimeFunction, Invoker> createInvoker)
    : RuntimeFunction(arguments, createInvoker)
{
    public string ProgramName { get; } = programName;

    public override int GetHashCode()
        => ProgramName.GetHashCode();

    public override string ToString()
        => ProgramName;
}