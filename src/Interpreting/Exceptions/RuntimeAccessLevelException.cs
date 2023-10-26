using Elk.Parsing;

namespace Elk.Interpreting.Exceptions;

class RuntimeAccessLevelException : RuntimeException
{
    public RuntimeAccessLevelException(AccessLevel accessLevel, string symbolName)
        : base($"Cannot access {accessLevel.ToString().ToLower()} symbol '{symbolName}'")
    {
    }
}