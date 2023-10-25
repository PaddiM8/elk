using Elk.Std.Attributes;
using Elk.Std.DataTypes;

namespace Elk.Std;

[ElkModule("pipe")]
public static class Pipe
{
    [ElkFunction("dispose", Reachability.Everywhere, StartsPipeManually = true, ConsumesPipe = true)]
    public static RuntimePipe Dispose(RuntimePipe pipe)
    {
        pipe.EnableDisposeOutput();
        pipe.Start();

        return pipe;
    }

    [ElkFunction("disposeAll", Reachability.Everywhere, StartsPipeManually = true)]
    public static RuntimePipe DisposeAll(RuntimePipe pipe)
    {
        pipe.EnableDisposeOutput();
        pipe.EnableDisposeError();
        pipe.Start();

        return pipe;
    }

    [ElkFunction("disposeErr", Reachability.Everywhere, StartsPipeManually = true)]
    public static RuntimePipe DisposeErr(RuntimePipe pipe)
    {
        pipe.EnableDisposeError();
        pipe.Start();

        return pipe;
    }
}