#region

using System;

#endregion

namespace Elk.Std.Attributes;

[AttributeUsage(AttributeTargets.Method)]
public class ElkStructAttribute : Attribute
{
    public string Name { get; }

    public ElkStructAttribute(string name) {
        Name = name;
    }
}