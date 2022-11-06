using System;
using System.Linq;
using Elk.Std.Attributes;
using Elk.Std.DataTypes;

namespace Elk.Std;

[ElkModule("collections")]
public class Collections
{
    [ElkFunction("select", Reachability.Everywhere)]
    public static RuntimeList Select(RuntimeList items, Func<IRuntimeValue, IRuntimeValue> closure)
        => new(items.Select(closure));
}