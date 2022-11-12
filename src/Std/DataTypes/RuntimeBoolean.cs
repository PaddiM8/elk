#region

using System;
using Elk.Interpreting.Exceptions;
using Elk.Parsing;
using Elk.Std.Attributes;

#endregion

namespace Elk.Std.DataTypes;

[ElkType("Boolean")]
public class RuntimeBoolean : RuntimeObject
{
    public static RuntimeBoolean True => new(true);
    public static RuntimeBoolean False => new(false);

    public bool IsTrue { get; }

    private RuntimeBoolean(bool isTrue)
    {
        IsTrue = isTrue;
    }

    public override RuntimeObject As(Type toType)
        => toType switch
        {
            _ when toType == typeof(RuntimeBoolean)
                => this,
            _ when toType == typeof(RuntimeString)
                => new RuntimeString(IsTrue.ToString()),
            _
                => throw new RuntimeCastException<RuntimeBoolean>(toType),
        };

    public static RuntimeBoolean From(bool value)
        => value ? True : False;

    public override RuntimeObject Operation(OperationKind kind)
        => new RuntimeBoolean(!IsTrue);

    public override RuntimeObject Operation(OperationKind kind, RuntimeObject other)
    {
        var otherBoolean = other.As<RuntimeBoolean>();
        var newValue = kind switch
        {
            OperationKind.EqualsEquals => IsTrue == otherBoolean.IsTrue,
            OperationKind.NotEquals => IsTrue != otherBoolean.IsTrue,
            OperationKind.And => IsTrue && otherBoolean.IsTrue,
            OperationKind.Or => IsTrue || otherBoolean.IsTrue,
            _ => throw new RuntimeInvalidOperationException(kind.ToString(), "Boolean"),
        };

        return new RuntimeBoolean(newValue);
    }

    public override int GetHashCode()
        => IsTrue.GetHashCode();

    public override string ToString()
        => IsTrue.ToString();
}