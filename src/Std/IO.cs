using System;
using System.IO;
using Elk.Attributes;
using Elk.Interpreting;
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

namespace Elk.Std;

// ReSharper disable once InconsistentNaming

[ElkModule("io")]
static class IO
{
    [ElkFunction("read", Reachability.Everywhere)]
    public static RuntimeString ReadFile(RuntimeString path, ShellEnvironment env)
        => new(File.ReadAllText(env.GetAbsolutePath(path.Value)));

    [ElkFunction("write", Reachability.Everywhere)]
    public static void WriteToFile(RuntimeString content, RuntimeString path, ShellEnvironment env)
        => File.WriteAllText(env.GetAbsolutePath(path.Value), content.Value);

    [ElkFunction("append", Reachability.Everywhere)]
    public static void AppendToFile(RuntimeString content, RuntimeString path, ShellEnvironment env)
        => File.AppendAllText(env.GetAbsolutePath(path.Value), content.Value);

    [ElkFunction("input", Reachability.Everywhere)]
    public static RuntimeString Input(RuntimeString? prompt = null)
    {
        if (prompt != null)
            Console.Write(prompt.Value);

        return new RuntimeString(Console.ReadLine() ?? "");
    }

    [ElkFunction("getKey")]
    public static RuntimeString GetKey()
    {
        return new RuntimeString(Console.ReadKey(true).KeyChar.ToString());
    }

    [ElkFunction("print", Reachability.Everywhere)]
    public static void Print(RuntimeString input)
    {
        Console.Write(input);
    }
    
    [ElkFunction("println", Reachability.Everywhere)]
    public static void PrintLine(RuntimeString input)
    {
        Console.WriteLine(input);
    }
}