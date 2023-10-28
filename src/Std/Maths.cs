#region

using System;
using System.Collections.Generic;
using System.Linq;
using Elk.Interpreting.Exceptions;
using Elk.Parsing;
using Elk.Std.Attributes;
using Elk.Std.DataTypes;

#endregion

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

namespace Elk.Std;

[ElkModule("math")]
static class Maths
{
    /// <param name="x" types="Integer, Float"></param>
    /// <returns>The input number made positive.</returns>
    [ElkFunction("abs", Reachability.Everywhere)]
    public static RuntimeObject Abs(RuntimeObject x)
        => x switch
        {
            RuntimeInteger integer => new RuntimeInteger(Math.Abs(integer.Value)),
            _ => new RuntimeFloat(Math.Abs(x.As<RuntimeFloat>().Value)),
        };

    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns>The result of adding the two given numbers.</returns>
    [ElkFunction("add")]
    public static RuntimeObject Add(RuntimeObject x, RuntimeObject y)
        => x.Operation(OperationKind.Addition, y);

    /// <param name="x" types="Integer, Float"></param>
    /// <returns>The input number rounded up.</returns>
    [ElkFunction("ceil", Reachability.Everywhere)]
    public static RuntimeFloat Ceil(RuntimeObject x)
        => new(Math.Ceiling(x.As<RuntimeFloat>().Value));

    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns>The result of dividing the two given numbers.</returns>
    [ElkFunction("div")]
    public static RuntimeObject Div(RuntimeObject x, RuntimeObject y)
        => x.Operation(OperationKind.Division, y);

    /// <param name="x" types="Integer, Float"></param>
    /// <returns>The input number rounded down.</returns>
    [ElkFunction("floor", Reachability.Everywhere)]
    public static RuntimeFloat Floor(RuntimeObject x)
        => new(Math.Floor(x.As<RuntimeFloat>().Value));

    /// <param name="x" types="Integer, Float"></param>
    /// <param name="y" types="Integer, Float"></param>
    /// <returns>The highest of the two input numbers.</returns>
    [ElkFunction("max", Reachability.Everywhere)]
    public static RuntimeObject Max(RuntimeObject x, RuntimeObject? y = null)
    {
        if (y == null)
        {
            if (x is not IEnumerable<RuntimeObject> items)
                throw new RuntimeCastException(x.GetType(), "Iterable");

            return items.Max() ?? RuntimeNil.Value;
        }

        return x.CompareTo(y) == 1
            ? x
            : y;
    }

    /// <param name="x" types="Integer, Float"></param>
    /// <param name="y" types="Integer, Float"></param>
    /// <returns>The lowest of the two input numbers.</returns>
    [ElkFunction("min", Reachability.Everywhere)]
    public static RuntimeObject Min(RuntimeObject x, RuntimeObject? y = null)
    {
        if (y == null)
        {
            if (x is not IEnumerable<RuntimeObject> items)
                throw new RuntimeCastException(x.GetType(), "Iterable");

            return items.Min() ?? RuntimeNil.Value;
        }

        return x.CompareTo(y) == -1
            ? x
            : y;
    }

    /// <returns>The lowest value in the Iterable with the closure applied.</returns>
    [ElkFunction("minOf", Reachability.Everywhere)]
    public static RuntimeObject MinOf(IEnumerable<RuntimeObject> items, Func<RuntimeObject, RuntimeObject> closure)
        => items.Min(closure) ?? RuntimeNil.Value;

    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns>The result of multiplying the two given numbers.</returns>
    [ElkFunction("mul")]
    public static RuntimeObject Mul(RuntimeObject x, RuntimeObject y)
        => x.Operation(OperationKind.Multiplication, y);

    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns>The result of raising x to the power of y.</returns>
    [ElkFunction("pow")]
    public static RuntimeObject Pow(RuntimeObject x, RuntimeObject y)
        => x.Operation(OperationKind.Power, y);

    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns>The result of subtracting the two given numbers.</returns>
    [ElkFunction("sub")]
    public static RuntimeObject Sub(RuntimeObject x, RuntimeObject y)
        => x.Operation(OperationKind.Subtraction, y);

    /// <param name="items">Items to sum</param>
    /// <returns>An Integer of Float of the sum of the given values.</returns>
    [ElkFunction("sum")]
    public static RuntimeObject Sum(IEnumerable<RuntimeObject> items)
    {
        // TODO: Better handling for Integers
        var result = items.Sum(x => x.As<RuntimeFloat>().Value);

        return Math.Floor(result) == result
            ? new RuntimeInteger((int)result)
            : new RuntimeFloat(result);
    }

    /// <param name="x" types="Integer, Float"></param>
    /// <returns>The square root of the input number</returns>
    [ElkFunction("sqrt", Reachability.Everywhere)]
    public static RuntimeFloat Sqrt(RuntimeObject x)
        => x is RuntimeInteger integer
            ? new(Math.Sqrt(integer.Value))
            : new(Math.Sqrt(x.As<RuntimeFloat>().Value));
}