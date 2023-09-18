using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Elk.Std.Attributes;
using Elk.Std.DataTypes;
using Elk.Std.DataTypes.Serialization;
using Newtonsoft.Json;

namespace Elk.Std;

[ElkModule("parse")]
public static class Parse
{
    [ElkFunction("csv")]
    public static RuntimeList Csv(RuntimeObject csv, RuntimeString? separator = null)
    {
        char separatorChar = separator?.Value.FirstOrDefault() ?? ',';
        var lines = csv is RuntimePipe pipe
            ? pipe.Select(x => x.As<RuntimeString>().Value)
            : csv.As<RuntimeString>().Value.ToLines();

        return new RuntimeList(new CsvParser(lines, separatorChar));
    }

    /// <param name="str">A string representation of a hexadecimal number.</param>
    /// <returns>
    /// The equivalent decimal integer of the given hexadecimal value,
    /// or null if given an invalid value.
    /// </returns>
    [ElkFunction("hex")]
    public static RuntimeObject Hex(RuntimeString str)
    {
        bool success = int.TryParse(
            str.Value.TrimStart('0').TrimStart('x'),
            NumberStyles.HexNumber,
            CultureInfo.InvariantCulture,
            out int result
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
    /// </summary>
    /// <param name="stringValue">The string to parse.</param>
    /// <param name="headerColumns">Header columns for the table, if the input does not have a header.</param>
    [ElkFunction("table")]
    public static RuntimeTable Table(RuntimeString stringValue, [ElkVariadic] IEnumerable<RuntimeObject> headerColumns)
    {
        var lines = stringValue.Value
            .Trim()
            .ToLines()
            .Select(x =>
                x.Split('\t').Select(y => new RuntimeString(y))
            );
        var header = headerColumns.Any()
            ? headerColumns
            : lines.FirstOrDefault() ?? new List<RuntimeString>();

        return new RuntimeTable(
            new RuntimeList(header),
            headerColumns.Any() ? lines : lines.Skip(1)
        );
    }
}