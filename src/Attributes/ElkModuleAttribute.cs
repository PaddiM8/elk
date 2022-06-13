using System;

namespace Elk.Attributes;

class ElkModuleAttribute : Attribute
{
    public string Name { get; }

    public ElkModuleAttribute(string name)
    {
        Name = name;
    }
}