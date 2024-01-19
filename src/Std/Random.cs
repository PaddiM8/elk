

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

#region

using System.Runtime.InteropServices;
using Elk.Std.Attributes;
using Elk.Std.DataTypes;

#endregion

namespace Elk.Std;

[ElkModule("random")]
static class Random
{
    private static readonly System.Random _rand = new();

    /// <returns>A random integer between the two provided values.</returns>
    [ElkFunction("next")]
    public static RuntimeInteger Next(RuntimeInteger from, RuntimeInteger to)
        => new(_rand.Next((int)from.Value, (int)to.Value));

    /// <summary>Shuffles the given list</summary>
    [ElkFunction("shuffle")]
    public static void Shuffle(RuntimeList list)
    {
        _rand.Shuffle(CollectionsMarshal.AsSpan(list.Values));
    }

    /// <summary>Pops a random element from the list.</summary>
    /// <returns>The removed element.</returns>
    [ElkFunction("pop")]
    public static RuntimeObject Pop(RuntimeList list)
    {
        var index = _rand.Next(list.Count - 1);
        var item = list.Values[index];
        list.Values.RemoveAt(index);

        return item;
    }
}