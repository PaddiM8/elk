using System;
using Elk.Exceptions;
using Elk.Std.Attributes;

namespace Elk.Std.DataTypes;

/// <summary>
/// Object containing an error thrown by the Elk Runtime itself,
/// for example from the standard library or semantic errors.
/// </summary>
/// <param name="message"></param>
[ElkType("ElkErrorValue")]
public class RuntimeElkErrorValue(string message) : RuntimeObject
{
    public string Message { get; } = message;

    public override RuntimeObject As(Type toType)
        => toType switch
        {
            _ when toType == typeof(RuntimeElkErrorValue)
                => this,
            _ when toType == typeof(RuntimeString)
                => new RuntimeString(ToString()),
            _
                => throw new RuntimeCastException<RuntimeElkErrorValue>(toType),
        };

    public override string ToString()
        => Message;
}
