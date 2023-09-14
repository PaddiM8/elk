#region

using System;
using Elk.Interpreting.Exceptions;
using Elk.Lexing;
using Elk.Parsing;
using Elk.Std.Attributes;

#endregion

namespace Elk.Std.DataTypes;

[ElkType("Error")]
public class RuntimeError : RuntimeObject
{
    public string Value { get; }

    private readonly TextPos _position;

    internal RuntimeError(string value, TextPos position)
    {
        Value = value;
        _position = position;
    }

    public override RuntimeObject As(Type toType)
        => toType switch
        {
            _ when toType == typeof(RuntimeError)
                => this,
            _ when toType == typeof(RuntimeBoolean)
                => RuntimeBoolean.False,
            _
                => throw new RuntimeUserException(Value),
        };

    public override RuntimeObject Operation(OperationKind kind)
        => throw new RuntimeException(Value, _position);

    public override RuntimeObject Operation(OperationKind kind, RuntimeObject other)
        => throw new RuntimeException(Value, _position);

    public override int GetHashCode()
        => Value.GetHashCode();

    public override string ToString()
        => Value.Length > 0
            ? $"{_position} {Value}"
            : "";
}
