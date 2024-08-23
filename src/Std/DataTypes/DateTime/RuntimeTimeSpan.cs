using System;
using System.Collections.Generic;
using System.Globalization;
using Elk.Exceptions;
using Elk.Parsing;
using Elk.Std.Attributes;

namespace Elk.Std.DataTypes.DateTime;

[ElkType("TimeSpan")]
public class RuntimeTimeSpan(TimeSpan value) : RuntimeObject
{
    public TimeSpan Value { get; } = value;

    public override RuntimeObject As(Type toType)
        => toType switch
        {
            _ when toType == typeof(RuntimeString)
                => new RuntimeString(ToString()),
            _ when toType == typeof(RuntimeTimeSpan)
                => this,
            _
                => throw new RuntimeCastException<RuntimeError>(toType),
        };

    public override RuntimeObject Operation(OperationKind kind, RuntimeObject other)
    {
        var otherTimeSpan = other.As<RuntimeTimeSpan>();

        return kind switch
        {
            OperationKind.Addition => new RuntimeTimeSpan(Value.Add(otherTimeSpan.Value)),
            OperationKind.Subtraction => new RuntimeTimeSpan(Value.Subtract(otherTimeSpan.Value)),
            OperationKind.Greater => RuntimeBoolean.From(Value > otherTimeSpan.Value),
            OperationKind.GreaterEquals => RuntimeBoolean.From(Value >= otherTimeSpan.Value),
            OperationKind.Less => RuntimeBoolean.From(Value < otherTimeSpan.Value),
            OperationKind.LessEquals => RuntimeBoolean.From(Value <= otherTimeSpan.Value),
            OperationKind.EqualsEquals => RuntimeBoolean.From(Value == otherTimeSpan.Value),
            OperationKind.NotEquals => RuntimeBoolean.From(Value != otherTimeSpan.Value),
            _ => throw InvalidOperation(kind),
        };
    }

    public override string ToString()
        => Value.ToString();
}