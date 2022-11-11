

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

#region

using Elk.Std.Attributes;
using Elk.Std.DataTypes;

#endregion

namespace Elk.Std;

[ElkModule("random")]
static class Random
{
    private static readonly System.Random _rand = new();

    /// <returns>A random integer between the two provided values.</returns>
    [ElkFunction("random", Reachability.Everywhere)]
    public static RuntimeFloat Next(RuntimeInteger from, RuntimeInteger to)
        => new(_rand.Next((int)from.Value, (int)to.Value));
}