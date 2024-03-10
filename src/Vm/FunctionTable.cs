using System.Collections.Generic;
using Elk.Interpreting.Scope;

namespace Elk.Vm;

class FunctionTable
{
    private readonly Dictionary<FunctionSymbol, Page> _functions = new();

    public Page Get(FunctionSymbol symbol)
    {
        if (_functions.TryGetValue(symbol, out var page))
            return page;

        var newPage = new Page();
        _functions.Add(symbol, newPage);

        return newPage;
    }
}