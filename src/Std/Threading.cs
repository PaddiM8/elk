using System.Threading;
using Elk.Attributes;
using Elk.Interpreting;

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

namespace Elk.Std;

static class Threading
{
    [ElkFunction("sleep", Reachability.Everywhere)]
    public static void EndsWith(RuntimeInteger length)
    {
        Thread.Sleep((int)length.Value);
    }
}