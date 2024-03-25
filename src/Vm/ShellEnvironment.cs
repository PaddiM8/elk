#region

using System;
using System.Collections.Generic;
using System.IO;
using Elk.Std.DataTypes;

#endregion

namespace Elk.Vm;

class ShellEnvironment(string? scriptPath)
{
    public static string WorkingDirectory
    {
        get => Environment.GetEnvironmentVariable("PWD")
            ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        set => Environment.SetEnvironmentVariable("PWD", value);
    }

    public IEnumerable<RuntimeObject> Argv { get; set; } = Array.Empty<RuntimeObject>();

    public string? ScriptPath { get; } = scriptPath;

    public string GetAbsolutePath(string relativePath)
        => Path.GetFullPath(Path.Combine(WorkingDirectory, relativePath));

}