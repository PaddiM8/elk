#region

using System;

#endregion

namespace Elk.Std.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
public class ElkTypeAttribute : Attribute
{
    public string Name { get; }

    public ElkTypeAttribute(string name)
    {
        Name = name;
    }
}