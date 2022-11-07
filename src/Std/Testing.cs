#region

using Elk.Interpreting.Exceptions;
using Elk.Std.Attributes;
using Elk.Std.DataTypes;

#endregion

namespace Elk.Std;

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
}