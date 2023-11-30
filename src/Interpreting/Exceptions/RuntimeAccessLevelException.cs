using Elk.Parsing;

namespace Elk.Interpreting.Exceptions;

class RuntimeAccessLevelException(AccessLevel accessLevel, string symbolName)
    : RuntimeException($"Cannot access {accessLevel.ToString().ToLower()} symbol '{symbolName}'");