using System.Linq;
using Elk.Interpreting.Exceptions;
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

    /// <param name="codepoint">The unicode codepoint to turn into a character</param>
    /// <returns>A string consisting of a single unicode character.</returns>
    [ElkFunction("char")]
    public static RuntimeString Char(RuntimeInteger codepoint)
        => new (char.ConvertFromUtf32((int)codepoint.Value));

    /// <param name="charString">A string containing the character to be converted.</param>
    /// <returns>An integer representing the unicode codepoint of the character.</returns>
    [ElkFunction("charCode")]
    public static RuntimeInteger CharCode(RuntimeString charString)
    {
        if (charString.Value.Length == 0)
            throw new RuntimeStdException("Cannot convert an empty string into a character code.");

        return new(char.ConvertToUtf32(charString.Value, 0));
    }
}