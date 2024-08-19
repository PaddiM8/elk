#region

using System;
using Elk.Exceptions;
using Elk.Std.Attributes;

#endregion

namespace Elk.Std.DataTypes;

[ElkType("Regex")]
public class RuntimeRegex(System.Text.RegularExpressions.Regex value) : RuntimeObject
{
    public System.Text.RegularExpressions.Regex Value { get; } = value;

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

    public override int GetHashCode()
        => Value.GetHashCode();

    public override string ToString()
        => Value.ToString();
}