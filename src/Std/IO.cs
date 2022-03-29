using System.IO;
using Shel.Attributes;
using Shel.Interpreting;

namespace Shel.Std;

static class IO
{
    [ShellFunction("read")]
    public static RuntimeString ReadFile(RuntimeString path, ShellEnvironment env)
        => new(File.ReadAllText(env.GetAbsolutePath(path.Value)));

    [ShellFunction("write")]
    public static void WriteToFile(RuntimeString path, RuntimeString content, ShellEnvironment env)
    {
        File.WriteAllText(env.GetAbsolutePath(path.Value), content.Value);
    }
}