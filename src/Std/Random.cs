using Elk.Attributes;
using Elk.Interpreting;
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

namespace Elk.Std;

static class Random
{
    private static readonly System.Random _rand = new();

    [ElkFunction("random", Reachability.Everywhere)]
    public static RuntimeFloat Next(IRuntimeValue from, IRuntimeValue to)
        => new(_rand.Next((int)from.As<RuntimeInteger>().Value, (int)to.As<RuntimeInteger>().Value));
}