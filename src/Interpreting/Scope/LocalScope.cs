using System.Collections.Generic;

namespace Elk.Interpreting.Scope;

class LocalScope : Scope
{
    public LocalScope(Scope? parent)
        : base(parent)
    {
    }
}