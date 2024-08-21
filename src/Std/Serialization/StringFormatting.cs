namespace Elk.Std.Serialization;

public static class StringFormatting
{
    public static string ToDisplayString(string input)
        => $"\"{System.Text.RegularExpressions.Regex.Replace(input, @"\r?\n", "\\n").Replace("\"", "\\\"")}\"";
}