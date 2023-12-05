using System;
using Elk.Std.Attributes;
using Elk.Std.DataTypes;

namespace Elk.Std;

[ElkModule("range")]
public class Range
{
    [ElkFunction("intersect")]
    public static RuntimeRange Intersect(RuntimeRange a, RuntimeRange b)
    {
        // TODO: Handle non-finite ranges
        if (!a.From.HasValue || !b.From.HasValue || !a.To.HasValue || !b.To.HasValue)
            return new RuntimeRange(0, 0);

        var lowerBound = Math.Max(a.From.Value, b.From.Value);
        var upperBound = Math.Min(a.To.Value, b.To.Value);
        return lowerBound <= upperBound
            ? new RuntimeRange(lowerBound, upperBound)
            : new RuntimeRange(0, 0);
    }

    [ElkFunction("except")]
    public static RuntimeRange Except(RuntimeRange a, RuntimeRange b)
    {
        // TODO: Handle non-finite ranges
        if (!a.From.HasValue || !b.From.HasValue || !a.To.HasValue || !b.To.HasValue)
            return new RuntimeRange(0, 0);

        if (a.To.Value < b.From.Value || b.To.Value < a.From.Value)
            return new RuntimeRange(0, 0);

        if (a.From.Value < b.From.Value)
        {
            return new RuntimeRange(
                Math.Min(a.From.Value, b.From.Value),
                Math.Max(a.From.Value, b.From.Value)
            );
        }

        return new RuntimeRange(
            Math.Min(a.To.Value, b.To.Value),
            Math.Max(a.To.Value, b.To.Value)
        );
    }

    [ElkFunction("bounds")]
    public static RuntimeTuple Bounds(RuntimeRange range)
        => new([
            range.From.HasValue
                ? new RuntimeInteger(range.From.Value)
                : RuntimeNil.Value,
            range.To.HasValue
                ? new RuntimeInteger(range.To.Value)
                : RuntimeNil.Value,
        ]);
}