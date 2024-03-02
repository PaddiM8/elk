using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Elk.Std.Attributes;
using Elk.Std.DataTypes;
using Elk.Std.DataTypes.Serialization;
using Newtonsoft.Json;

namespace Elk.Std;

[ElkModule("parse")]
public static partial class Parse
{
    [ElkFunction("csv")]
    public static RuntimeGenerator Csv(RuntimeObject csv, RuntimeString? separator = null)
    {
        var separatorChar = separator?.Value.FirstOrDefault() ?? ',';
        var lines = csv is RuntimePipe pipe
            ? pipe.Select(x => x.As<RuntimeString>().Value)
            : csv.As<RuntimeString>().Value.ToLines();

        return new RuntimeGenerator(new CsvParser(lines, separatorChar));
    }

    /// <param name="str">A string representation of a hexadecimal number.</param>
    /// <returns>
    /// The equivalent decimal integer of the given hexadecimal value,
    /// or null if given an invalid value.
    /// </returns>
    [ElkFunction("hex")]
    public static RuntimeObject Hex(RuntimeString str)
    {
        var success = int.TryParse(
            str.Value.TrimStart('0').TrimStart('x'),
            NumberStyles.HexNumber,
            CultureInfo.InvariantCulture,
            out var result
        );

        return success
            ? new RuntimeInteger(result)
            : RuntimeNil.Value;
    }

    [ElkFunction("json")]
    public static RuntimeObject Json(RuntimeString json)
        => JsonConvert.DeserializeObject<RuntimeObject>(json.Value, new RuntimeObjectJsonConverter())
           ?? RuntimeNil.Value;

    /// <summary>
    /// Parses a string into a Table.
    /// Only works for tables separates by tabs or 2+ spaces.
    /// </summary>
    /// <param name="stringValue">The string to parse.</param>
    /// <param name="headerColumns">Header columns for the table, if the input does not have a header.</param>
    [ElkFunction("table")]
    public static RuntimeTable Table(RuntimeString stringValue, [ElkVariadic] IEnumerable<RuntimeObject> headerColumns)
    {
        var separatorRegex = WhiteSpaceRegex();
        var lines = stringValue.Value
            .Trim()
            .ToLines()
            .Select(line =>
                separatorRegex
                    .Split(line)
                    .Select<string, RuntimeObject>(x => new RuntimeString(x))
                    .ToList()
            )
            .Where(x => x.Count >= 2)
            .ToList();

        if (headerColumns.Any())
        {
            return new RuntimeTable(
                new RuntimeList(headerColumns.ToList()),
                lines
            );
        }

        return new RuntimeTable(
            new RuntimeList(lines.FirstOrDefault()?.ToList() ?? []),
            lines[1..]
        );
    }

    [GeneratedRegex(@"\t\s*|\s{2,}", RegexOptions.Compiled)]
    private static partial System.Text.RegularExpressions.Regex WhiteSpaceRegex();
}