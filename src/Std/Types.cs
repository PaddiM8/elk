#region

using Elk.Std.Attributes;
using Elk.Std.DataTypes;

#endregion

namespace Elk.Std;

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

[ElkModule("types")]
public class Types
{
    /// <returns>Whether or not the given value is of a specific type.</returns>
    [ElkFunction("isType", Reachability.Everywhere)]
    public static RuntimeBoolean IsType(IRuntimeValue value, RuntimeType type)
        => RuntimeBoolean.From(value.GetType() == type.Type);
}