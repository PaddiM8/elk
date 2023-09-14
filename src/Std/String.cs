// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

#region

using System.Collections.Generic;
using System.Linq;
using System.Text;
using Elk.Std.Attributes;
using Elk.Std.DataTypes;

#endregion

namespace Elk.Std;

[ElkModule("str")]
static class String
{
    /// <summary>
    /// Gets the text at a specific row and line.
    /// </summary>
    /// <param name="input">A string consisting of several lines and columns.</param>
    /// <param name="rowIndex">The index of the row.</param>
    /// <param name="columnIndex">The index of the column.</param>
    /// <param name="divider">The character sequence that divides the column. Default: "\t"</param>
    /// <returns>A string if the cell was found, otherwise null.</returns>
    [ElkFunction("cell")]
    public static RuntimeObject Cell(
        RuntimeString input,
        RuntimeInteger rowIndex,
        RuntimeInteger columnIndex,
        RuntimeString? divider = null)
    {
        var line = input.Value.ToLines().ElementAtOrDefault((int)rowIndex.Value);
        var column = line?.Split(divider?.Value ?? "\t").ElementAtOrDefault((int)columnIndex.Value);
        if (line == null || column == null)
            return RuntimeNil.Value;

        return new RuntimeString(column);
    }

    /// <param name="str">The string to turn into chunks</param>
    /// <param name="size">The length of each chunk</param>
    /// <returns>A list of string chunks.</returns>
    [ElkFunction("chunks")]
    public static RuntimeList Chunks(RuntimeString str, RuntimeInteger size)
    {
        if (size.Value == 0)
            return new RuntimeList(new List<RuntimeObject>());

        var chunks = new List<RuntimeString>();
        var builder = new StringBuilder();
        foreach (var c in str)
        {
            if (builder.Length < size.Value)
            {
                builder.Append(c);
            }
            else
            {
                chunks.Add(new RuntimeString(builder.ToString()));
                builder.Clear();
                builder.Append(c);
            }
        }

        if (builder.Length > 0)
            chunks.Add(new RuntimeString(builder.ToString()));

        return new RuntimeList(chunks);
    }

    /// <summary>
    /// Gets a column from a multi-line string.
    /// </summary>
    /// <param name="input">A string consisting of several columns.</param>
    /// <param name="index">The index of the column.</param>
    /// <param name="divider">The character sequence that divides the column. Default: "\t"</param>
    /// <returns>A list of lines within the specific column.</returns>
    [ElkFunction("column")]
    public static RuntimeList Column(
        RuntimeString input,
        RuntimeInteger index,
        RuntimeString? divider = null)
    {
        var result = from line in input.Value.ToLines()
            select line.Split(divider?.Value ?? "\t")
            into columns
            where index.Value < columns.Length
            select columns[index.Value]
            into column
            where !string.IsNullOrEmpty(column)
            select new RuntimeString(column);

        return new(result);
    }

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
    [ElkFunction("lines")]
    public static RuntimeList Lines(RuntimeString input)
    {
        var lines = input.Value.ToLines().ToList();
        if (lines.LastOrDefault() == "")
            lines.RemoveAt(lines.Count - 1);

        return new(lines.Select(x => new RuntimeString(x)));
    }

    /// <returns>A copy of the input string with all the letters made lowercase.</returns>
    [ElkFunction("lower")]
    public static RuntimeString Lower(RuntimeString input)
        => new(input.Value.ToLower());

    /// <param name="str">A string consisting of one unicode character</param>
    /// <returns>The character code.</returns>
    [ElkFunction("ord")]
    public static RuntimeInteger Ord(RuntimeString str)
        => new(char.ConvertToUtf32(str.Value, 0));

    /// <param name="input">Entire string</param>
    /// <param name="start">Substring</param>
    /// <returns>Whether or not the input string starts with the string provided as the second argument.</returns>
    [ElkFunction("startsWith")]
    public static RuntimeBoolean StartsWith(RuntimeString input, RuntimeString start)
        => RuntimeBoolean.From(input.Value.StartsWith(start.Value));

    /// <summary>
    /// Gets a list of columns from a multi-line string.
    /// </summary>
    /// <param name="input">A string consisting of several columns and lines.</param>
    /// <param name="divider">The character sequence that divides the columns. Default: "\t"</param>
    /// <returns>A list of lists of columns.</returns>
    [ElkFunction("table")]
    public static RuntimeList Table(RuntimeString input, RuntimeString? divider = null)
    {
        var table = input
            .Value
            .ToLines()
            .Select(
                x =>
                    x
                        .Split(divider?.Value ?? "\t")
                        .Select(y => new RuntimeString(y))
            )
            .Select(x => new RuntimeList(x))
            .Where(x => x.Values.Count > 1 || !string.IsNullOrEmpty(x.Values.First().ToString()));

        return new(table.ToList());
    }

    /// <returns>A copy of the given string without the leading and trailing white-space characters.</returns>
    [ElkFunction("trim")]
    public static RuntimeString Trim(RuntimeString input)
        => new(input.Value.Trim());

    /// <param name="source"></param>
    /// <param name="pattern" types="String, Regex"></param>
    /// <param name="replacement"></param>
    /// <returns>A new string with all occurrences of the pattern string replaced with the replacement string.</returns>
    [ElkFunction("replace")]
    public static RuntimeString Replace(RuntimeString source, RuntimeObject pattern, RuntimeString replacement)
        => pattern is RuntimeRegex regex
            ? new(regex.Value.Replace(source.Value, replacement.Value))
            : new(source.Value.Replace(pattern.As<RuntimeString>().Value, replacement.Value));

    /// <param name="input">String to split up into parts</param>
    /// <param name="delimiter">Where to split (default: " ")</param>
    /// <param name="count">The amount of substrings to produce</param>
    /// <returns>A list of all the different parts as a result of splitting the string.</returns>
    [ElkFunction("split", Reachability.Everywhere)]
    public static RuntimeList Split(RuntimeString input, RuntimeString? delimiter = null, RuntimeInteger? count = null)
        => count == null
            ? new(input.Value.Split(delimiter?.Value ?? " ").Select(x => new RuntimeString(x)))
            : new(input.Value.Split(delimiter?.Value ?? " ", (int)count.Value).Select(x => new RuntimeString(x)));

    /// <returns>A copy of the input string with all the letters made uppercase.</returns>
    [ElkFunction("upper")]
    public static RuntimeString Upper(RuntimeString input)
        => new(input.Value.ToUpper());
}