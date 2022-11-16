using Elk.Std.Attributes;
using Elk.Std.DataTypes;

namespace Elk.Std;

[ElkModule("into")]
public class Into
{
    /// <param name="input"></param>
    /// <param name="indentationStyle">One of: "indented", "i", nil</param>
    /// <returns>A JSON string.</returns>
    [ElkFunction("json")]
    public static RuntimeString Json(RuntimeObject input, RuntimeString? indentationStyle = null)
        => new(Std.Json.Serialize(input, indentationStyle));
}