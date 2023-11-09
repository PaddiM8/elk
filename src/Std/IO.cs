#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
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
        => new(System.IO.File.ReadAllText(env.GetAbsolutePath(path.Value)));

    /// <summary>Writes the provided text to a file, overwriting any previous content.</summary>
    /// <param name="content">Text that should be written to the file</param>
    /// <param name="path">A file path</param>
    /// <returns>nil</returns>
    [ElkFunction("write", Reachability.Everywhere, ConsumesPipe = true)]
    public static void WriteToFile(RuntimeObject content, RuntimeString path, ShellEnvironment env)
    {
        var absolutePath = env.GetAbsolutePath(path.Value);
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
        System.IO.File.WriteAllText(absolutePath, runtimeString.Value);
    }

    /// <summary>Appends the provided text on its own line to a file.</summary>
    /// <param name="content">Text that should be written to the file</param>
    /// <param name="path">A file path</param>
    /// <returns>nil</returns>
    [ElkFunction("append", Reachability.Everywhere)]
    public static void AppendToFile(RuntimeObject content, RuntimeString path, ShellEnvironment env)
    {
        var absolutePath = env.GetAbsolutePath(path.Value);
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
            System.IO.File.AppendAllText(absolutePath, runtimeString.Value);
        }
    }

    /// <summary>Appends the provided text to a file *without* putting it on it's own line.</summary>
    /// <param name="content">Text that should be written to the file</param>
    /// <param name="path">A file path</param>
    /// <returns>nil</returns>
    [ElkFunction("appendEnd", Reachability.Everywhere)]
    public static void AppendEnd(RuntimeString content, RuntimeString path, ShellEnvironment env)
        => System.IO.File.AppendAllText(env.GetAbsolutePath(path.Value), content.Value);

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
    /// <param name="intercept">(default: true) Whether or not to prevent the key from being displayed in the terminal</param>
    /// <returns>
    /// A dictionary containing information about the pressed character.
    /// {
    ///     "key": name of key,
    ///     "modifiers": [
    ///         list of modifiers
    ///     ],
    /// }
    /// </returns>
    [ElkFunction("readKey")]
    public static RuntimeDictionary ReadKey(RuntimeBoolean? intercept = null)
    {
        var keyInfo = Console.ReadKey(intercept?.IsTrue ?? true);
        var key = keyInfo.Key switch
        {
            ConsoleKey.Backspace => "backspace",
            ConsoleKey.Delete => "delete",
            ConsoleKey.DownArrow => "down",
            ConsoleKey.End => "end",
            ConsoleKey.Enter => "enter",
            ConsoleKey.Escape => "escape",
            >= ConsoleKey.F1 and <= ConsoleKey.F23 => keyInfo.Key.ToString(),
            ConsoleKey.Home => "home",
            ConsoleKey.Insert => "insert",
            ConsoleKey.LeftArrow => "left",
            ConsoleKey.RightArrow => "right",
            ConsoleKey.Spacebar => "space",
            ConsoleKey.Tab => "tab",
            ConsoleKey.UpArrow => "up",
            _ => keyInfo.KeyChar.ToString(),
        };

        var modifiers = new List<RuntimeString>();
        if (keyInfo.Modifiers.HasFlag(ConsoleModifiers.Alt))
            modifiers.Add(new RuntimeString("alt"));

        if (keyInfo.Modifiers.HasFlag(ConsoleModifiers.Control))
            modifiers.Add(new RuntimeString("ctrl"));

        if (keyInfo.Modifiers.HasFlag(ConsoleModifiers.Shift))
            modifiers.Add(new RuntimeString("shift"));

        return new RuntimeDictionary
        {
            ["key"] = new RuntimeString(key),
            ["modifiers"] = new RuntimeList(modifiers),
        };
    }

    /// <summary>
    /// Prints the given value to the terminal, without adding a new line to the end.
    /// If the input value is of the type Error, the error message is forwarded to stderr instead of stdout.
    /// </summary>
    /// <param name="input">Value to print</param>
    [ElkFunction("print", Reachability.Everywhere)]
    public static void Print([ElkVariadic] IEnumerable<RuntimeObject> input)
        => PrintHelper(input, isLine: false, isError: false);

    /// <summary>
    /// Prints the given value to stderr, without adding a new line to the end.
    /// </summary>
    /// <param name="input">Value to print</param>
    [ElkFunction("printError", Reachability.Everywhere)]
    public static void PrintError([ElkVariadic] IEnumerable<RuntimeObject> input)
        => PrintHelper(input, isLine: false, isError: true);

    /// <summary>
    /// Prints the given value to the terminal while also adding a new line to the end.
    /// If the input value is of the type Error, the error message is forwarded to stderr instead of stdout.
    /// </summary>
    /// <param name="input">Value to print</param>
    [ElkFunction("println", Reachability.Everywhere)]
    public static void PrintLine([ElkVariadic] IEnumerable<RuntimeObject> input)
        => PrintHelper(input, isLine: true, isError: false);

    /// <summary>
    /// Prints the given value to stderr.
    /// </summary>
    /// <param name="input">Value to print</param>
    [ElkFunction("printlnError", Reachability.Everywhere)]
    public static void PrintLineError([ElkVariadic] IEnumerable<RuntimeObject> input)
        => PrintHelper(input, isLine: true, isError: true);

    private static void PrintHelper(IEnumerable<RuntimeObject> input, bool isLine, bool isError)
    {
        var builder = new StringBuilder();
        foreach (var value in input)
        {
            builder.Append(value.As<RuntimeString>().Value);
            builder.Append(' ');
        }

        if (builder.Length > 0)
            builder.Remove(builder.Length - 1, 1);

        Action<string> textWriter = (isLine, isError) switch
        {
            (true, true) => Console.Error.WriteLine,
            (false, true) => Console.Error.Write,
            (true, false) => Console.WriteLine,
            (false, false) => Console.Write,
        };

        textWriter(builder.ToString());
    }
}