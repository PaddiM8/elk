using System;
using Elk.Attributes;
using Elk.Interpreting;

namespace Elk.Std;

static class Random
{
    private static readonly System.Random _rand = new();

    [ShellFunction("random")]
    public static RuntimeFloat Next(IRuntimeValue from, IRuntimeValue to)
        => new(_rand.Next(from.As<RuntimeInteger>().Value, to.As<RuntimeInteger>().Value));
}