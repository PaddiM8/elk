#region

using System;

#endregion

namespace Elk.Std.Attributes;

[AttributeUsage(AttributeTargets.Parameter)]
public class ElkVariadicAttribute : Attribute;