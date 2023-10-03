#region

using System;
using System.Collections.Generic;
using System.IO;
using Elk.Std.DataTypes;

#endregion

namespace Elk.Interpreting;

class ShellEnvironment
{
    public static string WorkingDirectory
    {
        get
        {
            return Environment.GetEnvironmentVariable("PWD")
                ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        set
        {
            Environment.SetEnvironmentVariable("PWD", value);
        }
    }

    public string GetAbsolutePath(string relativePath)
        => Path.GetFullPath(Path.Combine(WorkingDirectory, relativePath));

    public IEnumerable<RuntimeObject> Argv { get; set; } = Array.Empty<RuntimeObject>();
}