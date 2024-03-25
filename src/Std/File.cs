using Elk.Std.Attributes;
using Elk.Std.DataTypes;
using Elk.Vm;

namespace Elk.Std;

[ElkModule("file")]
static class File
{
    [ElkFunction("exists")]
    public static RuntimeBoolean Exists(RuntimeString path, ShellEnvironment env)
        => RuntimeBoolean.From(System.IO.File.Exists(env.GetAbsolutePath(path.Value)));

    [ElkFunction("executableExists")]
    public static RuntimeBoolean ExecutableExists(RuntimeString path)
        => RuntimeBoolean.From(
            FileUtils.ExecutableExists(path.Value, ShellEnvironment.WorkingDirectory)
        );
}