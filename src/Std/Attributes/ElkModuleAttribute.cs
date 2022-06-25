#region

using System;

#endregion

namespace Elk.Std.Attributes;

public class ElkModuleAttribute : Attribute
{
    public string Name { get; }

    public ElkModuleAttribute(string name)
    {
        Name = name;
    }
}