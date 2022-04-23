using System;
using System.Collections.Generic;
using Elk.Interpreting;

namespace Elk;

class LocalScope : Scope
{
    public LocalScope(Scope? parent)
        : base(parent)
    {
    }
}