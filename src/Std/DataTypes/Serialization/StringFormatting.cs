namespace Elk.Std.DataTypes.Serialization;

public class StringFormatting
{
    public static string ToDisplayString(string input)
        => $"\"{input.Replace("\n", "\\n").Replace("\"", "\\\"")}\"";
}