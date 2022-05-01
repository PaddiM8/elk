using System;
using Elk.Interpreting.Exceptions;
using Elk.Parsing;

namespace Elk.Interpreting;

public class RuntimeFloat : IRuntimeValue
{
    public double Value { get; }

    public RuntimeFloat(double value)
    {
        Value = value;
    }

    public IRuntimeValue As(Type toType)
        => toType switch
        {
            var type when type == typeof(RuntimeFloat)
                => this,
            var type when type == typeof(RuntimeInteger)
                => new RuntimeInteger((int)Value),
            var type when type == typeof(RuntimeString)
                => new RuntimeString(Value.ToString()),
            var type when type == typeof(RuntimeBoolean)
                => RuntimeBoolean.From(Value != 0),
            _
                => throw new RuntimeCastException<RuntimeFloat>(toType),
        };

    public IRuntimeValue Operation(OperationKind kind)
        => kind switch
        {
            OperationKind.Subtraction => new RuntimeFloat(-Value),
            OperationKind.Not => RuntimeBoolean.From(Value == 0),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

    public IRuntimeValue Operation(OperationKind kind, IRuntimeValue other)
    {
        var otherNumber = other.As<RuntimeFloat>();
        return kind switch
        {
            OperationKind.Addition => new RuntimeFloat(Value + otherNumber.Value),
            OperationKind.Subtraction => new RuntimeFloat(Value - otherNumber.Value),
            OperationKind.Multiplication => new RuntimeFloat(Value * otherNumber.Value),
            OperationKind.Division => new RuntimeFloat(Value / otherNumber.Value),
            OperationKind.Power => new RuntimeFloat(Math.Pow(Value, otherNumber.Value)),
            OperationKind.Greater => RuntimeBoolean.From(Value > otherNumber.Value),
            OperationKind.GreaterEquals => RuntimeBoolean.From(Value >= otherNumber.Value),
            OperationKind.Less => RuntimeBoolean.From(Value < otherNumber.Value),
            OperationKind.LessEquals => RuntimeBoolean.From(Value <= otherNumber.Value),
            OperationKind.EqualsEquals => RuntimeBoolean.From(Value == otherNumber.Value),
            OperationKind.NotEquals => RuntimeBoolean.From(Value != otherNumber.Value),
            _ => throw new RuntimeInvalidOperationException(kind.ToString(), "Float"),
        };
    }

    public override int GetHashCode()
        => throw new RuntimeUnableToHashException<RuntimeFloat>();

    public override string ToString()
        => Value.ToString();
}