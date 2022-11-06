using System;
using System.Collections.Generic;
using System.Linq;
using Elk.Std.Attributes;
using Elk.Std.DataTypes;

namespace Elk.Std;

[ElkModule("collections")]
public class Collections
{
    /// <param name="items"></param>
    /// <param name="closure"></param>
    /// <returns>A list of values where the closure has been called on each value.</returns>
    /// <example>[1, 2, 3] | select => x: x + 1 #=> [2, 3, 4]</example>
    [ElkFunction("select", Reachability.Everywhere)]
    public static RuntimeList Select(IEnumerable<IRuntimeValue> items, Func<IRuntimeValue, IRuntimeValue> closure)
        => new(items.Select(closure));
}