#region

using System;

#endregion

namespace Elk.Std.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public class ElkModuleAttribute : Attribute
{
    public string Name { get; }

    public ElkModuleAttribute(string name)
    {
        Name = name;
    }
}