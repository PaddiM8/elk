#region

using System;
using Elk.Exceptions;
using Elk.Std.Attributes;
using Elk.Std.DataTypes;

#endregion

namespace Elk.Std;

[ElkModule("testing")]
static class Testing
{
    /// <summary>
    /// Throws a runtime error if the given boolean is false.
    /// </summary>
    [ElkFunction("assert", Reachability.Everywhere)]
    public static void Assert(RuntimeBoolean boolean)
    {
        if (!boolean.IsTrue)
            throw new RuntimeAssertException();
    }

    /// <summary>
    /// Throws a runtime error if the given boolean is false.
    /// </summary>
    [ElkFunction("assertEqual", Reachability.Everywhere)]
    public static void AssertEqual(RuntimeObject got, RuntimeObject expected)
    {
        if (!got.Equals(expected))
            throw new RuntimeAssertException(got, expected);
    }
}