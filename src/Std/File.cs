using Elk.Interpreting;
using Elk.Std.Attributes;
using Elk.Std.DataTypes;

namespace Elk.Std;

[ElkModule("file")]
static class File
{
    [ElkFunction("exists")]
    public static RuntimeBoolean Exists(RuntimeString path, ShellEnvironment env)
        => RuntimeBoolean.From(System.IO.File.Exists(env.GetAbsolutePath(path.Value)));
}