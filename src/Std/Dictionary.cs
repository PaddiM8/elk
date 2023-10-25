using System.Linq;
using Elk.Std.Attributes;
using Elk.Std.DataTypes;

namespace Elk.Std;

[ElkModule("dict")]
public static class Dictionary
{
    /// <returns>The keys in the given dictionary.</returns>
    [ElkFunction("keys")]
    public static RuntimeList Keys(RuntimeDictionary dictionary)
        => new(dictionary.Entries.Values.Select(x => x.Item1));

    /// <returns>The values in the given dictionary.</returns>
    [ElkFunction("values")]
    public static RuntimeList Values(RuntimeDictionary dictionary)
        => new(dictionary.Entries.Values.Select(x => x.Item2));
}