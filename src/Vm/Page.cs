using System;
using System.Collections.Generic;
using System.Linq;

namespace Elk.Vm;

class Page(string? name)
{
    public string? Name { get; } = name;

    public List<byte> Instructions { get; } = [];

    public ConstantTable ConstantTable { get; } = new();

    public void Dump()
    {
        Console.WriteLine(Disassembler.Disassemble(this));
    }
}