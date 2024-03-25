using System;
using System.Text;
using Elk.Interpreting.Scope;
using Elk.ReadLine.Render.Formatting;
using Elk.Std.Bindings;

namespace Elk.Vm;

enum DisassemblyFormatting
{
    Plain,
    Ansi,
}

class Disassembler
{
    private readonly StringBuilder _builder = new();
    private readonly Page _page;
    private readonly DisassemblyFormatting _formatting;
    private int _ip;

    private Disassembler(Page page, DisassemblyFormatting formatting)
    {
        _page = page;
        _formatting = formatting;
    }

    public static string Disassemble(Page page, DisassemblyFormatting formatting = DisassemblyFormatting.Ansi)
    {
        var disassembler = new Disassembler(page, formatting);
        try
        {
            while (disassembler._ip < page.Instructions.Count)
                disassembler.Next();
        }
        catch (Exception ex)
        {
            Console.WriteLine(disassembler._builder.ToString());
            Console.WriteLine();
            Console.WriteLine(ex);
        }

        return disassembler._builder.ToString();
    }

    private byte NextByte()
        => _page.Instructions[_ip++];

    private void Eat()
    {
        _builder.Append(" " + NextByte());
    }

    private void EatShort(string? format = null)
    {
        var value = NextByte().ToUshort(NextByte());
        _builder.Append(" " + value.ToString(format));
    }

    private void GetConstant<T>()
    {
        var key1 = _page.Instructions[_ip++];
        var key2 = _page.Instructions[_ip++];
        var obj = _page.ConstantTable.Get<T>(key1.ToUshort(key2));
        if (obj is StdFunction stdFunction)
        {
            _builder.Append($" StdFunction[{stdFunction.Name}]");

            return;
        }

        if (obj is Page page)
        {
            _builder.Append($" Page[{page.GetHashCode()}]");

            return;
        }

        if (obj is VariableSymbol variableSymbol)
        {
            _builder.Append($" VariableSymbol[{variableSymbol.Name}]");

            return;
        }

        var stringValue = obj?.ToString() ?? "???";
        _builder.Append(" " + stringValue);
    }

    private void Next()
    {
        switch (NextInstruction())
        {
            case InstructionKind.Store:
                GetConstant<int>();
                break;
            case InstructionKind.Load:
                GetConstant<int>();
                break;
            case InstructionKind.LoadEnvironmentVariable:
            case InstructionKind.StoreEnvironmentVariable:
                GetConstant<string>();
                break;
            case InstructionKind.LoadUpper:
            case InstructionKind.StoreUpper:
                GetConstant<VariableSymbol>();
                break;
            case InstructionKind.Const:
                GetConstant<object>();
                break;
            case InstructionKind.StructConst:
                GetConstant<StructSymbol>();
                break;
            case InstructionKind.New:
                Eat();
                break;
            case InstructionKind.BuildTuple:
            case InstructionKind.BuildList:
            case InstructionKind.BuildSet:
            case InstructionKind.BuildDict:
            case InstructionKind.BuildString:
                EatShort();
                break;
            case InstructionKind.BuildListBig:
                GetConstant<int>();
                break;
            case InstructionKind.BuildRange:
                Eat();
                break;
            case InstructionKind.PopArgs:
                Eat();
                break;
            case InstructionKind.Unpack:
            case InstructionKind.UnpackUpper:
                Eat();
                break;
            case InstructionKind.ExitBlock:
                Eat();
                break;
            case InstructionKind.CallStd:
                Eat();
                break;
            case InstructionKind.CallProgram:
            case InstructionKind.RootCallProgram:
            case InstructionKind.MaybeRootCallProgram:
                EatShort("B");
                Eat();
                break;
            case InstructionKind.DynamicCall:
                Eat();
                break;
            case InstructionKind.PushArgsToRef:
            case InstructionKind.ResolveArgumentsDynamically:
                Eat();
                break;
            case InstructionKind.Jump:
                EatShort();
                break;
            case InstructionKind.JumpBackward:
                EatShort();
                break;
            case InstructionKind.JumpIf:
            case InstructionKind.PopJumpIf:
                EatShort();
                break;
            case InstructionKind.JumpIfNot:
            case InstructionKind.PopJumpIfNot:
                EatShort();
                break;
            case InstructionKind.ForIter:
                EatShort();
                break;
        }
    }

    private InstructionKind NextInstruction()
    {
        var kind = (InstructionKind)_page.Instructions[_ip++];
        var kindString = AnsiFormat(
            kind.ToString().PadRight(12),
            AnsiForeground.Blue
        );
        var ipString = AnsiFormat(
            _ip.ToString().PadLeft(_page.Instructions.Count.ToString().Length),
            AnsiForeground.DarkYellow
        );
        _builder.Append($"\n{ipString}: ");
        _builder.Append(kindString);

        return kind;
    }

    private string AnsiFormat(string value, AnsiForeground foreground)
    {
        return _formatting != DisassemblyFormatting.Ansi
            ? value
            : Ansi.Format(value, foreground);
    }
}