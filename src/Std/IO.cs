using System;
using System.IO;
using Shel.Attributes;
using Shel.Interpreting;

namespace Shel.Std;

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
    public static RuntimeString Input(RuntimeString prompt)
    {
        Console.Write(prompt.Value);

        return new RuntimeString(Console.ReadLine() ?? "");
    }

    [ShellFunction("print")]
    public static void Print(RuntimeString input)
    {
        Console.Write(input.Value);
    }
}