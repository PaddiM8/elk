using System;
using System.Collections.Generic;
using System.Linq;

namespace Elk.Interpreting;

public static class Globbing
{
    public static IEnumerable<string> Glob(string workingDirectory, string pattern)
    {
        var prefix = pattern.StartsWith('/') ? "/" : "";

        try
        {
            return GlobExpressions.Glob
                .FilesAndDirectories(
                    prefix == "/"
                        ? "/"
                        : workingDirectory,
                    pattern.TrimStart('/')
                )
                .Select(x => prefix + x);
        }
        catch
        {
            return Array.Empty<string>();
        }
    }
}