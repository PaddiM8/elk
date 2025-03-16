#region

using System.Linq;
using Elk.Exceptions;
using Elk.Std.Attributes;
using Elk.Std.DataTypes;
using Elk.Vm;

#endregion

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

namespace Elk.Std.Environment;

[ElkModule("env::path")]
public static class Path
{
    /// <summary>
    /// Adds the provided path to $PATH and ~/.config/elk/path.txt.
    /// Paths in path.txt are automatically added to $PATH each Elk session.
    /// </summary>
    /// <param name="path">Path to add</param>
    [ElkFunction("add")]
    public static void Add(RuntimeString path)
    {
        var absolutePath = ShellEnvironment.GetAbsolutePath(path.Value);
        System.IO.File.AppendAllText(CommonPaths.PathFile, $"{absolutePath}{System.Environment.NewLine}");

        // Reload the path variable
        var pathVar = System.Environment.GetEnvironmentVariable("PATH") ?? "";
        var colon = pathVar == "" ? "" : ":";
        System.Environment.SetEnvironmentVariable("PATH", path + colon + pathVar);
    }

    /// <returns>A list of all the paths in ~/.config/elk/path.txt.</returns>
    [ElkFunction("all")]
    public static RuntimeList All()
    {
        if (!System.IO.File.Exists(CommonPaths.PathFile))
            return new([]);

        var lines = System.IO.File.ReadAllLines(CommonPaths.PathFile)
            .Select<string, RuntimeObject>(x => new RuntimeString(x))
            .ToList();

        return new(lines);
    }

    /// <returns>A string displaying all the paths in ~/.config/elk/path.txt together with their index.</returns>
    [ElkFunction("list")]
    public static RuntimeString List()
    {
        if (!System.IO.File.Exists(CommonPaths.PathFile))
            return new RuntimeString("");

        var lines = System.IO.File.ReadAllLines(CommonPaths.PathFile)
            .Select((x, i) => $"{i}. {x}");

        return new RuntimeString(string.Join(System.Environment.NewLine, lines));
    }

    /// <summary>Removes the path of the given index from $PATH and ~/.config/elk/path.txt. The index can be found with the help of the path::list function.</summary>
    /// <throws>If the index is out of range.</throws>
    [ElkFunction("remove")]
    public static void Remove(RuntimeInteger index)
    {
        if (!System.IO.File.Exists(CommonPaths.PathFile))
            throw new RuntimeStdException("Index out of range");

        var lines = System.IO.File.ReadAllLines(CommonPaths.PathFile).ToList();
        if (index.Value >= lines.Count)
            throw new RuntimeStdException("Index out of range");

        lines.RemoveAt((int)index.Value);
        System.IO.File.WriteAllLines(CommonPaths.PathFile, lines);
    }
}