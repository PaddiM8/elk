using Elk.Attributes;
using Elk.Interpreting;
using Elk.Interpreting.Exceptions;

namespace Elk.Std;

static class Testing
{
    [ShellFunction("assert")]
    public static void Assert(RuntimeBoolean boolean)
    {
        if (!boolean.Value)
            throw new RuntimeAssertException();
    }
}