using System.Threading;
using Shel.Attributes;
using Shel.Interpreting;

namespace Shel.Std;

static class Threading
{
    [ShellFunction("sleep")]
    public static void EndsWith(RuntimeInteger length)
    {
        Thread.Sleep(length.Value);
    }
}