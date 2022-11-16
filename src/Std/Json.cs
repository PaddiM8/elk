using System.IO;
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

    /// <summary>
    /// Deserializes the content in the given file.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <returns>A JSON string.</returns>
    [ElkFunction("read")]
    public static RuntimeObject Read(RuntimeString path)
    {
        var fileContent = File.ReadAllText(path.Value);

        return JsonConvert.DeserializeObject<RuntimeObject>(fileContent, new RuntimeObjectJsonConverter())
           ?? RuntimeNil.Value;
    }

    /// <summary>
    /// Serializes the given object and writes it to a file.
    /// </summary>
    /// <param name="input">The object to serialize.</param>
    /// <param name="path">The file path.</param>
    /// <param name="indentationStyle">One of: "indented", "i", nil</param>
    [ElkFunction("write")]
    public static void Write(RuntimeObject input, RuntimeString path, RuntimeString? indentationStyle = null)
    {
        File.WriteAllText(path.Value, Serialize(input, indentationStyle));
    }

    internal static string Serialize(RuntimeObject input, RuntimeString? indentationStyle = null)
    {
        var formatting = indentationStyle?.Value is "indented" or "i"
            ? Formatting.Indented
            : Formatting.None;

        return JsonConvert.SerializeObject(
            input,
            formatting,
            new RuntimeObjectJsonConverter()
        );
    }
}