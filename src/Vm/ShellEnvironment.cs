#region

using System;
using System.Collections.Generic;
using System.IO;
using Elk.Std.DataTypes;

#endregion

namespace Elk.Vm;

public class ShellEnvironment(string? scriptPath)
{
    public static string WorkingDirectory
    {
        get => Directory.GetCurrentDirectory();
        set
        {
            var path = value == string.Empty
                ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                : value;
            Directory.SetCurrentDirectory(path);
            Environment.SetEnvironmentVariable("PWD", value);
        }
    }

    public IEnumerable<RuntimeObject> Argv { get; set; } = Array.Empty<RuntimeObject>();

    public string? ScriptPath { get; set; } = scriptPath;

    public string GetAbsolutePath(string relativePath)
        => Path.GetFullPath(Path.Combine(WorkingDirectory, relativePath));

}