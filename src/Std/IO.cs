using System;
using System.IO;
using Elk.Attributes;
using Elk.Interpreting;
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

namespace Elk.Std;

// ReSharper disable once InconsistentNaming
static class IO
{
    [ShellFunction("read")]
    public static RuntimeString ReadFile(RuntimeString path, ShellEnvironment env)
        => new(File.ReadAllText(env.GetAbsolutePath(path.Value)));

    [ShellFunction("write")]
    public static void WriteToFile(RuntimeString content, RuntimeString path, ShellEnvironment env)
        => File.WriteAllText(env.GetAbsolutePath(path.Value), content.Value);

    [ShellFunction("append")]
    public static void AppendToFile(RuntimeString content, RuntimeString path, ShellEnvironment env)
        => File.AppendAllText(env.GetAbsolutePath(path.Value), content.Value);

    [ShellFunction("input")]
    public static RuntimeString Input(RuntimeString? prompt = null)
    {
        if (prompt != null)
            Console.Write(prompt.Value);

        return new RuntimeString(Console.ReadLine() ?? "");
    }

    [ShellFunction("getKey")]
    public static RuntimeString GetKey()
    {
        return new RuntimeString(Console.ReadKey(true).KeyChar.ToString());
    }

    [ShellFunction("print")]
    public static void Print(RuntimeString input)
    {
        Console.Write(input);
    }
    
    [ShellFunction("println")]
    public static void PrintLine(RuntimeString input)
    {
        Console.WriteLine(input);
    }
}