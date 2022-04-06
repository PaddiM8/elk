using System;
using Shel.Attributes;
using Shel.Interpreting;

namespace Shel.Std;

static class Random
{
    private static readonly System.Random _rand = new();

    [ShellFunction("random")]
    public static RuntimeFloat Next(IRuntimeValue from, IRuntimeValue to)
        => new(_rand.Next(from.As<RuntimeInteger>().Value, to.As<RuntimeInteger>().Value));
}