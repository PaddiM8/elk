#region

using System;
using System.Text.RegularExpressions;
using Elk.Interpreting.Exceptions;
using Elk.Parsing;
using Elk.Std.Attributes;

#endregion

namespace Elk.Std.DataTypes;

[ElkType("Regex")]
public class RuntimeRegex : RuntimeObject
{
    public Regex Value { get; }

    public RuntimeRegex(Regex value)
    {
        Value = value;
    }

    public override RuntimeObject As(Type toType)
        => toType switch
        {
            _ when toType == typeof(RuntimeRegex)
                => this,
            _ when toType == typeof(RuntimeString)
                => new RuntimeString(Value.ToString()),
            _ when toType == typeof(RuntimeBoolean)
                => RuntimeBoolean.True,
            _
                => throw new RuntimeCastException<RuntimeRegex>(toType),
        };

    public override RuntimeObject Operation(OperationKind kind)
        => throw new RuntimeInvalidOperationException(kind.ToString(), "Regex");

    public override RuntimeObject Operation(OperationKind kind, RuntimeObject other)
        => throw new RuntimeInvalidOperationException(kind.ToString(), "Regex");

    public override int GetHashCode()
        => Value.GetHashCode();

    public override string ToString()
        => Value.ToString();
}