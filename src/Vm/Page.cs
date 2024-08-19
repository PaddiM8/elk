using System;
using System.Collections.Generic;
using System.Linq;

namespace Elk.Vm;

class Page(string? name, string? filePath)
{
    public string? Name { get; } = name;

    public string? FilePath { get; } = filePath;

    public List<byte> Instructions { get; } = [];

    public IReadOnlyList<(int instructionIndex, int lineNumber)> LineNumbers
        => _lineNumbers;

    public ConstantTable ConstantTable { get; } = new();

    private readonly List<(int instructionIndex, int lineNumber)> _lineNumbers = [];

    public void AddLine(int lineNumber)
    {
        _lineNumbers.Add((Instructions.Count, lineNumber));
    }

    public void Dump()
    {
        Console.WriteLine(Disassembler.Disassemble(this));
    }
}