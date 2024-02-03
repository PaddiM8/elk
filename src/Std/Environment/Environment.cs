#region

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Elk.Analysis;
using Elk.Interpreting;
using Elk.Std.Attributes;
using Elk.Std.DataTypes;

#endregion

namespace Elk.Std.Environment;

#pragma warning disable CS1573

[ElkModule("env")]
static class Environment
{
    /// <param name="index">The index of the argument to get</param>
    /// <returns>The list of command line arguments passed to the program, or a specific one if an index was specified</returns>
    [ElkFunction("getArgv", Reachability.Everywhere)]
    public static RuntimeObject Argv(RuntimeInteger? index = null, ShellEnvironment env = null!)
    {
        if (index != null)
            return env.Argv.ElementAtOrDefault((int)index.Value) ?? RuntimeNil.Value;

        return new RuntimeList(env.Argv);
    }

    /// <summary>
    /// Evaluates the given string as Elk code.
    /// </summary>
    /// <param name="input">The input string</param>
    /// <param name="env">A dictionary with variables</param>
    /// <returns>The resulting value.</returns>
    [ElkFunction("eval", Reachability.Everywhere)]
    public static RuntimeObject Eval(RuntimeString input, RuntimeDictionary? env = null)
    {
        var interpreter = new Interpreter(null);
        if (env != null)
        {
            foreach (var (key, value) in env.Entries.Select(x => x.Value))
            {
                interpreter.CurrentModule.AddVariable(
                    key.As<RuntimeString>().Value,
                    value
                );
            }
        }

        return ElkProgram.Evaluate(
            input.Value,
            interpreter.CurrentModule,
            AnalysisScope.OncePerModule,
            interpreter
        ) ?? RuntimeNil.Value;
    }

    /// <summary>
    /// Exits the program.
    /// </summary>
    /// <param name="exitCode">The exit code (default: 0)</param>
    [ElkFunction("exit", Reachability.Everywhere)]
    public static void Exit(RuntimeInteger? exitCode = null)
    {
        System.Environment.Exit((int?)exitCode?.Value ?? 0);
    }

    /// <returns>The exit code (Integer) of the last exited process, or nil.</returns>
    [ElkFunction("exitCode")]
    public static RuntimeObject ExitCode()
    {
        var exitCode = System.Environment.GetEnvironmentVariable("?");

        return exitCode == null
            ? new RuntimeInteger(0)
            : new RuntimeInteger(int.Parse(exitCode));
    }

    /// <param name="pattern">The glob pattern to expand</param>
    /// <returns>Expands a glob string, eg. "**/*.elk"</returns>
    [ElkFunction("expand")]
    public static RuntimeList Expand(RuntimeString pattern)
        => new(
            Globbing.Glob(ShellEnvironment.WorkingDirectory, pattern.Value)
                .Select(x => new RuntimeString(x))
        );

    /// <returns>A string containing a modified version of the path to the current directory (the value of $PWD). The names of all the directories in the path except for the last one are replaced with their first letter, and '/home/user' is replaced with a tilde.</returns>
    /// <example>assert(prettyPwd() == "~/P/e/src")</example>
    [ElkFunction("prettyPwd")]
    public static RuntimeString PrettyPwd()
    {
        var homePath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
        var pwd = System.Environment.GetEnvironmentVariable("PWD");
        if (string.IsNullOrEmpty(pwd))
            pwd = homePath;

        var containsHome = false;
        if (pwd.StartsWith(homePath))
        {
            containsHome = true;
            pwd = pwd[homePath.Length..];
        }

        if (pwd == "/")
            return new("/");

        var directoryNames = GetDirectoryNames(pwd);
        if (directoryNames.Count == 0)
            return new(containsHome ? "~" : "");

        var shortenedPath = string.Join('/', directoryNames.Select(x => x[0]));
        shortenedPath = containsHome
            ? "~/" + shortenedPath
            : "/" + shortenedPath;

        return new(shortenedPath + directoryNames.Last()[1..]);
    }

    private static List<string> GetDirectoryNames(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return [];

        var directoryNames = new List<string>();
        var directoryInfo = new DirectoryInfo(path);
        while (directoryInfo.Parent != null)
        {
            directoryNames.Add(directoryInfo.Name);
            directoryInfo = directoryInfo.Parent;
        }

        directoryNames.Reverse();

        return directoryNames;
    }
}