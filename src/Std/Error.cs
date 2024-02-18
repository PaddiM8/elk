using Elk.Std.Attributes;
using Elk.Std.DataTypes;

namespace Elk.Std;

[ElkModule("error")]
public class Error
{
    /// <returns>The value inside the Error object.</returns>
    [ElkFunction("value")]
    public static RuntimeObject Value(RuntimeError error)
        => error.Value;
}