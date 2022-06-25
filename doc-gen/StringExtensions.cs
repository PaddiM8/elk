namespace Elk.DocGen;

public static class StringExtensions
{
    public static string RemoveTrailing(this string value, string trailing)
        => value.EndsWith(trailing)
            ? value[..^trailing.Length]
            : value;
}