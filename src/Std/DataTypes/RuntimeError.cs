#region

using System;
using Elk.Interpreting.Exceptions;
using Elk.Parsing;
using Elk.Std.Attributes;

#endregion

namespace Elk.Std.DataTypes;

[ElkType("Error")]
public class RuntimeError : RuntimeObject
{
    public string Value { get; }

    public RuntimeError(string value)
    {
        Value = value;
    }

    public override RuntimeObject As(Type toType)
        => toType switch
        {
            var type when type == typeof(RuntimeError)
                => this,
            var type when type == typeof(RuntimeBoolean)
                => RuntimeBoolean.False,
            _
                => throw new RuntimeUserException(Value),
        };

    public override RuntimeObject Operation(OperationKind kind)
        => throw new ArgumentOutOfRangeException(nameof(kind), kind, null);

    public override RuntimeObject Operation(OperationKind kind, RuntimeObject other)
        => throw new ArgumentOutOfRangeException(nameof(kind), kind, null);

    public override int GetHashCode()
        => Value.GetHashCode();

    public override string ToString()
        => Value;
}
