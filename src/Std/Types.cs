using Elk.Attributes;
using Elk.Interpreting;

namespace Elk.Std;

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

[ElkModule("types")]
public class Types
{
    [ElkFunction("isType", Reachability.Everywhere)]
    public static RuntimeBoolean IsType(IRuntimeValue value, RuntimeType type)
        => RuntimeBoolean.From(value.GetType() == type.Type);
}