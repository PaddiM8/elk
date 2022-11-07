

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

#region

using System;
using System.Collections.Generic;
using System.Linq;
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
    [ElkFunction("cell", Reachability.Everywhere)]
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

    /// <summary>
    /// Gets a column from a multi-line string.
    /// </summary>
    /// <param name="input">A string consisting of several columns.</param>
    /// <param name="index">The index of the column.</param>
    /// <param name="divider">The character sequence that divides the column. Default: "\t"</param>
    /// <returns>A list of lines within the specific column.</returns>
    [ElkFunction("column", Reachability.Everywhere)]
    public static RuntimeList Column(
        RuntimeString input,
        RuntimeInteger index,
        RuntimeString? divider = null)
    {
        var column = from line in input.Value.ToLines()
            select line.Split(divider?.Value ?? "\t")
            into columns
            where index.Value < columns.Length
            select new RuntimeString(columns[index.Value]);

        return new(column);
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
    [ElkFunction("lines", Reachability.Everywhere)]
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