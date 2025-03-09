using System.Linq;
using Elk.Std.Attributes;
using Elk.Std.DataTypes;
using Elk.Vm;

namespace Elk.Std;

[ElkModule("dir")]
static class Directory
{
    [ElkFunction("exists")]
    public static RuntimeBoolean Exists(RuntimeString path)
        => RuntimeBoolean.From(System.IO.Directory.Exists(ShellEnvironment.GetAbsolutePath(path.Value)));

    [ElkFunction("entries")]
    public static RuntimeList Entries(RuntimeString? path = null)
    {
        var currentDir = path?.Value ?? ShellEnvironment.WorkingDirectory;

        var files = System.IO.Directory
            .GetFileSystemEntries(ShellEnvironment.GetAbsolutePath(currentDir))
            .Select<string, RuntimeObject>(x => new RuntimeString(x))
            .ToList();

        return new RuntimeList(files);
    }

    [ElkFunction("dirs")]
    public static RuntimeList Dirs(RuntimeString? path = null)
    {
        var currentDir = path?.Value ?? ShellEnvironment.WorkingDirectory;

        var files = System.IO.Directory
            .GetDirectories(ShellEnvironment.GetAbsolutePath(currentDir))
            .Select<string, RuntimeObject>(x => new RuntimeString(x))
            .ToList();

        return new RuntimeList(files);
    }

    [ElkFunction("files")]
    public static RuntimeList Files(RuntimeString? path = null)
    {
        var currentDir = path?.Value ?? ShellEnvironment.WorkingDirectory;

        var files = System.IO.Directory
            .GetFiles(ShellEnvironment.GetAbsolutePath(currentDir))
            .Select<string, RuntimeObject>(x => new RuntimeString(x))
            .ToList();

        return new RuntimeList(files);
    }
}