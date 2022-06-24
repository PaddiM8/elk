#region

using System;
using Elk.Interpreting.Exceptions;
using Elk.Parsing;
using Elk.Std.Attributes;

#endregion

namespace Elk.Std.DataTypes;

[ElkType("Nil")]
public class RuntimeNil : IRuntimeValue
{
    public static readonly RuntimeNil Value = new();

    public IRuntimeValue As(Type toType)
        => toType switch
        {
            var type when type == typeof(RuntimeBoolean)
                => RuntimeBoolean.False,
            _
                => throw new RuntimeCastException<RuntimeNil>(toType),
        };

    public IRuntimeValue Operation(OperationKind kind)
        => kind == OperationKind.Not
            ? RuntimeBoolean.True
            : this;

    public IRuntimeValue Operation(OperationKind kind, IRuntimeValue other)
        => kind switch
        {
            OperationKind.Equals => RuntimeBoolean.From(other is RuntimeNil),
            OperationKind.NotEquals => RuntimeBoolean.From(other is not RuntimeNil),
            _ => this,
        };

    public override int GetHashCode()
        => throw new RuntimeUnableToHashException<RuntimeNil>();

    public override string ToString()
        => "nil";
}