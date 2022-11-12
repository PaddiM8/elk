#region

using System;
using Elk.Interpreting.Exceptions;
using Elk.Parsing;
using Elk.Std.Attributes;

#endregion

namespace Elk.Std.DataTypes;

[ElkType("Float")]
public class RuntimeFloat : RuntimeObject
{
    public double Value { get; }

    public RuntimeFloat(double value)
    {
        Value = value;
    }

    public override RuntimeObject As(Type toType)
        => toType switch
        {
            _ when toType == typeof(RuntimeFloat)
                => this,
            _ when toType == typeof(RuntimeInteger)
                => new RuntimeInteger((int)Value),
            _ when toType == typeof(RuntimeString)
                => new RuntimeString(Value.ToString()),
            _ when toType == typeof(RuntimeBoolean)
                => RuntimeBoolean.From(Value != 0),
            _
                => throw new RuntimeCastException<RuntimeFloat>(toType),
        };

    public override RuntimeObject Operation(OperationKind kind)
        => kind switch
        {
            OperationKind.Subtraction => new RuntimeFloat(-Value),
            OperationKind.Not => RuntimeBoolean.From(Value == 0),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

    public override RuntimeObject Operation(OperationKind kind, RuntimeObject other)
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