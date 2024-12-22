using Elk.Std.Attributes;
using Elk.Std.DataTypes;

namespace Elk.Std;

[ElkModule("int")]
public class Int
{
    /// <returns>The maximum possible value of the Integer type</returns>
    [ElkFunction("max")]
    public static RuntimeInteger Max()
        => new(long.MaxValue);
}