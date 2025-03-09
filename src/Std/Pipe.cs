using System;
using System.Collections.Generic;
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
        pipe.AllowNonZeroExit();
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
        pipe.AllowNonZeroExit();
        pipe.Start();

        return pipe;
    }

    /// <summary>
    /// NOTE: Program must be piped with `|all`. For example, `let (out, err) = program-name |all getOutAndErr`
    /// </summary>
    /// <returns>A tuple contain an stdout stream and an stderr stream.</returns>
    [ElkFunction("getOutAndErr", Reachability.Everywhere, StartsPipeManually = true)]
    public static RuntimeTuple GetOutAndErr(RuntimePipe pipe)
    {
        pipe.EnableSecondaryStreamForStdErr();

        pipe.AllowNonZeroExit();
        pipe.Start();

        return new RuntimeTuple([pipe, pipe.CloneAsSecondary()]);
    }

    private static IEnumerable<RuntimeObject> Enumerate(IEnumerator<RuntimeObject> enumerator)
    {
        while (enumerator.MoveNext())
        {
            yield return enumerator.Current;
        }

        enumerator.Dispose();
    }

    /// <summary>
    /// Configures the given pipe to only redirect stderr.
    /// </summary>
    /// <returns>The given pipe.</returns>
    [ElkFunction("getErr", Reachability.Everywhere, StartsPipeManually = true)]
    public static RuntimePipe Err(RuntimePipe pipe)
    {
        pipe.EnableDisposeOutput();
        pipe.AllowNonZeroExit();
        pipe.Start();

        return pipe;
    }

    /// <returns>
    /// The exit code of the process belonging to the given pipe, or nil if it has not terminated yet.
    /// Note: Only works on pipes that run in the background (eg. where the `background` function has been used).
    /// </returns>
    [ElkFunction("exitCode", Reachability.Everywhere, StartsPipeManually = true)]
    public static RuntimeObject ExitCode(RuntimePipe pipe)
    {
        pipe.AllowNonZeroExit();
        pipe.Start();

        return pipe.ExitCode == null
            ? RuntimeNil.Value
            : new RuntimeInteger(pipe.ExitCode.Value);
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
