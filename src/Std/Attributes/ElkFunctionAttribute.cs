#region

using System;

#endregion

namespace Elk.Std.Attributes;

public enum Reachability
{
    Module,
    Everywhere,
}

[AttributeUsage(AttributeTargets.Method)]
public class ElkFunctionAttribute : Attribute
{
    public string Name { get; }

    public Reachability Reachability { get; }

    public ElkFunctionAttribute(string name,
                                Reachability reachability = Reachability.Module)
    {
        Name = name;
        Reachability = reachability;
    }
}