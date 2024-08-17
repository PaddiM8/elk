using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Elk.Exceptions;
using Elk.Lexing;
using Elk.Parsing;
using Elk.ReadLine.Render.Formatting;
using Elk.Scoping;
using Elk.Std.DataTypes;

namespace Elk.Vm;

record struct Frame(
    Page Page,
    RuntimeUserFunction? Function,
    int ReturnAddress,
    int BasePointer,
    bool IsRoot
);

class InstructionExecutor
{
    private readonly VirtualMachineOptions _vmOptions;
    private readonly VirtualMachineContext _context;
    private readonly IndexableStack<RuntimeObject> _stack;
    private readonly int _initialStackSize;
    private readonly Stack<Frame> _callStack = new();
    private int _ip;
    private Page _currentPage = null!;
    private RuntimeObject? _returnedValue;

internal InstructionExecutor(VirtualMachineOptions vmOptions, VirtualMachineContext context)
{
    _vmOptions = vmOptions;
    _context = context;
    _stack = context.Stack;
    _initialStackSize = context.Stack.Count;
}

public RuntimeObject Execute(Page page)
{
    PushFrame(new Frame(page, null, page.Instructions.Count, 0, IsRoot: false));

    if (_vmOptions.DumpInstructions)
    {
        Console.Write($"Page {_currentPage.GetHashCode()}:");
        page.Dump();
        Console.WriteLine();
    }

    try
    {
        while (_callStack.Any())
        {
            ExecuteCurrentPage();
            PopFrame();
        }
    }
    catch (Exception ex)
    {
        if (_vmOptions.DumpInstructions)
        {
            if (ex is not RuntimeException)
                DumpState();

            Console.WriteLine();
            Console.WriteLine(ex);
        }

        while (_stack.Count > _initialStackSize)
            _stack.PopObject();

        var lineNumber = FindCurrentLineNumber();
        var textPos = new TextPos(lineNumber, -1, -1, _currentPage.FilePath);

        throw new RuntimeException(ex.Message, textPos, textPos);
    }

    var returnValue = _stack.Any()
        ? _stack.Pop()
        : RuntimeNil.Value;
    Debug.Assert(page.Name == "<root>" || !_stack.Any());

    return returnValue;
}

private int FindCurrentLineNumber()
{
    if (_currentPage.LineNumbers.Count == 0)
        return 0;

        var target = _ip;
        var min = 0;
        var max = _currentPage.LineNumbers.Count - 1;
        while (max - min > 1)
        {
            var middle = (min + max) / 2;
            if (target > _currentPage.LineNumbers[middle].instructionIndex)
            {
                min = middle;
            }
            else
            {
                max = middle;
            }
        }

        var maxEntry = _currentPage.LineNumbers[max];

        return maxEntry.instructionIndex <= _ip
            ? maxEntry.lineNumber
            : _currentPage.LineNumbers[min].lineNumber;
    }

    private void DumpState()
    {
            // If it's an anonymous function, it won't have been dumped
            // before, so dump it now instead
            if (_currentPage.Name == null)
                _currentPage.Dump();

            var ipString = Ansi.Format(_ip.ToString(), AnsiForeground.DarkYellow);
            var pageString = Ansi.Format(
                _currentPage.GetHashCode().ToString(),
                AnsiForeground.DarkYellow
            );
            Console.WriteLine(
                Ansi.Format("Stopped at instruction ", AnsiForeground.Red) +
                    ipString +
                    Ansi.Format(", page ", AnsiForeground.Red) +
                    pageString
            );

            Console.WriteLine("Stack:");
            foreach (var item in _stack)
            {
                var itemString = item.ToString()?.Replace("\n", " ") ?? "";
                if (itemString.Length > 40)
                    itemString = itemString[..40] + "...";

                Console.WriteLine(itemString);
            }
    }

    private void ExecuteCurrentPage()
    {
        while (_ip < _currentPage.Instructions.Count)
        {
            try
            {
                Next();
            }
            catch (RuntimeException ex)
            {
                if (!_context.ExceptionStack.Any())
                    throw;

                var exceptionFrame = _context.ExceptionStack.Pop();
                while (_currentPage != exceptionFrame.Page)
                    PopFrame();

                while (_stack.Count > exceptionFrame.StackSize)
                    _stack.PopObject();

                _ip = exceptionFrame.Ip;

                var value = (ex as RuntimeUserException)?.Value
                    ?? new RuntimeError(new RuntimeString(ex.Message));

                _stack.Push(value);
            }
        }
    }

    public RuntimeObject ExecuteFunction(
        RuntimeUserFunction function,
        ICollection<RuntimeObject> arguments,
        bool isRoot)
    {
        if (function.VariadicStart.HasValue)
        {
            ((RuntimeList)_stack.Peek()).Values.InsertRange(0, arguments);
        }
        else
        {
            foreach (var argument in arguments.AsEnumerable().Reverse())
                _stack.Push(argument);
        }

        _stack.Push(function);

        var originalCallStackSize = _callStack.Count;
        Call(isRoot);

        while (_callStack.Count > originalCallStackSize)
        {
            ExecuteCurrentPage();
            PopFrame();
        }

        PopArgs((byte)arguments.Count);

        return _stack.Pop();
    }

    public RuntimeObject ExecuteFunction(
        RuntimeStdFunction function,
        ICollection<RuntimeObject> arguments)
    {
        if (function.VariadicStart.HasValue)
        {
            ((RuntimeList)_stack.Peek()).Values.InsertRange(0, arguments);
        }
        else
        {
            foreach (var argument in arguments.AsEnumerable().Reverse())
                _stack.Push(argument);
        }

        _stack.Push(function);
        CallStd(function.ParameterCount);

        return _stack.Pop();
    }

    public RuntimeObject ExecuteFunction(
        RuntimeProgramFunction function,
        ICollection<RuntimeObject> arguments,
        bool isRoot)
    {
        ((RuntimeList)_stack.Peek()).Values.InsertRange(0, arguments);

        _stack.Push(function);
        CallProgram(ProgramCallProps.None, 0, isRoot);

        return _stack.Pop();
    }

    private T GetConstant<T>()
        => _currentPage.ConstantTable.Get<T>(Eat().ToUshort(Eat()));

    private byte Eat()
        => _currentPage.Instructions[_ip++];

    private void Next()
    {
        switch ((InstructionKind)Eat())
        {
            case InstructionKind.Nop:
                break;
            case InstructionKind.Load:
                Load(GetConstant<int>());
                break;
            case InstructionKind.Store:
                Store(GetConstant<int>());
                break;
            case InstructionKind.LoadEnvironmentVariable:
                LoadEnvironmentVariable(GetConstant<string>());
                break;
            case InstructionKind.StoreEnvironmentVariable:
                StoreEnvironmentVariable(GetConstant<string>());
                break;
            case InstructionKind.LoadUpper:
                LoadUpper(GetConstant<VariableSymbol>());
                break;
            case InstructionKind.StoreUpper:
                StoreUpper(GetConstant<VariableSymbol>());
                break;
            case InstructionKind.LoadCaptured:
                LoadCaptured(GetConstant<string>());
                break;
            case InstructionKind.StoreCaptured:
                StoreCaptured(GetConstant<string>());
                break;
            case InstructionKind.Capture:
                Capture(Eat());
                break;
            case InstructionKind.Pop:
                Pop();
                break;
            case InstructionKind.PopArgs:
                PopArgs(Eat());
                break;
            case InstructionKind.Unpack:
                Unpack(Eat());
                break;
            case InstructionKind.UnpackUpper:
                UnpackUpper(Eat());
                break;
            case InstructionKind.ExitBlock:
                ExitBlock(Eat());
                break;
            case InstructionKind.Ret:
                Ret();
                break;
            case InstructionKind.Call:
                Call();
                break;
            case InstructionKind.RootCall:
                RootCall();
                break;
            case InstructionKind.MaybeRootCall:
                MaybeRootCall();
                break;
            case InstructionKind.CallStd:
                CallStd(Eat());
                break;
            case InstructionKind.CallProgram:
                CallProgram(
                    (ProgramCallProps)Eat().ToUshort(Eat()),
                    Eat()
                );
                break;
            case InstructionKind.RootCallProgram:
                RootCallProgram(
                    (ProgramCallProps)Eat().ToUshort(Eat()),
                    Eat()
                );
                break;
            case InstructionKind.MaybeRootCallProgram:
                MaybeRootCallProgram(
                    (ProgramCallProps)Eat().ToUshort(Eat()),
                    Eat()
                );
                break;
            case InstructionKind.DynamicCall:
                DynamicCall(Eat());
                break;
            case InstructionKind.PushArgsToRef:
                PushArgsToRef(Eat());
                break;
            case InstructionKind.ResolveArgumentsDynamically:
                ResolveArgumentsDynamically(Eat());
                break;
            case InstructionKind.Index:
                Index();
                break;
            case InstructionKind.IndexStore:
                IndexStore();
                break;
            case InstructionKind.Const:
                Const(GetConstant<object>());
                break;
            case InstructionKind.StructConst:
                StructConst(GetConstant<StructSymbol>());
                break;
            case InstructionKind.Glob:
                Glob(GetConstant<GlobbedArgumentCount>());
                break;
            case InstructionKind.New:
                New(Eat());
                break;
            case InstructionKind.BuildTuple:
                BuildTuple(Eat().ToUshort(Eat()));
                break;
            case InstructionKind.BuildList:
                BuildList(Eat().ToUshort(Eat()));
                break;
            case InstructionKind.BuildListBig:
                BuildListBig(GetConstant<int>());
                break;
            case InstructionKind.BuildGlobbedArgumentList:
                BuildGlobbedArgumentList(GetConstant<GlobbedArgumentCount>());
                break;
            case InstructionKind.BuildSet:
                BuildSet(Eat().ToUshort(Eat()));
                break;
            case InstructionKind.BuildDict:
                BuildDict(Eat().ToUshort(Eat()));
                break;
            case InstructionKind.BuildRange:
                BuildRange(Eat() == 1);
                break;
            case InstructionKind.BuildString:
                BuildString(Eat().ToUshort(Eat()));
                break;
            case InstructionKind.BuildProgramCallReference:
                BuildProgramCallReference();
                break;
            case InstructionKind.Add:
                Add();
                break;
            case InstructionKind.Sub:
                Sub();
                break;
            case InstructionKind.Mul:
                Mul();
                break;
            case InstructionKind.Div:
                Div();
                break;
            case InstructionKind.Pow:
                Pow();
                break;
            case InstructionKind.Mod:
                Mod();
                break;
            case InstructionKind.Negate:
                Negate();
                break;
            case InstructionKind.Not:
                Not();
                break;
            case InstructionKind.Equal:
                Equal();
                break;
            case InstructionKind.NotEqual:
                NotEqual();
                break;
            case InstructionKind.Less:
                Less();
                break;
            case InstructionKind.LessEqual:
                LessEqual();
                break;
            case InstructionKind.Greater:
                Greater();
                break;
            case InstructionKind.GreaterEqual:
                GreaterEqual();
                break;
            case InstructionKind.And:
                And();
                break;
            case InstructionKind.Or:
                Or();
                break;
            case InstructionKind.Contains:
                Contains();
                break;
            case InstructionKind.Coalesce:
                Coalesce();
                break;
            case InstructionKind.ErrorIsType:
                ErrorIsType();
                break;
            case InstructionKind.Jump:
                Jump(Eat().ToUshort(Eat()));
                break;
            case InstructionKind.JumpBackward:
                JumpBackward(Eat().ToUshort(Eat()));
                break;
            case InstructionKind.JumpIf:
                JumpIf(Eat().ToUshort(Eat()));
                break;
            case InstructionKind.JumpIfNot:
                JumpIfNot(Eat().ToUshort(Eat()));
                break;
            case InstructionKind.PopJumpIf:
                PopJumpIf(Eat().ToUshort(Eat()));
                break;
            case InstructionKind.PopJumpIfNot:
                PopJumpIfNot(Eat().ToUshort(Eat()));
                break;
            case InstructionKind.GetIter:
                GetIter();
                break;
            case InstructionKind.ForIter:
                ForIter(Eat().ToUshort(Eat()));
                break;
            case InstructionKind.EndFor:
                EndFor();
                break;
            case InstructionKind.Try:
                Try(Eat().ToUshort(Eat()));
                break;
            case InstructionKind.EndTry:
                EndTry();
                break;
            case InstructionKind.Throw:
                Throw();
                break;
            default:
                throw new ArgumentOutOfRangeException(((InstructionKind)_currentPage.Instructions[_ip - 1]).ToString());
        }
    }

    private void Load(int index)
    {
        var pointer = index + _callStack.Peek().BasePointer;
        _stack.Push(_stack[pointer]);
    }

    private void Store(int index)
    {
        var pointer = index + _callStack.Peek().BasePointer;
        _stack[pointer] = _stack.Peek();
    }

    private void LoadEnvironmentVariable(string name)
    {
        _stack.Push(new RuntimeString(Environment.GetEnvironmentVariable(name) ?? ""));
    }

    private void StoreEnvironmentVariable(string name)
    {
        Environment.SetEnvironmentVariable(
            name,
            _stack.Peek().As<RuntimeString>().Value
        );
    }

    private void LoadUpper(VariableSymbol symbol)
    {
        _context.Variables.TryGetValue(symbol, out var value);
        _stack.Push(value ?? RuntimeNil.Value);
    }

    private void StoreUpper(VariableSymbol symbol)
    {
        _context.Variables[symbol] = _stack.Peek();
    }

    private void LoadCaptured(string name)
    {
        var function = (RuntimeClosureFunction)_callStack.Peek().Function!;
        _stack.Push(function.Environment.FindVariable(name)!.Value);
    }

    private void StoreCaptured(string name)
    {
        var function = (RuntimeClosureFunction)_callStack.Peek().Function!;
        function.Environment.FindVariable(name)!.Value = _stack.Peek();
    }

    private void Capture(byte count)
    {
        var function = (RuntimeClosureFunction)_stack.PopObject();
        for (byte i = 0; i < count; i++)
        {
            function.Environment.AddVariable(
                (string)_stack.PopObject(),
                _stack.Pop()
            );
        }
    }

    private void Pop()
    {
        _stack.PopObject();
    }

    private void PopArgs(byte count)
    {
        for (byte i = 0; i < count; i++)
            Pop();

        _stack.Push(_returnedValue!);
        _returnedValue = null;
    }

    private void Unpack(byte count)
    {
        var container = _stack.Pop();
        if (container is not IEnumerable<RuntimeObject> items)
            throw new RuntimeException("Can only destructure Iterable values");

        var actualCount = 0;
        foreach (var item in items)
        {
            _stack.Push(item);
            actualCount++;
        }

        if (actualCount != count)
            throw new RuntimeException("The amount of items in the destructured Iterable is not the same as the amount of identifiers in the destructuring expressions");
    }

    private void UnpackUpper(byte count)
    {
        var symbols = new VariableSymbol[count];
        for (byte i = 0; i < count; i++)
            symbols[count - i - 1] = (VariableSymbol)_stack.PopObject();

        var container = _stack.Peek();
        if (container is not IEnumerable<RuntimeObject> items)
            throw new RuntimeException("Can only destructure Iterable values");

        var actualCount = 0;
        foreach (var (symbol, item) in symbols.Zip(items))
        {
            _context.Variables[symbol] = item;
            actualCount++;
        }

        if (actualCount != count)
            throw new RuntimeException("The amount of items in the destructured Iterable is not the same as the amount of identifiers in the destructuring expressions");
    }

    private void ExitBlock(byte popCount)
    {
        var returnValue = _stack.Pop();
        for (byte i = 0; i < popCount; i++)
            Pop();

        _stack.Push(returnValue);
    }

    private void Ret()
    {
        _ip = _currentPage.Instructions.Count;
        _returnedValue = _stack.Pop();
    }

    private void Call(bool isRoot = false)
    {
        var function = (RuntimeUserFunction)_stack.Pop();
        var frame = new Frame(
            function.Page,
            function,
            _ip,
            _stack.Count - 1,
            isRoot
        );

        PushFrame(frame);
    }

    private void RootCall()
    {
        Call(isRoot: true);
    }

    private void MaybeRootCall()
    {
        Call(isRoot: _callStack.Peek().IsRoot);
    }

    private void CallStd(byte argumentCount)
    {
        var function = (RuntimeStdFunction)_stack.PopObject();
        var arguments = new object?[argumentCount];
        for (byte i = 0; i < argumentCount; i++)
            arguments[i] = _stack.PopObject();

        // TODO: Get rid of .ToList. The invoker could probably just take an
        // array, after the tree walking interpreter is gone.
        try
        {
            var result = function.StdFunction.Invoke(arguments.ToList());
            _stack.Push(result);
        }
        catch (RuntimeException ex)
        {
            throw new RuntimeStdException(ex.Message)
            {
                ElkStackTrace = ex.ElkStackTrace,
            };
        }
        catch (Exception ex)
        {
            throw new RuntimeStdException(ex.Message);
        }
    }

    private void CallProgram(
        ProgramCallProps props,
        byte environmentVariableCount,
        bool isRoot = false)
    {
        // Environment variables
        var environmentVariables = environmentVariableCount > 0
            ? new (string, RuntimeObject)[environmentVariableCount]
            : null;
        // TODO: I guess these should be generated in reverse order and *not* be added
        // in reverse here, to be consistent with how call arguments work now
        for (byte i = 0; i < environmentVariableCount; i++)
        {
            var value = _stack.Pop();
            var key = _stack.Pop().ToString()!;
            environmentVariables![environmentVariableCount - i - 1] = (key, value);
        }

        // Piped value
        var pipedValue = props.HasFlag(ProgramCallProps.HasPipedValue)
            ? _stack.Pop()
            : null;

        // Program name
        var fileName = ((RuntimeProgramFunction)_stack.Pop()).ProgramName;
        var process = new Process();
        var shouldRedirectOutput = props.HasFlag(ProgramCallProps.RedirectOutput) || !isRoot;
        process.StartInfo = new ProcessStartInfo
        {
            FileName = fileName.StartsWith("./")
                ? Path.Combine(ShellEnvironment.WorkingDirectory, fileName)
                : fileName,
            RedirectStandardOutput = shouldRedirectOutput,
            RedirectStandardError = props.HasFlag(ProgramCallProps.RedirectError),
            RedirectStandardInput = pipedValue != null,
            WorkingDirectory = ShellEnvironment.WorkingDirectory,
        };

        if (environmentVariables != null)
        {
            foreach (var (key, value) in environmentVariables)
                process.StartInfo.EnvironmentVariables.Add(key, value.ToString());
        }

        // Arguments
        var arguments = (RuntimeList)_stack.Pop();
        foreach (var arg in arguments)
            process.StartInfo.ArgumentList.Add(arg.ToString() ?? "");

        var processContext = new ProcessContext(
            process,
            pipedValue,
            waitForExit: !props.HasFlag(ProgramCallProps.DisableRedirectionBuffering)
        );
        if (!shouldRedirectOutput && !props.HasFlag(ProgramCallProps.RedirectError))
        {
            processContext.Start();
            _stack.Push(RuntimeNil.Value);

            return;
        }

        var pipe = new RuntimePipe(
            processContext,
            props.HasFlag(ProgramCallProps.DisableRedirectionBuffering),
            !props.HasFlag(ProgramCallProps.NoAutomaticStart)
        );
        _stack.Push(pipe);
    }

    private void RootCallProgram(ProgramCallProps props, byte environmentVariableCount)
    {
        CallProgram(
            props,
            environmentVariableCount,
            isRoot: true
        );
    }

    private void MaybeRootCallProgram(ProgramCallProps props, byte environmentVariableCount)
    {
        CallProgram(
            props,
            environmentVariableCount,
            isRoot: _callStack.Peek().IsRoot
        );
    }

    private void DynamicCall(byte isRootModifier)
    {
        var argumentCount = (byte)_stack.PopObject();
        var runtimeFunction = _stack.Peek();
        if (runtimeFunction is RuntimeUserFunction)
        {
            var originalCallStackSize = _callStack.Count;
            switch (isRootModifier)
            {
                case 0:
                    Call();
                    break;
                case 1:
                    RootCall();
                    break;
                case 2:
                    MaybeRootCall();
                    break;
            }

            while (_callStack.Count > originalCallStackSize)
            {
                while (_ip < _currentPage.Instructions.Count)
                    Next();

                PopFrame();
            }

            PopArgs(argumentCount);
        }
        else if (runtimeFunction is RuntimeStdFunction)
        {
            CallStd(argumentCount);
        }
        else
        {
            switch (isRootModifier)
            {
                case 0:
                    CallProgram(ProgramCallProps.None, 0);
                    break;
                case 1:
                    RootCallProgram(ProgramCallProps.None, 0);
                    break;
                case 2:
                    MaybeRootCallProgram(ProgramCallProps.None, 0);
                    break;
            }
        }
    }

    private void PushArgsToRef(byte argumentCount)
    {
        var functionReference = (RuntimeFunction)_stack.Pop();
        var arguments = new object[argumentCount];
        for (byte i = 0; i < argumentCount; i++)
            arguments[i] = _stack.PopObject();

        functionReference.Arguments = arguments;
        _stack.Push(functionReference);
    }

    private void ResolveArgumentsDynamically(byte argumentCount)
    {
        var runtimeFunction = _stack[^(argumentCount + 1)].As<RuntimeFunction>();
        var existingArgumentCount = (byte)runtimeFunction.Arguments.Count;
        var arguments = new object[argumentCount + existingArgumentCount];
        for (byte i = 0; i < existingArgumentCount; i++)
            arguments[i] = runtimeFunction.Arguments[i];

        for (var i = existingArgumentCount; i < argumentCount + existingArgumentCount; i++)
            arguments[i] = _stack.Pop();

        // Pop the runtime function
        _stack.Pop();

        // TODO: Simplify this code
        var actualArgumentCount = arguments.Length;
        if (runtimeFunction.VariadicStart.HasValue)
        {
            actualArgumentCount = (byte)(runtimeFunction.VariadicStart.Value + 1);

            // Closure
            if (runtimeFunction.Closure != null)
            {
                _stack.PushObject(runtimeFunction.Closure);
                actualArgumentCount++;
            }

            // Variadic
            var variadic = new RuntimeList(
                arguments[runtimeFunction.VariadicStart.Value..]
                    .Cast<RuntimeObject>()
                    .ToList()
            );
            _stack.Push(variadic);
        }
        else if (runtimeFunction.ParameterCount > arguments.Length)
        {
            actualArgumentCount = runtimeFunction.ParameterCount;

            // Closure
            if (runtimeFunction.Closure != null)
            {
                _stack.PushObject(runtimeFunction.Closure);
                actualArgumentCount++;
            }

            // Default values
            if (runtimeFunction.ParameterCount > actualArgumentCount)
            {
                var defaultValues = runtimeFunction.DefaultParameters?[arguments.Length..runtimeFunction.ParameterCount];
                if (defaultValues != null)
                {
                    foreach (var defaultValue in defaultValues.AsEnumerable().Reverse())
                        _stack.Push(defaultValue);
                }
            }
        }
        else
        {
            // Closure
            if (runtimeFunction.Closure != null)
            {
                _stack.PushObject(runtimeFunction.Closure);
                actualArgumentCount++;
            }
        }

        // Regular arguments (they need to be in reverse order again now)
        var end = runtimeFunction.VariadicStart ?? arguments.Length;
        for (var i = end - 1; i >= 0; i--)
            _stack.PushObject(arguments[i]);

        // The function reference is later going to be popped after the argument count
        _stack.Push(runtimeFunction);

        // Return the actual argument count
        _stack.PushObject((byte)actualArgumentCount);
    }

    private void Index()
    {
        var index = _stack.Pop();
        var value = _stack.Pop();
        if (value is not IIndexable<RuntimeObject> indexable)
            throw new RuntimeUnableToIndexException(value.GetType());

        _stack.Push(indexable[index]);
    }

    private void IndexStore()
    {
        var value = _stack.Pop();
        var index = _stack.Pop();
        var obj = _stack.Pop();

        if (obj is not IIndexable<RuntimeObject> indexable)
            throw new RuntimeUnableToIndexException(value.GetType());

        indexable[index] = value;
        _stack.Push(value);
    }

    private void Const(object value)
    {
        _stack.PushObject(value);
    }

    private void StructConst(StructSymbol symbol)
    {
        _stack.PushObject(symbol);
    }

    private void Glob(GlobbedArgumentCount globbedArgumentCount)
    {
        // The glob instruction should only be used for variadic arguments
        var value = _stack[^1].As<RuntimeString>().Value;
        var matches = Globbing.Glob(ShellEnvironment.WorkingDirectory, value).ToList();
        if (!matches.Any())
            return;

        globbedArgumentCount.GlobbedCount += matches.Count - 1;

        _stack.Pop();
        foreach (var match in matches)
            _stack.Push(new RuntimeString(match));
    }

    private void New(byte argumentCount)
    {
        var symbol = (StructSymbol)_stack.PopObject();
        var arguments = new Dictionary<string, RuntimeObject>();
        for (byte i = 0; i < argumentCount; i++)
        {
            var index = argumentCount - i - 1;
            arguments[symbol.Expr!.Parameters[index].Identifier.Value] = _stack.Pop();
        }

        _stack.Push(new RuntimeStruct(symbol, arguments));
    }

    private void BuildTuple(ushort size)
    {
        var values = new RuntimeObject[size];
        for (ushort i = 0; i < size; i++)
            values[size - i - 1] = _stack.Pop();

        _stack.Push(new RuntimeTuple(values));
    }

    private void BuildList(ushort size)
    {
        var values = new RuntimeObject[size];
        for (ushort i = 0; i < size; i++)
            values[size - i - 1] = _stack.Pop();

        _stack.Push(new RuntimeList(values.ToList()));
    }

    private void BuildListBig(int size)
    {
        var values = new RuntimeObject[size];
        for (var i = 0; i < size; i++)
            values[size - i - 1] = _stack.Pop();

        _stack.Push(new RuntimeList(values.ToList()));
    }

    private void BuildGlobbedArgumentList(GlobbedArgumentCount globbedArgumentCount)
    {
        BuildListBig(globbedArgumentCount.NonGlobbedCount + globbedArgumentCount.GlobbedCount);
        globbedArgumentCount.GlobbedCount = 0;
    }

    private void BuildSet(ushort size)
    {
        var dict = new Dictionary<int, RuntimeObject>(size);
        for (ushort i = 0; i < size; i++)
        {
            var value = _stack.Pop();
            if (!dict.TryAdd(value.GetHashCode(), value))
                throw new RuntimeException("Duplicate value in set");
        }

        _stack.Push(new RuntimeSet(dict));
    }

    private void BuildDict(ushort size)
    {
        var dict = new Dictionary<int, (RuntimeObject, RuntimeObject)>(size);
        for (ushort i = 0; i < size; i++)
        {
            var value = _stack.Pop();
            var key = _stack.Pop();
            if (!dict.TryAdd(key.GetHashCode(), (key, value)))
                throw new RuntimeException("Duplicate value in dictionary");
        }

        _stack.Push(new RuntimeDictionary(dict));
    }

    private void BuildRange(bool isInclusive)
    {
        var toObject = _stack.Pop();
        long? to = toObject is RuntimeNil
            ? null
            : toObject.As<RuntimeInteger>().Value;

        var fromObject = _stack.Pop();
        long? from = fromObject is RuntimeNil
            ? null
            : fromObject.As<RuntimeInteger>().Value;

        if (isInclusive)
        {
            if (to >= from)
            {
                to++;
            }
            else
            {
                from++;
            }
        }

        _stack.Push(new RuntimeRange(from, to));
    }

    private void BuildString(ushort count)
    {
        var parts = new string[count];
        for (ushort i = 0; i < count; i++)
            parts[count - i - 1] = _stack.Pop().As<RuntimeString>().Value;

        _stack.Push(new RuntimeString(string.Concat(parts)));
    }

    private void BuildProgramCallReference()
    {
        Func<RuntimeFunction, Invoker> invoker = function =>
        {
            return (arguments, isRoot) => ExecuteFunction(
                (RuntimeProgramFunction)function,
                arguments,
                isRoot
            );
        };

        var function = new RuntimeProgramFunction(
            _stack.Pop().As<RuntimeString>().Value,
            null,
            invoker
        )
        {
            ParameterCount = 1,
            DefaultParameters = [],
            VariadicStart = 0,
        };

        _stack.Push(function);
    }

    private void Add()
    {
        var right = _stack.Pop();
        _stack[^1] = _stack[^1].Operation(OperationKind.Addition, right);
    }

    private void Sub()
    {
        var right = _stack.Pop();
        _stack[^1] = _stack[^1].Operation(OperationKind.Subtraction, right);
    }

    private void Mul()
    {
        var right = _stack.Pop();
        _stack[^1] = _stack[^1].Operation(OperationKind.Multiplication, right);
    }

    private void Div()
    {
        var right = _stack.Pop();
        _stack[^1] = _stack[^1].Operation(OperationKind.Division, right);
    }

    private void Pow()
    {
        var right = _stack.Pop();
        _stack[^1] = _stack[^1].Operation(OperationKind.Power, right);
    }

    private void Mod()
    {
        var right = _stack.Pop();
        _stack[^1] = _stack[^1].Operation(OperationKind.Modulo, right);
    }

    private void Negate()
    {
        _stack[^1] = _stack[^1].Operation(OperationKind.Subtraction);
    }

    private void Not()
    {
        _stack[^1] = RuntimeBoolean.From(
            !_stack[^1].As<RuntimeBoolean>().IsTrue
        );
    }

    private void Equal()
    {
        var right = _stack.Pop();
        var left = _stack[^1];

        // TODO: Copied over from interpreter... but this is not efficient enough
        var isLeftNil = left is RuntimeNil;
        var isRightNil = right is RuntimeNil;
        if (isLeftNil != isRightNil && (isLeftNil || isRightNil))
        {
            _stack[^1] = RuntimeBoolean.False;

            return;
        }

        _stack[^1] = RuntimeBoolean.From(left.Equals(right));
    }

    private void NotEqual()
    {
        var right = _stack.Pop();
        var left = _stack[^1];

        // TODO: Copied over from interpreter... but this is not efficient enough
        var isLeftNil = left is RuntimeNil;
        var isRightNil = right is RuntimeNil;
        if (isLeftNil != isRightNil && (isLeftNil || isRightNil))
        {
            _stack[^1] = RuntimeBoolean.True;

            return;
        }

        _stack[^1] = RuntimeBoolean.From(!left.Equals(right));
    }

    private void Less()
    {
        var right = _stack.Pop();
        _stack[^1] = RuntimeBoolean.From(
            _stack[^1].CompareTo(right) < 0
        );
    }

    private void LessEqual()
    {
        var right = _stack.Pop();
        _stack[^1] = RuntimeBoolean.From(
            _stack[^1].CompareTo(right) <= 0
        );
    }

    private void Greater()
    {
        var right = _stack.Pop();
        _stack[^1] = RuntimeBoolean.From(
            _stack[^1].CompareTo(right) > 0
        );
    }

    private void GreaterEqual()
    {
        var right = _stack.Pop();
        _stack[^1] = RuntimeBoolean.From(
            _stack[^1].CompareTo(right) >= 0
        );
    }

    private void And()
    {
        var right = _stack.Pop().As<RuntimeBoolean>().IsTrue;
        var left = _stack[^1].As<RuntimeBoolean>().IsTrue;
        _stack[^1] = RuntimeBoolean.From(left && right);
    }

    private void Or()
    {
        var right = _stack.Pop().As<RuntimeBoolean>().IsTrue;
        var left = _stack[^1].As<RuntimeBoolean>().IsTrue;
        _stack[^1] = RuntimeBoolean.From(left || right);
    }

    private void Contains()
    {
        var right = _stack.Pop();
        var left = _stack[^1];
        var result = right switch
        {
            RuntimeList list => list.Values
                .Find(x => x.Operation(OperationKind.EqualsEquals, left).As<RuntimeBoolean>().IsTrue) != null,
            RuntimeRange range => range.Contains(left.As<RuntimeInteger>().Value),
            RuntimeSet set => set.Entries.ContainsKey(left.GetHashCode()),
            RuntimeDictionary dict => dict.Entries.ContainsKey(left.GetHashCode()),
            RuntimeString str => str.Value.Contains(left.As<RuntimeString>().Value),
            _ => throw new RuntimeInvalidOperationException("in", right.GetType()),
        };

        _stack[^1] = RuntimeBoolean.From(result);
    }

    private void Coalesce()
    {
        if (_stack[^2] is RuntimeNil)
        {
            _stack[^2] = _stack.Peek();
            _stack.Pop();

            return;
        }

        _stack.Pop();
    }

    private void ErrorIsType()
    {
        var value = ((RuntimeError)_stack[^2]).Value;
        var result = _stack[^1]
            .As<RuntimeType>()
            .IsAssignableTo(value);

        _stack[^1] = RuntimeBoolean.From(result);
    }

    private void Jump(ushort offset)
    {
        _ip += offset;
    }

    private void JumpBackward(ushort offset)
    {
        _ip -= offset;
    }

    private void JumpIf(ushort offset)
    {
        if (_stack.Peek().As<RuntimeBoolean>().IsTrue)
            _ip += offset;
    }

    private void JumpIfNot(ushort offset)
    {
        if (!_stack.Peek().As<RuntimeBoolean>().IsTrue)
            _ip += offset;
    }

    private void PopJumpIf(ushort offset)
    {
        JumpIf(offset);
        Pop();
    }

    private void PopJumpIfNot(ushort offset)
    {
        JumpIfNot(offset);
        Pop();
    }

    private void GetIter()
    {
        var value = _stack.Pop();
        if (value is not IEnumerable<RuntimeObject> iterable)
            throw new RuntimeIterationException(value.GetType());

        // ReSharper disable once NotDisposedResource
        _stack.PushObject(iterable.GetEnumerator());
    }

    private void ForIter(ushort endOffset)
    {
        var generator = (IEnumerator<RuntimeObject>)_stack.PeekObject();
        if (generator.MoveNext())
        {
            _stack.Push(generator.Current);

            return;
        }

        _ip += endOffset;
    }

    private void EndFor()
    {
        var returnedValue = _stack.Pop();
        var generator = (IEnumerator<RuntimeObject>)_stack.PeekObject();
        generator.Dispose();
        _stack.Push(returnedValue);
    }

    private void Try(ushort endOffset)
    {
        var frame = new ExceptionFrame(
            _currentPage,
            _ip + endOffset,
            _stack.Count
        );
        _context.ExceptionStack.Push(frame);
    }

    private void EndTry()
    {
        _context.ExceptionStack.Pop();
    }

    private void Throw()
    {
        var value = _stack.Pop();
        var error = value as RuntimeError ?? new RuntimeError(value);

        throw new RuntimeUserException(error);
    }

    private void PushFrame(Frame frame)
    {
        _callStack.Push(frame);
        _currentPage = frame.Page;
        _ip = 0;
    }

    private void PopFrame()
    {
        var frame = _callStack.Pop();
        if (_callStack.Count > 0)
            _currentPage = _callStack.Peek().Page;

        _ip = frame.ReturnAddress;
    }
}