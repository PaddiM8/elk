#region

using Elk.Interpreting.Exceptions;
using Elk.Std.Attributes;
using Elk.Std.DataTypes;

#endregion

namespace Elk.Std;

static class Testing
{
    [ElkFunction("assert", Reachability.Everywhere)]
    public static void Assert(RuntimeBoolean boolean)
    {
        if (!boolean.Value)
            throw new RuntimeAssertException();
    }
}