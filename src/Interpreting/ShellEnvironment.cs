#region

using System;
using System.IO;

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
}