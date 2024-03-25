using System.Collections.Generic;
using System.Linq;
using Elk.Lexing;

namespace Elk.Exceptions;

class RuntimeModuleNotFoundException : RuntimeException
{
    public RuntimeModuleNotFoundException(string name)
        : base($"No such module: {name}")
    {
    }

    public RuntimeModuleNotFoundException(IEnumerable<Token> modulePath)
        : base($"No such module: {string.Join("::", modulePath.Select(x => x.Value))}")
    {
    }
}