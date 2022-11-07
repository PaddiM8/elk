#region

using System;
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
    public static void WriteToFile(RuntimeString content, RuntimeString path, ShellEnvironment env)
        => File.WriteAllText(env.GetAbsolutePath(path.Value), content.Value);

    /// <summary>Appends the provided text to a file.</summary>
    /// <param name="content">Text that should be written to the file</param>
    /// <param name="path">A file path</param>
    /// <returns>nil</returns>
    [ElkFunction("append", Reachability.Everywhere)]
    public static void AppendToFile(RuntimeString content, RuntimeString path, ShellEnvironment env)
        => File.AppendAllText(env.GetAbsolutePath(path.Value), content.Value);

    /// <summary>Reads the next line from the standard input stream. This is used to get input from the user in a terminal.</summary>
    /// <param name="prompt">Text that should be printed before the input prompt</param>
    /// <returns>The value given by the user.</returns>
    [ElkFunction("input", Reachability.Everywhere)]
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
    /// <param name="input">IsTrue to print</param>
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
    /// <param name="input">IsTrue to print</param>
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