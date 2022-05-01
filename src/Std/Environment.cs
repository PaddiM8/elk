using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Elk.Attributes;
using Elk.Interpreting;

namespace Elk.Std;

public class Environment
{
    [ShellFunction("prettyPwd")]
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