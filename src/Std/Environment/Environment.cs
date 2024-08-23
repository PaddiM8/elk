#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Elk.Analysis;
using Elk.Exceptions;
using Elk.Scoping;
using Elk.Std.Attributes;
using Elk.Std.DataTypes;
using Elk.Vm;

#endregion

namespace Elk.Std.Environment;

#pragma warning disable CS1573

[ElkModule("env")]
static class Environment
{
    [ElkFunction("cd", Reachability.Everywhere)]
    public static void Cd(RuntimeString? path = null, ShellEnvironment? env = null)
    {
        if (path == null)
        {
            ShellEnvironment.WorkingDirectory = "";

            return;
        }

        var absolutePath = path.Value == "-"
            ? System.Environment.GetEnvironmentVariable("OLDPWD") ?? ""
            : env!.GetAbsolutePath(path.Value);

        if (!System.IO.Directory.Exists(absolutePath))
            throw new RuntimeException($"cd: The directory \"{path}\" does not exist");

        System.Environment.SetEnvironmentVariable("OLDPWD", ShellEnvironment.WorkingDirectory);
        ShellEnvironment.WorkingDirectory = absolutePath;
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
        var virtualMachine = new VirtualMachine(
            new RootModuleScope(null, null),
            new VirtualMachineOptions()
        );
        if (env != null)
        {
            foreach (var (_, (key, value)) in env.Entries)
            {
                virtualMachine.AddGlobalVariable(
                    key.As<RuntimeString>().Value,
                    value
                );
            }
        }

        var result = ElkProgram.Evaluate(
            input.Value,
            virtualMachine.RootModule,
            AnalysisScope.OncePerModule,
            virtualMachine
        );
        if (result.Diagnostics.Any())
            throw new RuntimeException(result.Diagnostics.FirstOrDefault()?.Message ?? "Eval error.");

        return result.Value ?? RuntimeNil.Value;
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
    public static RuntimeGenerator Expand(RuntimeString pattern)
        => new(
            Globbing.Glob(ShellEnvironment.WorkingDirectory, pattern.Value)
                .Select(x => new RuntimeString(x))
        );

    /// <param name="index">The index of the argument to get</param>
    /// <returns>The list of command line arguments passed to the program, or a specific one if an index was specified</returns>
    [ElkFunction("getArgv", Reachability.Everywhere)]
    public static RuntimeObject GetArgv(RuntimeInteger? index = null, ShellEnvironment env = null!)
    {
        if (index != null)
            return env.Argv.ElementAtOrDefault((int)index.Value) ?? RuntimeNil.Value;

        return new RuntimeList(env.Argv.ToList());
    }

    /// <param name="options">A dictionary such as the one shown in the example below.</param>
    /// <returns>
    /// A string containing a modified version of the path to the current directory (the value of $PWD).
    /// The names of all the directories in the path except for the last one are replaced with their first
    /// letter, and '/home/user' is replaced with a tilde.
    /// </returns>
    /// <example>
    /// prettyPwd() #=> ~/P/e/src
    ///
    /// prettyPwd({
    ///     "wordColor": "brightBlue",    # The color of the folder names (see: `ansi::color`)
    ///     "slashColor": "blue",         # The color of the slashes (and tilde)
    ///     "wholeFolderNameAmount": "2", # The amount of folder names that should *not* be shortened (default: 1)
    /// })
    /// # => ~/P/elk/src
    /// </example>
    [ElkFunction("prettyPwd")]
    public static RuntimeString PrettyPwd(RuntimeDictionary? options = null)
    {
        var homePath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
        var pwd = ShellEnvironment.WorkingDirectory;
        if (string.IsNullOrEmpty(pwd))
            pwd = homePath;

        var containsHome = false;
        if (pwd.StartsWith(homePath))
        {
            containsHome = true;
            pwd = pwd[homePath.Length..];
        }

        var slashColor = options?.GetValue<RuntimeString>("slashColor");
        var tilde = slashColor == null
            ? "~"
            : Ansi.Color("~", slashColor.Value);
        var slash = slashColor == null
            ? "/"
            : Ansi.Color("/", slashColor.Value);

        if (pwd == "/")
            return new RuntimeString(slash);

        var directoryNames = GetDirectoryNames(pwd);
        var pathRoot = "/";
        if (pwd.Length > 0)
        {
            pathRoot = System.IO.Path.GetPathRoot(System.IO.Path.GetFullPath(pwd))?
                .Replace('\\', '/') ?? "";
        }

        if (directoryNames.Count == 0)
        {
            return new RuntimeString(
                containsHome
                    ? tilde
                    : pathRoot.Replace("/", slash)
            );
        }

        var wordColor = options?.GetValue<RuntimeString>("wordColor")?.Value;
        var wholeFolderNameAmount = (int?)options?.GetValue<RuntimeInteger>("wholeFolderNameAmount")?.Value ?? 1;
        var shortenedFolderNameAmount = directoryNames.Count - wholeFolderNameAmount;
        var shortenedFolderNames = directoryNames
            .Take(shortenedFolderNameAmount)
            .Select(x => FormatDirectoryName(x, wordColor, shorten: true));
        var fullFolderNames = directoryNames
            .Skip(shortenedFolderNameAmount)
            .Take(wholeFolderNameAmount)
            .Select(x => FormatDirectoryName(x, wordColor, shorten: false));

        var formattedFolderNames = shortenedFolderNames.Concat(fullFolderNames);
        var formatted = string.Join(slash, formattedFolderNames);
        formatted = containsHome
            ? tilde + slash + formatted
            : pathRoot + formatted;

        return new RuntimeString(formatted);
    }

    private static string FormatDirectoryName(string name, string? color, bool shorten)
    {
        var shortened = shorten
            ? name[0].ToString()
            : name;

        return color == null
            ? shortened
            : Ansi.Color(shortened, color);
    }

    [ElkFunction("scriptPath", Reachability.Everywhere)]
    public static RuntimeString ScriptPath(ShellEnvironment env)
    {
        var path = env.ScriptPath == null
            ? ShellEnvironment.WorkingDirectory
            : System.IO.Path.GetDirectoryName(env.ScriptPath)!;

        return new RuntimeString(path);
    }

    /// <returns>
    /// The a human-readable string showing the amount of time it took to evaluate the given closure.
    /// </returns>
    [ElkFunction("time", Reachability.Everywhere)]
    public static RuntimeString Time(Func<RuntimeObject> closure)
    {
        var stopwatch = new Stopwatch();
        stopwatch.Start();
        closure();
        stopwatch.Stop();

        var milliseconds = Math.Max(0, stopwatch.ElapsedMilliseconds - stopwatch.Elapsed.Seconds * 1000);
        var paddedMilliseconds = milliseconds.ToString().PadLeft(2, '0');

        return new RuntimeString($"elapsed: {stopwatch.Elapsed.Minutes}m{stopwatch.Elapsed.Seconds}.{paddedMilliseconds}");
    }

    /// <returns>The amount of time it took to evaluate the given closure, in milliseconds.</returns>
    [ElkFunction("timeMs", Reachability.Everywhere)]
    public static RuntimeInteger TimeMs(Func<RuntimeObject> closure)
    {
        var stopwatch = new Stopwatch();
        stopwatch.Start();
        closure();
        stopwatch.Stop();

        return new RuntimeInteger(stopwatch.ElapsedMilliseconds);
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