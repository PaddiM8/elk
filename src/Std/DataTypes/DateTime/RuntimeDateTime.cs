using System;
using System.Collections.Generic;
using System.Globalization;
using Elk.Exceptions;
using Elk.Parsing;
using Elk.Std.Attributes;
using Elk.Std.DataTypes.DateTime;

namespace Elk.Std.DataTypes;

[ElkType("DateTime")]
public class RuntimeDateTime(System.DateTime value) : RuntimeObject
{
    public System.DateTime Value { get; } = value;

    public override RuntimeObject As(Type toType)
        => toType switch
        {
            _ when toType == typeof(RuntimeString)
                => new RuntimeString(ToString()),
            _ when toType == typeof(RuntimeInteger)
                => new RuntimeInteger(((DateTimeOffset)Value).ToUnixTimeSeconds()),
            _ when toType == typeof(RuntimeDateTime)
                => this,
            _
                => throw new RuntimeCastException<RuntimeError>(toType),
        };

    public override RuntimeObject Operation(OperationKind kind, RuntimeObject other)
    {
        if (other is RuntimeTimeSpan otherTimeSpan)
        {
            return kind switch
            {
                OperationKind.Addition => new RuntimeDateTime(Value.Add(otherTimeSpan.Value)),
                OperationKind.Subtraction => new RuntimeDateTime(Value.Subtract(otherTimeSpan.Value)),
                _ => throw InvalidOperation(kind),
            };
        }

        var otherDateTime = other.As<RuntimeDateTime>();

        return kind switch
        {
            OperationKind.Subtraction => new RuntimeTimeSpan(Value.Subtract(otherDateTime.Value)),
            OperationKind.Greater => RuntimeBoolean.From(Value > otherDateTime.Value),
            OperationKind.GreaterEquals => RuntimeBoolean.From(Value >= otherDateTime.Value),
            OperationKind.Less => RuntimeBoolean.From(Value < otherDateTime.Value),
            OperationKind.LessEquals => RuntimeBoolean.From(Value <= otherDateTime.Value),
            OperationKind.EqualsEquals => RuntimeBoolean.From(Value == otherDateTime.Value),
            OperationKind.NotEquals => RuntimeBoolean.From(Value != otherDateTime.Value),
            _ => throw InvalidOperation(kind),
        };
    }

    public override string ToString()
        => Value.ToString("o", CultureInfo.InvariantCulture);
}