using Elk.Std.Attributes;
using Elk.Std.DataTypes;

namespace Elk.Std;

[ElkModule("pipe")]
public static class Pipe
{
    /// <summary>
    /// Starts the given pipe in the background without redirecting output.
    /// </summary>
    /// <returns>The given pipe.</returns>
    [ElkFunction("background", Reachability.Everywhere, StartsPipeManually = true, ConsumesPipe = true)]
    public static RuntimePipe Background(RuntimePipe pipe)
    {
        pipe.MakeBackground();
        pipe.Start();

        return pipe;
    }

    /// <summary>
    /// Configures the given pipe to start in the background, but does not start
    /// it yet. This makes it possible to then use a function like `dispose`.
    /// </summary>
    /// <returns>The given pipe.</returns>
    /// <example>some-program | backgroundAnd | dispose</example>
    [ElkFunction("backgroundAnd", Reachability.Everywhere, StartsPipeManually = true, ConsumesPipe = true)]
    public static RuntimePipe BackgroundAnd(RuntimePipe pipe)
    {
        pipe.MakeBackground();

        return pipe;
    }


    /// <summary>
    /// Configures the given pipe to dispose stdout, meaning it won't be printed
    /// and won't be saved in a buffer.
    /// </summary>
    /// <returns>The given pipe.</returns>
    [ElkFunction("dispose", Reachability.Everywhere, StartsPipeManually = true)]
    public static RuntimePipe Dispose(RuntimePipe pipe)
    {
        pipe.EnableDisposeOutput();
        pipe.Start();

        return pipe;
    }

    /// <summary>
    /// Configures the given pipe to dispose stdout and stderr, meaning it won't be printed
    /// and won't be saved in a buffer.
    /// </summary>
    /// <returns>The given pipe.</returns>
    [ElkFunction("disposeAll", Reachability.Everywhere, StartsPipeManually = true)]
    public static RuntimePipe DisposeAll(RuntimePipe pipe)
    {
        pipe.EnableDisposeOutput();
        pipe.EnableDisposeError();
        pipe.Start();

        return pipe;
    }

    /// <summary>
    /// Configures the given pipe to dispose stderr, meaning it won't be printed
    /// and won't be saved in a buffer.
    /// </summary>
    /// <returns>The given pipe.</returns>
    [ElkFunction("disposeErr", Reachability.Everywhere, StartsPipeManually = true)]
    public static RuntimePipe DisposeErr(RuntimePipe pipe)
    {
        pipe.EnableDisposeError();
        pipe.Start();

        return pipe;
    }

    /// <summary>
    /// Waits for a pipe that was started in the background.
    /// </summary>
    /// <returns>The exit code.</returns>
    [ElkFunction("wait", Reachability.Everywhere)]
    public static RuntimeInteger Wait(RuntimePipe pipe)
        => new(pipe.Wait());

    /// <summary>
    /// Waits for the given pipes (that were started in the background).
    /// </summary>
    /// <returns>The exit code.</returns>
    [ElkFunction("waitAll", Reachability.Everywhere)]
    public static void WaitAll(RuntimeList pipes)
    {
        foreach (var pipe in pipes)
            pipe.As<RuntimePipe>().Wait();
    }
}