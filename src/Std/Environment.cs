#region

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Elk.Std.Attributes;
using Elk.Std.DataTypes;

#endregion

namespace Elk.Std;

[ElkModule("env")]
public class Environment
{
    /// <summary>
    /// Exits the program.
    /// <param name="exitCode">The exit code (default: 0)</param>
    /// </summary>
    [ElkFunction("exit", Reachability.Everywhere)]
    public static void Exit(RuntimeInteger? exitCode = null)
    {
        System.Environment.Exit((int?)exitCode?.Value ?? 0);
    }

    /// <returns>A string containing a modified version of the path to the current directory (the value of $PWD). The names of all the directories in the path except for the last one are replaced with their first letter, and '/home/user' is replaced with a tilde.</returns>
    /// <example>assert(prettyPwd() == "~/P/e/src")</example>
    [ElkFunction("prettyPwd")]
    public static RuntimeString PrettyPwd()
    {
        string homePath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
        string? pwd = System.Environment.GetEnvironmentVariable("PWD");
        if (string.IsNullOrEmpty(pwd))
            pwd = homePath;

        bool containsHome = false;
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
            return new();

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