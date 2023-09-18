using System.Collections.Generic;
using System.Linq;
using System.Text;
using Elk.Interpreting.Exceptions;
using Elk.Std.Attributes;
using Elk.Std.DataTypes;
using Elk.Std.DataTypes.Serialization;
using Newtonsoft.Json;

namespace Elk.Std;

[ElkModule("into")]
public class Into
{
    /// <param name="value">Value that should be cast</param>
    [ElkFunction("bool")]
    public static RuntimeBoolean ToBool(RuntimeObject value)
        => value.As<RuntimeBoolean>();

    /// <param name="codepoint">The unicode codepoint to turn into a character</param>
    /// <returns>A string consisting of a single unicode character.</returns>
    [ElkFunction("char")]
    public static RuntimeString Char(RuntimeInteger codepoint)
        => new(char.ConvertFromUtf32((int)codepoint.Value));

    /// <param name="rows">A collection of rows containing a list of columns.</param>
    /// <param name="separator">The column separator. Default: ,</param>
    /// <returns>A CSV string.</returns>
    [ElkFunction("csv")]
    public static RuntimeString Csv(IEnumerable<RuntimeObject> rows, RuntimeString? separator = null)
    {
        var builder = new StringBuilder();
        char separatorChar = separator?.Value.FirstOrDefault() ?? ',';

        void BuildRow(IEnumerable<string> cells)
        {
            foreach (var cell in cells)
            {
                var escaped = cell.Replace("\"", "\"\"");
                bool needsQuotes = escaped.Any(x => x is '"' or ',' or '\n');
                builder.Append(
                    needsQuotes
                        ? $"\"{escaped}\""
                        : escaped
                );
                builder.Append(separatorChar);
            }

            if (cells.Any())
                builder.Remove(builder.Length - 1, 1);
            builder.AppendLine();
        }

        if (rows is RuntimeTable table)
            BuildRow(table.Header);

        foreach (var row in rows)
        {
            if (row is not IEnumerable<RuntimeObject> enumerableRow)
                throw new RuntimeCastException(row.GetType(), "Iterable");

            var cellValues = enumerableRow.Select(x => x.As<RuntimeString>().Value);
            BuildRow(cellValues);
        }

        return new(builder.ToString());
    }

    /// <param name="charString">A string containing the character to be converted.</param>
    /// <returns>An integer representing the unicode codepoint of the character.</returns>
    [ElkFunction("charCode")]
    public static RuntimeInteger CharCode(RuntimeString charString)
    {
        if (charString.Value.Length == 0)
            throw new RuntimeStdException("Cannot convert an empty string into a character code.");

        return new(char.ConvertToUtf32(charString.Value, 0));
    }

    /// <param name="value">Value that should be cast</param>
    [ElkFunction("dict")]
    public static RuntimeDictionary ToDictionary(RuntimeObject? value = null)
        => value?.As<RuntimeDictionary>() ?? new RuntimeDictionary(new());

    /// <param name="value">Value that should be cast</param>
    [ElkFunction("float")]
    public static RuntimeFloat ToFloat(RuntimeObject value)
        => value.As<RuntimeFloat>();

    /// <param name="decimalValue"></param>
    /// <returns>A string of the hexadecimal representation of the given value.</returns>
    [ElkFunction("hex")]
    public static RuntimeString Hex(RuntimeInteger decimalValue)
        => new(decimalValue.Value.ToString("x"));

    /// <param name="value">Value that should be cast</param>
    [ElkFunction("int")]
    public static RuntimeInteger ToInt(RuntimeObject value)
        => value.As<RuntimeInteger>();

    /// <param name="input"></param>
    /// <param name="indentationStyle">One of: "indented", "i", nil</param>
    /// <returns>A JSON string.</returns>
    [ElkFunction("json")]
    public static RuntimeString Json(RuntimeObject input, RuntimeString? indentationStyle = null)
    {
        var formatting = indentationStyle?.Value is "indented" or "i"
            ? Formatting.Indented
            : Formatting.None;

        return new(
            JsonConvert.SerializeObject(
                input,
                formatting,
                new RuntimeObjectJsonConverter()
            )
        );
    }

    /// <param name="value">Value that should be cast</param>
    [ElkFunction("list")]
    public static RuntimeList ToList(RuntimeObject? value = null)
        => value?.As<RuntimeList>() ?? new RuntimeList(new List<RuntimeObject>());

    /// <param name="value">Value that should be cast</param>
    [ElkFunction("regex")]
    public static RuntimeRegex ToRegex(RuntimeObject value)
        => value.As<RuntimeRegex>();

    /// <param name="value">Value that should be cast</param>
    [ElkFunction("set")]
    public static RuntimeSet ToSet(RuntimeObject? value = null)
    {
        if (value is IEnumerable<RuntimeObject> enumerable)
        {
            var dict = new Dictionary<int, RuntimeObject>();
            foreach (var item in enumerable)
                dict.TryAdd(item.GetHashCode(), item);

            return new RuntimeSet(dict);
        }

        return  value?.As<RuntimeSet>() ?? new RuntimeSet(new());
    }

    /// <param name="value">Value that should be cast</param>
    [ElkFunction("str")]
    public static RuntimeString ToString(RuntimeObject value)
        => value.As<RuntimeString>();

    /// <param name="value">Value that should be cast</param>
    [ElkFunction("table")]
    public static RuntimeTable ToTable(RuntimeObject value)
        => value.As<RuntimeTable>();

    /// <param name="value">Value that should be cast</param>
    [ElkFunction("type")]
    public static RuntimeType ToType(RuntimeObject value)
        => new(value.GetType());

    /// <returns>The message stored in the given error.</returns>
    [ElkFunction("message")]
    public static RuntimeString Message(RuntimeError err)
        => new(err.Value);
}