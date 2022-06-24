#region

using System;

#endregion

namespace Elk.Std.Attributes;

class ElkTypeAttribute : Attribute
{
    public string Name { get; }

    public ElkTypeAttribute(string name)
    {
        Name = name;
    }
}