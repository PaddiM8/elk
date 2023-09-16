#region

using System;
using System.Diagnostics;
using System.IO;
using Elk.Interpreting;
using Elk.Std.Attributes;
using Elk.Std.DataTypes;

#endregion

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable once InconsistentNaming

namespace Elk.Std;

#pragma warning disable CS1573

[ElkModule("io")]
static class IO
{
    /// <param name="path">A file path</param>
    /// <returns>Text content of the file at the provided path.</returns>
    [ElkFunction("read", Reachability.Everywhere)]
    public static RuntimeString ReadFile(RuntimeString path, ShellEnvironment env)
        => new(File.ReadAllText(env.GetAbsolutePath(path.Value)));

    /// <summary>Writes the provided text to a file, overwriting any previous content.</summary>
    /// <param name="content">Text that should be written to the file</param>
    /// <param name="path">A file path</param>
    /// <returns>nil</returns>
    [ElkFunction("write", Reachability.Everywhere)]
    public static void WriteToFile(RuntimeObject content, RuntimeString path, ShellEnvironment env)
    {
        string absolutePath = env.GetAbsolutePath(path.Value);
        if (content is RuntimePipe runtimePipe)
        {
            var fileInfo = new FileInfo(absolutePath);
            using var fileStream = fileInfo.Open(FileMode.Create);
            using var streamWriter = new StreamWriter(fileStream);
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            while (runtimePipe.StreamEnumerator.MoveNext())
            {
                streamWriter.WriteLine(runtimePipe.StreamEnumerator.Current);

                // Flush at least every 500ms
                if (stopwatch.ElapsedTicks > TimeSpan.TicksPerSecond / 2)
                {
                    stopwatch.Reset();
                    streamWriter.Flush();
                }
            }

            stopwatch.Stop();

            return;
        }

        var runtimeString = content.As<RuntimeString>();
        File.WriteAllText(absolutePath, runtimeString.Value);
    }

    /// <summary>Appends the provided text on its own line to a file.</summary>
    /// <param name="content">Text that should be written to the file</param>
    /// <param name="path">A file path</param>
    /// <returns>nil</returns>
    [ElkFunction("append", Reachability.Everywhere)]
    public static void AppendToFile(RuntimeObject content, RuntimeString path, ShellEnvironment env)
    {
        string absolutePath = env.GetAbsolutePath(path.Value);
        var fileInfo = new FileInfo(absolutePath);

        if (content is RuntimePipe runtimePipe)
        {
            using var fileStream = fileInfo.Open(FileMode.Append);
            using var streamWriter = new StreamWriter(fileStream);
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            while (runtimePipe.StreamEnumerator.MoveNext())
            {
                streamWriter.WriteLine(runtimePipe.StreamEnumerator.Current);

                // Flush at least every 500ms
                if (stopwatch.ElapsedTicks > TimeSpan.TicksPerSecond / 2)
                {
                    stopwatch.Reset();
                    streamWriter.Flush();
                }
            }

            stopwatch.Stop();

            return;
        }

        var runtimeString = content.As<RuntimeString>();
        if (fileInfo.Exists)
        {
            using var writer = new StreamWriter(fileInfo.Open(FileMode.Append));
            writer.Write(System.Environment.NewLine);
            writer.Write(runtimeString.Value);
        }
        else
        {
            File.AppendAllText(absolutePath, runtimeString.Value);
        }
    }

    /// <summary>Appends the provided text to a file *without* putting it on it's own line.</summary>
    /// <param name="content">Text that should be written to the file</param>
    /// <param name="path">A file path</param>
    /// <returns>nil</returns>
    [ElkFunction("appendEnd", Reachability.Everywhere)]
    public static void AppendEnd(RuntimeString content, RuntimeString path, ShellEnvironment env)
        => File.AppendAllText(env.GetAbsolutePath(path.Value), content.Value);

    /// <summary>Reads the next line from the standard input stream. This is used to get input from the user in a terminal.</summary>
    /// <param name="prompt">Text that should be printed before the input prompt</param>
    /// <returns>The value given by the user.</returns>
    [ElkFunction("input")]
    public static RuntimeString Input(RuntimeString? prompt = null)
    {
        if (prompt != null)
            Console.Write(prompt.Value);

        return new RuntimeString(Console.ReadLine() ?? "");
    }

    /// <summary>
    /// Waits until the user presses a key in the terminal.
    /// </summary>
    /// <returns>The character produced by the key press.</returns>
    [ElkFunction("getKey")]
    public static RuntimeString GetKey()
    {
        return new RuntimeString(Console.ReadKey(true).KeyChar.ToString());
    }

    /// <summary>
    /// Prints the given value to the terminal, without adding a new line to the end.
    /// If the input value is of the type Error, the error message is forwarded to stderr instead of stdout.
    /// </summary>
    /// <param name="input">Value to print</param>
    [ElkFunction("print", Reachability.Everywhere)]
    public static void Print(RuntimeObject input)
    {
        if (input is RuntimeError err)
        {
            Console.Error.Write(err.Value);
        }
        else
        {
            Console.Write(input.As<RuntimeString>().Value);
        }
    }

    /// <summary>
    /// Prints the given value to the terminal while also adding a new line to the end.
    /// If the input value is of the type Error, the error message is forwarded to stderr instead of stdout.
    /// </summary>
    /// <param name="input">Value to print</param>
    [ElkFunction("println", Reachability.Everywhere)]
    public static void PrintLine(RuntimeObject input)
    {
        if (input is RuntimeError err)
        {
            Console.Error.WriteLine(err.Value);
        }
        else
        {
            Console.WriteLine(input.As<RuntimeString>().Value);
        }
    }
}