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

    /// <returns>A JSON string.</returns>
    [ElkFunction("write")]
    public static RuntimeString Write(RuntimeObject input)
        => new(JsonConvert.SerializeObject(input, new RuntimeObjectJsonConverter()));
}