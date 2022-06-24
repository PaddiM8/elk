using System;
using Elk.Attributes;
using Elk.Interpreting.Exceptions;
using Elk.Parsing;

namespace Elk.Interpreting;

[ElkType("Error")]
public class RuntimeError : IRuntimeValue
{
    public string Value { get; }

    public RuntimeError(string value)
    {
        Value = value;
    }

    public IRuntimeValue As(Type toType)
        => toType switch
        {
            var type when type == typeof(RuntimeError)
                => this,
            var type when type == typeof(RuntimeBoolean)
                => RuntimeBoolean.False,
            _
                => throw new RuntimeUserException(Value),
        };

    public IRuntimeValue Operation(OperationKind kind)
        => throw new ArgumentOutOfRangeException(nameof(kind), kind, null);

    public IRuntimeValue Operation(OperationKind kind, IRuntimeValue other)
        => throw new ArgumentOutOfRangeException(nameof(kind), kind, null);

    public override int GetHashCode()
        => Value.GetHashCode();

    public override string ToString()
        => Value;
}
