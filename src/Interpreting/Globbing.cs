using System.Collections.Generic;
using System.Linq;

namespace Elk.Interpreting;

public static class Globbing
{
    public static IEnumerable<string> Glob(string workingDirectory, string pattern)
    {
        var prefix = pattern.StartsWith('/') ? "/" : "";

        return GlobExpressions.Glob.FilesAndDirectories(
            prefix == "/" ? "/" : workingDirectory,
            pattern.TrimCharStart('/')
        )
        .Select(x => prefix + x);
    }
}