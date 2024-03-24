using Elk.Interpreting;
using Elk.Interpreting.Exceptions;
using Elk.Std.Attributes;
using Elk.Std.DataTypes;

namespace Elk.Std;

[ElkModule("dir")]
static class Directory
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

    [ElkFunction("exists")]
    public static RuntimeBoolean Exists(RuntimeString path, ShellEnvironment env)
        => RuntimeBoolean.From(System.IO.Directory.Exists(env.GetAbsolutePath(path.Value)));
}