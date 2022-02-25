using System;
using System.Collections.Generic;
using Shel.Interpreting;

namespace Shel;

class LocalScope : Scope
{
    public LocalScope(Scope? parent)
        : base(parent)
    {
    }
}