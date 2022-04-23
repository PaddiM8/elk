using System;

namespace Elk.Attributes;

class ShellFunctionAttribute : Attribute
{
    public string Name { get; }

    public ShellFunctionAttribute(string name)
    {
        Name = name;
    }
}