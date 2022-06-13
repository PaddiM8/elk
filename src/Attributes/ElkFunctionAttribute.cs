using System;

namespace Elk.Attributes;

enum Reachability
{
    Module,
    Everywhere,
}

class ElkFunctionAttribute : Attribute
{
    public string Name { get; }

    public Reachability Reachability { get; }

    public ElkFunctionAttribute(string name, Reachability reachability = Reachability.Module)
    {
        Name = name;
        Reachability = reachability;
    }
}