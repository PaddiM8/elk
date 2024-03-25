using Elk.Std.Attributes;
using Elk.Std.DataTypes;
using Elk.Vm;

namespace Elk.Std;

[ElkModule("dir")]
static class Directory
{
    [ElkFunction("exists")]
    public static RuntimeBoolean Exists(RuntimeString path, ShellEnvironment env)
        => RuntimeBoolean.From(System.IO.Directory.Exists(env.GetAbsolutePath(path.Value)));
}