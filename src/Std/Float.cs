using Elk.Std.Attributes;
using Elk.Std.DataTypes;

namespace Elk.Std;

[ElkModule("float")]
public class Float
{
    /// <returns>The maximum possible value of the Float type</returns>
    [ElkFunction("max")]
    public static RuntimeFloat Max()
        => new(double.MaxValue);
}
