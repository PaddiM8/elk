using System;
using System.IO;

namespace Elk.Interpreting;

class ShellEnvironment
{
    public string WorkingDirectory { get; set; }

    public ShellEnvironment()
    {
        WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    public string GetAbsolutePath(string relativePath)
        => Path.Combine(WorkingDirectory, relativePath);
}