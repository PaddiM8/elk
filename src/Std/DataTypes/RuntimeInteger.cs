#region

using System;
using Elk.Interpreting.Exceptions;
using Elk.Parsing;
using Elk.Std.Attributes;

#endregion

namespace Elk.Std.DataTypes;

[ElkType("Integer")]
public class RuntimeInteger : RuntimeObject
{
    public long Value { get; }

    public RuntimeInteger(long value)
    {
        Value = value;
    }

    public override RuntimeObject As(Type toType)
        => toType switch
        {
            var type when type == typeof(RuntimeInteger)
                => this,
            var type when type == typeof(RuntimeFloat)
                => new RuntimeFloat(Value),
            var type when type == typeof(RuntimeString)
                => new RuntimeString(Value.ToString()),
            var type when type == typeof(RuntimeBoolean)
                => RuntimeBoolean.From(Value != 0),
            _
                => throw new RuntimeCastException<RuntimeInteger>(toType),
        };

    public override RuntimeObject Operation(OperationKind kind)
        => kind switch
        {
            OperationKind.Subtraction => new RuntimeInteger(-Value),
            OperationKind.Not => RuntimeBoolean.From(Value == 0),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

    public override RuntimeObject Operation(OperationKind kind, RuntimeObject other)
    {
        if (other is RuntimeFloat)
            return As<RuntimeFloat>().Operation(kind, other);

        var otherNumber = other.As<RuntimeInteger>();
        return kind switch
        {
            OperationKind.Addition => new RuntimeInteger(Value + otherNumber.Value),
            OperationKind.Subtraction => new RuntimeInteger(Value - otherNumber.Value),
            OperationKind.Multiplication => new RuntimeInteger(Value * otherNumber.Value),
            OperationKind.Division => new RuntimeFloat((double)Value / otherNumber.Value),
            OperationKind.Modulo => new RuntimeInteger(Value % otherNumber.Value),
            OperationKind.Power => new RuntimeFloat(Math.Pow(Value, otherNumber.Value)),
            OperationKind.Greater => RuntimeBoolean.From(Value > otherNumber.Value),
            OperationKind.GreaterEquals => RuntimeBoolean.From(Value >= otherNumber.Value),
            OperationKind.Less => RuntimeBoolean.From(Value < otherNumber.Value),
            OperationKind.LessEquals => RuntimeBoolean.From(Value <= otherNumber.Value),
            OperationKind.EqualsEquals => RuntimeBoolean.From(Value == otherNumber.Value),
            OperationKind.NotEquals => RuntimeBoolean.From(Value != otherNumber.Value),
            _ => throw new RuntimeInvalidOperationException(kind.ToString(), "Integer"),
        };
    }

    public override int GetHashCode()
        => Value.GetHashCode();

    public override string ToString()
        => Value.ToString();
}