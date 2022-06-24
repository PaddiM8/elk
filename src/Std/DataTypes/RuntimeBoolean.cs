#region

using System;
using Elk.Interpreting.Exceptions;
using Elk.Parsing;
using Elk.Std.Attributes;

#endregion

namespace Elk.Std.DataTypes;

[ElkType("Boolean")]
public class RuntimeBoolean : IRuntimeValue
{
    public static RuntimeBoolean True => new(true);
    public static RuntimeBoolean False => new(false);

    public bool Value { get; }

    private RuntimeBoolean(bool value)
    {
        Value = value;
    }

    public IRuntimeValue As(Type toType)
        => toType switch
        {
            var type when type == typeof(RuntimeBoolean)
                => this,
            var type when type == typeof(RuntimeString)
                => new RuntimeString(Value.ToString()),
            _
                => throw new RuntimeCastException<RuntimeBoolean>(toType),
        };

    public static RuntimeBoolean From(bool value)
        => value ? True : False;

    public IRuntimeValue Operation(OperationKind kind)
        => new RuntimeBoolean(!Value);

    public IRuntimeValue Operation(OperationKind kind, IRuntimeValue other)
    {
        var otherBoolean = other.As<RuntimeBoolean>();
        var newValue = kind switch
        {
            OperationKind.EqualsEquals => Value == otherBoolean.Value,
            OperationKind.NotEquals => Value != otherBoolean.Value,
            OperationKind.And => Value && otherBoolean.Value,
            OperationKind.Or => Value || otherBoolean.Value,
            _ => throw new RuntimeInvalidOperationException(kind.ToString(), "Boolean"),
        };

        return new RuntimeBoolean(newValue);
    }

    public override int GetHashCode()
        => Value.GetHashCode();

    public override string ToString()
        => Value.ToString();
}