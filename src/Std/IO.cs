using System.IO;
using Shel.Interpreting;

namespace Shel.Std;

static class IO
{
    [ShellFunction("read")]
    public static RuntimeString ReadFile(RuntimeString path)
        => new(File.ReadAllText(path.Value));
}