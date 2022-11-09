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

    internal TextPos? Position { get; }

    public RuntimeError(string value)
    {
        Value = value;
    }

    internal RuntimeError(string value, TextPos position)
    {
        Value = value;
        Position = position;
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
        => throw new RuntimeException(Value, Position);

    public override RuntimeObject Operation(OperationKind kind, RuntimeObject other)
        => throw new RuntimeException(Value, Position);

    public override int GetHashCode()
        => Value.GetHashCode();

    public override string ToString()
        => $"{Position} {Value}";
}
