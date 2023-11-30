using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Elk.Interpreting;
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
        => new(Path.Join(path1.Value, path2.Value));

    [ElkFunction("fileName")]
    public static RuntimeString FileName(RuntimeString path)
        => new(Path.GetFileName(path.Value));

    /// <returns>The absolute path for the given path string.</returns>
    [ElkFunction("full")]
    public static RuntimeString Full(RuntimeString path)
    {
        var absolute = path.Value.FirstOrDefault() switch
        {
            '/' => path.Value,
            '~' => Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
                path.Value[2..]
            ),
            _ => Path.Combine(ShellEnvironment.WorkingDirectory, path.Value),
        };

        return new(new Uri(absolute).LocalPath);
    }

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

    /// <returns>A relative version of 'entirePath' without the base path</returns>
    /// <example>
    /// str::path::relative("/a/b", "a/b/c") #=> "c"
    /// </example>
    [ElkFunction("relative")]
    public static RuntimeString Relative(RuntimeString basePath, RuntimeString entirePath)
        => entirePath.Value.StartsWith(basePath.Value)
            ? new(entirePath.Value[basePath.Value.Length..].TrimStart('/'))
            : entirePath;
}
