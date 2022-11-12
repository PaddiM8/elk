#region

using System;
using Elk.Interpreting.Exceptions;
using Elk.Parsing;
using Elk.Std.Attributes;

#endregion

namespace Elk.Std.DataTypes;

[ElkType("Nil")]
public class RuntimeNil : RuntimeObject
{
    public static readonly RuntimeNil Value = new();

    public override RuntimeObject As(Type toType)
        => toType switch
        {
            _ when toType == typeof(RuntimeBoolean)
                => RuntimeBoolean.False,
            _
                => throw new RuntimeCastException<RuntimeNil>(toType),
        };

    public override RuntimeObject Operation(OperationKind kind)
        => kind == OperationKind.Not
            ? RuntimeBoolean.True
            : this;

    public override RuntimeObject Operation(OperationKind kind, RuntimeObject other)
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