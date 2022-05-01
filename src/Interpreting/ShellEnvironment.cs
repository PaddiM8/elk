using System;
using System.IO;

namespace Elk.Interpreting;

class ShellEnvironment
{
    public string WorkingDirectory
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