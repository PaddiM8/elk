using System;
using System.Collections.Generic;
using System.Linq;
using Elk.Std.Attributes;
using Elk.Std.DataTypes;

namespace Elk.Std.String;

[ElkModule("str::path")]
public static class StringPath
{
    /// <param name="path1">An absolute or relative path</param>
    /// <param name="path2">A relative path</param>
    /// <returns>A path created by concatenating the given paths.</returns>
    [ElkFunction("join")]
    public static RuntimeString Join(RuntimeString path1, RuntimeString path2)
        => new(System.IO.Path.Join(path1.Value, path2.Value));

    [ElkFunction("fileName")]
    public static RuntimeString FileName(RuntimeString path)
        => new(System.IO.Path.GetFileName(path.Value));

    /// <returns>A relative version of 'entirePath' without the base path</returns>
    /// <example>
    /// str::path::relative("/a/b", "a/b/c") #=> "c"
    /// </example>
    [ElkFunction("relative")]
    public static RuntimeString Relative(RuntimeString basePath, RuntimeString entirePath)
        => entirePath.Value.StartsWith(basePath.Value)
            ? new(entirePath.Value[basePath.Value.Length..].TrimStart('/'))
            : entirePath;

    [ElkFunction("fuzzyFind")]
    public static RuntimeList FuzzyFind(IEnumerable<RuntimeObject> paths, RuntimeString query)
    {
        var pathStrings = paths.Select(x => x.As<RuntimeString>());
        var exactStart = pathStrings.Where(x =>
            x.Value.StartsWith(query.Value)
        );
        var exactContains = pathStrings.Where(x =>
            x.Value.Contains(query.Value)
        );
        var insensitiveContains = pathStrings.Where(x =>
            x.Value.Contains(query.Value, StringComparison.OrdinalIgnoreCase)
        );

        return new(exactStart.Union(exactContains).Union(insensitiveContains));
    }
}
