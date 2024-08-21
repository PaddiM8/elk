using System;
using System.Collections.Generic;
using Elk.Scoping;

namespace Elk.Vm;

class FunctionTable
{
    private readonly Dictionary<FunctionSymbol, Page> _functions = new();

    public Page Get(FunctionSymbol symbol)
    {
        return _functions.TryGetValue(symbol, out var page)
            ? page
            : Add(symbol);
    }

    public Page GetAndUpdate(FunctionSymbol symbol)
    {
        if (_functions.TryGetValue(symbol, out var page))
        {
            page.Update(symbol.Expr.Identifier.Position.FilePath, []);

            return page;
        }

        return Add(symbol);
    }

    private Page Add(FunctionSymbol symbol)
    {
        var page = new Page(
            symbol.Expr.Identifier.Value,
            symbol.Expr.Identifier.Position.FilePath
        );

        _functions.Add(symbol, page);

        return page;
    }
}