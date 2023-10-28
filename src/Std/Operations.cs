using Elk.Parsing;
using Elk.Std.Attributes;
using Elk.Std.DataTypes;

namespace Elk.Std;

[ElkModule("op")]
public class Operations
{
    /// <returns>The result of adding the two given numbers.</returns>
    [ElkFunction("add")]
    public static RuntimeObject Add(RuntimeObject x, RuntimeObject y)
        => x.Operation(OperationKind.Addition, y);

    /// <returns>The result of dividing the two given numbers.</returns>
    [ElkFunction("div")]
    public static RuntimeObject Div(RuntimeObject x, RuntimeObject y)
        => x.Operation(OperationKind.Division, y);

    /// <returns>Whether or not the two values are equal</returns>
    [ElkFunction("equals")]
    public static RuntimeObject Equals(RuntimeObject x, RuntimeObject y)
        => RuntimeBoolean.From(x.Equals(y));

    /// <returns>Whether or not the two values are unequal</returns>
    [ElkFunction("notEquals")]
    public static RuntimeObject NotEquals(RuntimeObject x, RuntimeObject y)
        => RuntimeBoolean.From(!x.Equals(y));

    /// <returns>The result of multiplying the two given numbers.</returns>
    [ElkFunction("mul")]
    public static RuntimeObject Mul(RuntimeObject x, RuntimeObject y)
        => x.Operation(OperationKind.Multiplication, y);

    /// <returns>The result of raising x to the power of y.</returns>
    [ElkFunction("pow")]
    public static RuntimeObject Pow(RuntimeObject x, RuntimeObject y)
        => x.Operation(OperationKind.Power, y);

    /// <returns>The result of subtracting the two given numbers.</returns>
    [ElkFunction("sub")]
    public static RuntimeObject Sub(RuntimeObject x, RuntimeObject y)
        => x.Operation(OperationKind.Subtraction, y);
}