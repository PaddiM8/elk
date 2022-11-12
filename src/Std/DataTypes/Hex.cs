using System.Globalization;
using Elk.Std.Attributes;

namespace Elk.Std.DataTypes;

[ElkModule("hex")]
public class Hex
{
    /// <param name="str">A string representation of a hexadecimal number.</param>
    /// <returns>
    /// The equivalent decimal integer of the given hexadecimal value,
    /// or null if given an invalid value.
    /// </returns>
    [ElkFunction("parse")]
    public static RuntimeObject Parse(RuntimeString str)
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

    /// <param name="decimalValue"></param>
    /// <returns>A string of the hexadecimal representation of the given value.</returns>
    [ElkFunction("into")]
    public static RuntimeString Into(RuntimeInteger decimalValue)
        => new(decimalValue.Value.ToString("x"));
}