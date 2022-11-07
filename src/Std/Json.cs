using Elk.Std.Attributes;
using Elk.Std.DataTypes;
using Elk.Std.DataTypes.Serialization;
using Newtonsoft.Json;

namespace Elk.Std;

[ElkModule("json")]
public class Json
{
    /// <param name="input">A JSON string.</param>
    [ElkFunction("parse")]
    public static RuntimeObject Parse(RuntimeString input)
        => JsonConvert.DeserializeObject<RuntimeObject>(input.Value, new RuntimeObjectJsonConverter())
           ?? RuntimeNil.Value;

    /// <param name="input"></param>
    /// <param name="indentationStyle">One of: "indented", "i", nil</param>
    /// <returns>A JSON string.</returns>
    [ElkFunction("write")]
    public static RuntimeString Write(RuntimeObject input, RuntimeString? indentationStyle = null)
    {
        var formatting = indentationStyle?.Value is "indented" or "i"
            ? Formatting.Indented
            : Formatting.None;

        return new(JsonConvert.SerializeObject(
            input,
            formatting,
            new RuntimeObjectJsonConverter()
        ));
    }
}