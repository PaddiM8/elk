using System;
using System.Collections.Generic;
using Elk.Interpreting.Exceptions;
using Elk.Std.Attributes;

namespace Elk.Std.DataTypes;

[ElkType("Error")]
public class RuntimeError(RuntimeObject value) : RuntimeObject
{
    public RuntimeObject Value { get; } = value;

    public IList<Trace>? StackTrace { get; set; } = null;

    public override RuntimeObject As(Type toType)
        => toType switch
        {
            _ when toType == typeof(RuntimeError)
                => this,
            _
                => throw new RuntimeCastException<RuntimeError>(toType),
        };

    public override string? ToString()
        => Value.ToString();
}