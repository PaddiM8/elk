using System.IO;
using System.Linq;
using Elk.Attributes;
using Elk.Interpreting;
using Elk.Interpreting.Exceptions;

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

namespace Elk.Std;

[ElkModule("path")]
public class Path
{
    [ElkFunction("add")]
    public static void Add(RuntimeString path)
    {
        File.AppendAllText(CommonPaths.PathFile, $"{path}\n");

        // Reload the path variable
        string pathVar = System.Environment.GetEnvironmentVariable("PATH") ?? "";
        string colon = pathVar == "" ? "" : ":";
        System.Environment.SetEnvironmentVariable("PATH", pathVar + colon + path);
    }

    [ElkFunction("all")]
    public static RuntimeList All()
    {
        if (!File.Exists(CommonPaths.PathFile))
            return new(new IRuntimeValue[] {});

        var lines = File.ReadAllLines(CommonPaths.PathFile)
            .Select(x => new RuntimeString(x));

        return new(lines);
    }

    [ElkFunction("list")]
    public static RuntimeString List()
    {
        if (!File.Exists(CommonPaths.PathFile))
            return new("");

        var lines = File.ReadAllLines(CommonPaths.PathFile)
            .Select((x, i) => $"{i}. {x}");

        return new(string.Join("\n", lines));
    }

    [ElkFunction("remove")]
    public static void Remove(RuntimeInteger index)
    {
        if (!File.Exists(CommonPaths.PathFile))
            throw new RuntimeItemNotFoundException(index.ToString());

        var lines = File.ReadAllLines(CommonPaths.PathFile).ToList();
        if (index.Value >= lines.Count)
            throw new RuntimeItemNotFoundException(index.ToString());

        lines.RemoveAt(index.Value);
        File.WriteAllLines(CommonPaths.PathFile, lines);
    }
}