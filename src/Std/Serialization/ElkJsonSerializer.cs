using System.Globalization;
using System.IO;
using System.Text;
using Elk.Std.DataTypes;
using Newtonsoft.Json;

namespace Elk.Std.Serialization;

public static class ElkJsonSerializer
{
    private static readonly RuntimeObjectJsonConverter _runtimeObjectJsonConverter = new();
    private static readonly JsonSerializer _serializer;

    static ElkJsonSerializer()
    {
        _serializer = JsonSerializer.CreateDefault();
        _serializer.Converters.Add(new RuntimeObjectJsonConverter());
    }

    public static string Serialize<T>(T value, Formatting formatting = Formatting.None)
    {
        var builder = new StringBuilder();
        var stringWriter = new StringWriter(builder, CultureInfo.InvariantCulture);

        using var jsonWriter = new JsonTextWriter(stringWriter);
        jsonWriter.Formatting = formatting;
        jsonWriter.IndentChar = ' ';
        jsonWriter.Indentation = 4;

        _serializer.Serialize(jsonWriter, value, typeof(T));

        return stringWriter.ToString();
    }

    public static RuntimeObject? Deserialize(string json)
        => JsonConvert.DeserializeObject<RuntimeObject>(json, _runtimeObjectJsonConverter);
}