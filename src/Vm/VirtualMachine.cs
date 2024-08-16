using System;
using System.Collections.Generic;
using System.Linq;
using Elk.Exceptions;
using Elk.Parsing;
using Elk.Scoping;
using Elk.Std.DataTypes;

namespace Elk.Vm;

record struct ExceptionFrame(Page Page, int Ip, int StackSize);

class VirtualMachine(RootModuleScope rootModule, VirtualMachineOptions options)
{
    public RootModuleScope RootModule { get; } = rootModule;

    public ShellEnvironment ShellEnvironment { get; } = new(null);

    private readonly FunctionTable _functions = new();
    private readonly VirtualMachineContext _context = new();

    public void AddGlobalVariable(string name, RuntimeObject value)
    {
        var symbol = RootModule.AddVariable(name, value);
        _context.Variables.Add(symbol, value);
    }

    public Page Generate(Ast ast)
    {
        var executor = new InstructionExecutor(options, _context);
        var generator = new InstructionGenerator(_functions, ShellEnvironment, executor);
        var page = generator.Generate(ast);
        if (!options.DumpInstructions)
            return page;

        foreach (var function in ast.Expressions.Where(x => x is FunctionExpr).Cast<FunctionExpr>())
        {
            var symbol = function.Module.FindFunction(function.Identifier.Value, lookInImports: false)!;
            var functionPage = _functions.Get(symbol);
            Console.Write($"Page {functionPage.GetHashCode()} [{function.Identifier.Value}]");
            functionPage.Dump();
            Console.WriteLine();
        }

        return page;
    }

    public RuntimeObject Execute(Page page)
    {
        var executor = new InstructionExecutor(options, _context);

        return executor.Execute(page);
    }

    public RuntimeObject ExecuteFunction(
        string identifier,
        ICollection<RuntimeObject> arguments,
        bool isRoot)
    {
        var symbol = RootModule.FindFunction(identifier, lookInImports: true);
        if (symbol == null)
            throw new RuntimeNotFoundException(identifier);

        var page = _functions.Get(symbol);
        var function = new RuntimeUserFunction(
            page,
            null,
            _ => (_, _) => RuntimeNil.Value
        )
        {
            ParameterCount = (byte)symbol.Expr.Parameters.Count,
            DefaultParameters = null,
            VariadicStart = null,
        };

        try
        {
            var executor = new InstructionExecutor(options, _context);

            return executor.ExecuteFunction(function, arguments, isRoot);
        }
        catch (RuntimeException e)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.Write($"Error evaluating {identifier}: ");
            Console.ResetColor();
            Console.WriteLine(e);

            return RuntimeNil.Value;
        }
    }
}