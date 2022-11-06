

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

#region

using System.Linq;
using Elk.Std.Attributes;
using Elk.Std.DataTypes;

#endregion

namespace Elk.Std;

[ElkModule("str")]
static class String
{
    /// <param name="input">Entire string</param>
    /// <param name="ending">Substring</param>
    /// <returns>Whether or not the input string ends with the string provided as the second argument.</returns>
    [ElkFunction("endsWith")]
    public static RuntimeBoolean EndsWith(RuntimeString input, RuntimeString ending)
        => RuntimeBoolean.From(input.Value.EndsWith(ending.Value));

    /// <returns>Whether or not the given string consists of a single digit.</returns>
    [ElkFunction("isDigit")]
    public static RuntimeBoolean IsDigit(RuntimeString str)
        => RuntimeBoolean.From(str.Value.Length == 1 && char.IsDigit(str.Value[0]));

    /// <returns>A list of all the lines in the given string.</returns>
    [ElkFunction("lines", Reachability.Everywhere)]
    public static RuntimeList Lines(RuntimeString input)
    {
        var lines = input.Value.Split(System.Environment.NewLine).ToList();
        if (lines.LastOrDefault() == "")
            lines.RemoveAt(lines.Count - 1);

        return new(lines.Select(x => new RuntimeString(x)));
    }

    /// <returns>A copy of the input string with all the letters made lowercase.</returns>
    [ElkFunction("lower")]
    public static RuntimeString Lower(RuntimeString input)
        => new(input.Value.ToLower());

    /// <param name="input">Entire string</param>
    /// <param name="start">Substring</param>
    /// <returns>Whether or not the input string starts with the string provided as the second argument.</returns>
    [ElkFunction("startsWith")]
    public static RuntimeBoolean StartsWith(RuntimeString input, RuntimeString start)
        => RuntimeBoolean.From(input.Value.StartsWith(start.Value));

    /// <returns>A copy of the given string without the leading and trailing white-space characters.</returns>
    [ElkFunction("trim")]
    public static RuntimeString Trim(RuntimeString input)
        => new(input.Value.Trim());

    /// <param name="input">String to split up into parts</param>
    /// <param name="delimiter">Where to split (default: " ")</param>
    /// <returns>A list of all the different parts as a result of splitting the string.</returns>
    [ElkFunction("split", Reachability.Everywhere)]
    public static RuntimeList Split(RuntimeString input, RuntimeString? delimiter = null)
        => new(input.Value.Split(delimiter?.Value ?? " ").Select(x => new RuntimeString(x)));

    /// <returns>A copy of the input string with all the letters made uppercase.</returns>
    [ElkFunction("upper")]
    public static RuntimeString Upper(RuntimeString input)
        => new(input.Value.ToUpper());
}