using Elk.Std.Attributes;
using Elk.Std.DataTypes;

namespace Elk.Std;

[ElkModule("bitwise")]
public class Bitwise
{
    [ElkFunction("bitAnd")]
    public static RuntimeInteger And(RuntimeInteger a, RuntimeInteger b)
        => new(a.Value & b.Value);

    [ElkFunction("bitOr")]
    public static RuntimeInteger Or(RuntimeInteger a, RuntimeInteger b)
        => new(a.Value | b.Value);

    [ElkFunction("xor")]
    public static RuntimeInteger Xor(RuntimeInteger a, RuntimeInteger b)
        => new(a.Value ^ b.Value);
}