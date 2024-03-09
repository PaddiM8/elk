using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Elk.Interpreting;
using Elk.Interpreting.Exceptions;
using Elk.Parsing;
using Elk.ReadLine.Render.Formatting;
using Elk.Std.DataTypes;

namespace Elk.Vm;

record struct Frame(
    Page Page,
    int ReturnAddress,
    int BasePointer,
    bool IsRoot
);

class InstructionExecutor
{
    // TODO: Micro-optimisation... keep this as a field instead and
    // have some helper methods for dealing with the call stack, that
    // make sure _currentPage gets updated.
    private Page CurrentPage
        => _callStack.Peek().Page;

    private const bool DUMP_PAGES = true;
    private readonly List<Page> _pages;
    private readonly IndexableStack<RuntimeObject> _stack = new();
    private readonly Stack<Frame> _callStack = new();
    private int _ip;

    private InstructionExecutor(List<Page> pages)
    {
        _pages = pages;
    }

    public static RuntimeObject Execute(List<Page> pages)
    {
        if (!pages.Any())
            return RuntimeNil.Value;

        var executor = new InstructionExecutor(pages);
        executor._callStack.Push(new Frame(pages.First(), 0, 0, IsRoot: false));

        if (DUMP_PAGES)
        {
            foreach (var (page, i) in pages.WithIndex())
            {
                Console.Write($"Page {i}:");
                page.Dump();
                Console.WriteLine();
            }
        }

        try
        {
            while (executor._callStack.Any())
            {
                while (executor._ip < executor.CurrentPage.Instructions.Count)
                    executor.Next();

                var frame = executor._callStack.Pop();
                executor._ip = frame.ReturnAddress;
            }
        }
        catch (Exception ex)
        {
            var ipString = Ansi.Format(executor._ip.ToString(), AnsiForeground.DarkYellow);
            var pageString = Ansi.Format(
                executor._pages.IndexOf(executor._callStack.Peek().Page).ToString(),
                AnsiForeground.DarkYellow
            );
            Console.WriteLine(
                Ansi.Format("Stopped at instruction ", AnsiForeground.Red) +
                    ipString +
                    Ansi.Format(", page ", AnsiForeground.Red) +
                    pageString
            );

            Console.WriteLine("Stack:");
            foreach (var item in executor._stack)
                Console.WriteLine(item);

            Console.WriteLine();
            Console.WriteLine(ex);
        }

        Console.WriteLine(string.Join(", ", executor._stack));
        Debug.Assert(!executor._stack.Any());

        return RuntimeNil.Value;
    }

    private T GetConstant<T>()
        => CurrentPage.ConstantTable.Get<T>(Eat());

    private byte Eat()
        => CurrentPage.Instructions[_ip++];

    private void Next()
    {
        switch ((InstructionKind)Eat())
        {
            case InstructionKind.Load:
                Load(GetConstant<int>());
                break;
            case InstructionKind.Store:
                Store(GetConstant<int>());
                break;
            case InstructionKind.Pop:
                Pop();
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
                    Eat(),
                    (ProgramCallProps)Eat().ToUshort(Eat()),
                    Eat()
                );
                break;
            case InstructionKind.RootCallProgram:
                RootCallProgram(
                    Eat(),
                    (ProgramCallProps)Eat().ToUshort(Eat()),
                    Eat()
                );
                break;
            case InstructionKind.MaybeRootCallProgram:
                MaybeRootCallProgram(
                    Eat(),
                    (ProgramCallProps)Eat().ToUshort(Eat()),
                    Eat()
                );
                break;
            case InstructionKind.Index:
                Index();
                break;
            case InstructionKind.IndexStore:
                IndexStore();
                break;
            case InstructionKind.Const:
                Const(GetConstant<RuntimeObject>());
                break;
            case InstructionKind.Dict:
                Dict(GetConstant<int>());
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
            default:
                throw new NotImplementedException(((InstructionKind)CurrentPage.Instructions[_ip - 1]).ToString());
        }
    }

    private void Store(int index)
    {
        _stack[index] = _stack.Pop();
    }

    private void Load(int index)
    {
        // This should probably be its own instruction, to make it
        // more efficient.
        if (index == -1)
        {
            _stack.Push(_stack[0]);

            return;
        }

        var pointer = index + _callStack.Peek().BasePointer;
        _stack.Push(_stack[pointer]);
    }

    private void Pop()
    {
        _stack.PopObject();
    }

    private void Ret()
    {
        _ip = CurrentPage.Instructions.Count;
    }

    private void Call(bool isRoot = false)
    {
        var pageIndex = (int)_stack.Pop().As<RuntimeInteger>().Value;
        var frame = new Frame(
            _pages[pageIndex],
            _ip,
            _stack.Count - 1,
            isRoot
        );

        _callStack.Push(frame);
        _ip = 0;
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
        var function = (RuntimeStdFunction)_stack.Pop();
        var arguments = new RuntimeObject[argumentCount];
        for (var i = 0; i < argumentCount; i++)
            arguments[argumentCount - i - 1] = _stack.Pop();

        // TODO: Get rid of .ToList. The invoker could probably just take an
        // array, after the tree walking interpreter is gone.
        // TODO: Is IsRoot fine like this? Probably?
        var result = function.Invoker(arguments.ToList(), isRoot: false);
        _stack.Push(result);
    }

    private void CallProgram(
        byte argumentCount,
        ProgramCallProps props,
        byte environmentVariableCount,
        bool isRoot = false)
    {
        // Environment variables
        var environmentVariables = environmentVariableCount > 0
            ? new (string, RuntimeObject)[environmentVariableCount]
            : null;
        for (var i = 0; i < environmentVariableCount; i++)
        {
            var value = _stack.Pop();
            var key = _stack.Pop().ToString()!;
            environmentVariables![environmentVariableCount - i - 1] = (key, value);
        }

        // Piped value
        var pipedValue = props.HasFlag(ProgramCallProps.HasPipedValue)
            ? _stack.Pop()
            : null;

        // Arguments
        var arguments = new string[argumentCount];
        for (var i = 0; i < argumentCount; i++)
        {
            var argument = _stack.Pop();
            arguments[argumentCount - i - 1] = argument is RuntimeNil
                ? string.Empty
                : argument.ToString() ?? "";
        }

        // Program name
        var fileName = _stack.Pop().As<RuntimeString>().Value;
        var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = fileName.StartsWith("./")
                ? Path.Combine(ShellEnvironment.WorkingDirectory, fileName)
                : fileName,
            RedirectStandardOutput = props.HasFlag(ProgramCallProps.RedirectOutput) || !isRoot,
            RedirectStandardError = props.HasFlag(ProgramCallProps.RedirectError),
            RedirectStandardInput = pipedValue != null,
            WorkingDirectory = ShellEnvironment.WorkingDirectory,
        };

        if (environmentVariables != null)
        {
            foreach (var (key, value) in environmentVariables)
                process.StartInfo.EnvironmentVariables.Add(key, value.ToString());
        }

        foreach (var arg in arguments)
            process.StartInfo.ArgumentList.Add(arg);

        var processContext = new ProcessContext(
            process,
            pipedValue,
            waitForExit: !props.HasFlag(ProgramCallProps.DisableRedirectionBuffering)
        );
        if (!props.HasFlag(ProgramCallProps.RedirectOutput) &&
            !props.HasFlag(ProgramCallProps.RedirectError))
        {
            processContext.Start();
            _stack.Push(RuntimeNil.Value);

            return;
        }

        var pipe = new RuntimePipe(
            processContext,
            props.HasFlag(ProgramCallProps.DisableRedirectionBuffering),
            props.HasFlag(ProgramCallProps.AutomaticStart)
        );
        _stack.Push(pipe);
    }

    private void RootCallProgram(byte argumentCount, ProgramCallProps props, byte environmentVariableCount)
    {
        CallProgram(
            argumentCount,
            props,
            environmentVariableCount,
            isRoot: true
        );
    }

    private void MaybeRootCallProgram(byte argumentCount, ProgramCallProps props, byte environmentVariableCount)
    {
        CallProgram(
            argumentCount,
            props,
            environmentVariableCount,
            isRoot: _callStack.Peek().IsRoot
        );
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

    private void Const(RuntimeObject value)
    {
        _stack.Push(value);
    }

    private void Dict(int size)
    {
        var dict = new Dictionary<int, (RuntimeObject, RuntimeObject)>(size);
        for (var i = 0; i < size; i++)
        {
            var value = _stack.Pop();
            var key = _stack.Pop();
            if (!dict.TryAdd(key.GetHashCode(), (key, value)))
                throw new RuntimeException("Duplicate value in dictionary");
        }

        _stack.Push(new RuntimeDictionary(dict));
    }

    private void Add()
    {
        var right = _stack.Pop();
        var left = _stack.Pop();
        _stack.Push(left.Operation(OperationKind.Addition, right));
    }

    private void Sub()
    {
        var right = _stack.Pop();
        var left = _stack.Pop();
        _stack.Push(left.Operation(OperationKind.Subtraction, right));
    }

    private void Mul()
    {
        var right = _stack.Pop();
        var left = _stack.Pop();
        _stack.Push(left.Operation(OperationKind.Multiplication, right));
    }

    private void Div()
    {
        var right = _stack.Pop();
        var left = _stack.Pop();
        _stack.Push(left.Operation(OperationKind.Division, right));
    }

    private void Negate()
    {
        var value = _stack.Pop();
        _stack.Push(value.Operation(OperationKind.Subtraction));
    }

    private void Not()
    {
        var value = _stack.Pop();
        _stack.Push(
            RuntimeBoolean.From(
                !value.As<RuntimeBoolean>().IsTrue
            )
        );
    }

    private void Equal()
    {
        var right = _stack.Pop();
        var left = _stack.Pop();

        // TODO: Copied over from interpreter... but this is not efficient enough
        var isLeftNil = left is RuntimeNil;
        var isRightNil = right is RuntimeNil;
        if (isLeftNil != isRightNil && (isLeftNil || isRightNil))
        {
            _stack.Push(RuntimeBoolean.False);

            return;
        }

        _stack.Push(RuntimeBoolean.From(left.Equals(right)));
    }

    private void NotEqual()
    {
        var right = _stack.Pop();
        var left = _stack.Pop();

        // TODO: Copied over from interpreter... but this is not efficient enough
        var isLeftNil = left is RuntimeNil;
        var isRightNil = right is RuntimeNil;
        if (isLeftNil != isRightNil && (isLeftNil || isRightNil))
        {
            _stack.Push(RuntimeBoolean.True);

            return;
        }

        _stack.Push(RuntimeBoolean.From(!left.Equals(right)));
    }

    private void Less()
    {
        var right = _stack.Pop();
        var left = _stack.Pop();
        var isLess = left.CompareTo(right) < 0;
        _stack.Push(RuntimeBoolean.From(isLess));
    }

    private void LessEqual()
    {
        var right = _stack.Pop();
        var left = _stack.Pop();
        var isLess = left.CompareTo(right) <= 0;
        _stack.Push(RuntimeBoolean.From(isLess));
    }

    private void Greater()
    {
        var right = _stack.Pop();
        var left = _stack.Pop();
        var isLess = left.CompareTo(right) > 0;
        _stack.Push(RuntimeBoolean.From(isLess));
    }

    private void GreaterEqual()
    {
        var right = _stack.Pop();
        var left = _stack.Pop();
        var isLess = left.CompareTo(right) >= 0;
        _stack.Push(RuntimeBoolean.From(isLess));
    }

    private void And()
    {
        var right = _stack.Pop().As<RuntimeBoolean>().IsTrue;
        var left = _stack.Pop().As<RuntimeBoolean>().IsTrue;
        _stack.Push(RuntimeBoolean.From(left && right));
    }

    private void Or()
    {
        var right = _stack.Pop().As<RuntimeBoolean>().IsTrue;
        var left = _stack.Pop().As<RuntimeBoolean>().IsTrue;
        _stack.Push(RuntimeBoolean.From(left || right));
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
        var generator = (IEnumerator<RuntimeObject>)_stack.PeekObject();
        generator.Dispose();
    }
}