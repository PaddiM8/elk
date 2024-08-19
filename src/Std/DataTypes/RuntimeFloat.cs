#region

using System;
using Elk.Exceptions;
using Elk.Parsing;
using Elk.Std.Attributes;

#endregion

namespace Elk.Std.DataTypes;

[ElkType("Float")]
public class RuntimeFloat(double value) : RuntimeObject
{
    public double Value { get; } = value;

    public override bool Equals(object? obj)
        => obj is RuntimeObject runtimeObject &&
            Operation(OperationKind.EqualsEquals, runtimeObject) is RuntimeBoolean { IsTrue: true };

    public override int CompareTo(RuntimeObject? other)
        => other is null or RuntimeNil
            ? 1
            : Value.CompareTo(other.As<RuntimeFloat>().Value);

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
            _ => throw InvalidOperation(kind),
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
            _ => throw InvalidOperation(kind),
        };
    }

    public override int GetHashCode()
        => throw new RuntimeUnableToHashException<RuntimeFloat>();

    public override string ToString()
        => Value.ToString();
}