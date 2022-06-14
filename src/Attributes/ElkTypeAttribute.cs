using System;

namespace Elk.Attributes;

class ElkTypeAttribute : Attribute
{
    public string Name { get; }

    public ElkTypeAttribute(string name)
    {
        Name = name;
    }
}