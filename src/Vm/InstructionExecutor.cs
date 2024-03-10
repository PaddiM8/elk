using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Elk.Interpreting;
using Elk.Interpreting.Exceptions;
using Elk.Interpreting.Scope;
using Elk.Parsing;
using Elk.ReadLine.Render.Formatting;
using Elk.Std.Bindings;
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
    private const bool DUMP_PAGES = true;
    private readonly IndexableStack<RuntimeObject> _stack = new();
    private readonly Stack<Frame> _callStack = new();
    private int _ip;
    private Page _currentPage;
    private RuntimeObject? _returnedValue;

    private InstructionExecutor(Page page)
    {
        _currentPage = page;
        PushFrame(new Frame(page, page.Instructions.Count, 0, IsRoot: false));
    }

    public static RuntimeObject Execute(Page page)
    {
        var executor = new InstructionExecutor(page);

        if (DUMP_PAGES)
        {
            Console.Write($"Page {executor._currentPage.GetHashCode()}:");
            page.Dump();
            Console.WriteLine();
        }

        try
        {
            while (executor._callStack.Any())
            {
                while (executor._ip < executor._currentPage.Instructions.Count)
                    executor.Next();

                executor.PopFrame();
            }
        }
        catch (Exception ex)
        {
            var ipString = Ansi.Format(executor._ip.ToString(), AnsiForeground.DarkYellow);
            var pageString = Ansi.Format(
                executor._currentPage.GetHashCode().ToString(),
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

        Debug.Assert(!executor._stack.Any());

        return RuntimeNil.Value;
    }

    private T GetConstant<T>()
        => _currentPage.ConstantTable.Get<T>(Eat());

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
            case InstructionKind.Pop:
                Pop();
                break;
            case InstructionKind.PopArgs:
                PopArgs(Eat());
                break;
            case InstructionKind.Unpack:
                Unpack(Eat());
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
                Glob();
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
            case InstructionKind.Contains:
                Contains();
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
                throw new NotImplementedException(((InstructionKind)_currentPage.Instructions[_ip - 1]).ToString());
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

    private void Ret()
    {
        _ip = _currentPage.Instructions.Count;
        _returnedValue = _stack.Pop();
    }

    private void Call(bool isRoot = false)
    {
        var page = (Page)_stack.PopObject();
        var frame = new Frame(
            page,
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
        var function = (StdFunction)_stack.PopObject();
        var arguments = new object?[argumentCount];
        for (byte i = 0; i < argumentCount; i++)
            arguments[argumentCount - i - 1] = _stack.Pop();

        // TODO: Get rid of .ToList. The invoker could probably just take an
        // array, after the tree walking interpreter is gone.
        try
        {
            var result = function.Invoke(arguments.ToList());
            _stack.Push(result);
        }
        catch (RuntimeException e)
        {
            throw new RuntimeStdException(e.Message)
            {
                ElkStackTrace = e.ElkStackTrace,
            };
        }
        catch (Exception e)
        {
            throw new RuntimeStdException(e.Message);
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

        // Arguments
        var arguments = (RuntimeList)_stack.Pop();
        foreach (var arg in arguments)
            process.StartInfo.ArgumentList.Add(arg.ToString() ?? "");

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

    private void Glob()
    {
        // The glob instruction should only be used for variadic arguments
        var value = _stack[^1].As<RuntimeString>().Value;
        var matches = Globbing.Glob(ShellEnvironment.WorkingDirectory, value).ToList();
        if (!matches.Any())
            return;

        // Find the list containing the variadic arguments
        var instructions = _currentPage.Instructions;
        var buildListIndex = _ip;
        while (instructions[buildListIndex] is not
            ((byte)InstructionKind.BuildList or (byte)InstructionKind.BuildListBig))
        {
            buildListIndex++;
        }

        // If it's a regular BuildList instruction, turn it into a BuildListBig
        // instruction and add the amount of matches.
        // Otherwise, if it's a BuildListBig instruction already, find the list
        // size constant in the ConstantTable and increase it.
        if (instructions[buildListIndex] == (byte)InstructionKind.BuildList)
        {
            int count = instructions[buildListIndex + 1].ToUshort(instructions[buildListIndex + 2]);
            instructions[buildListIndex] = (byte)InstructionKind.BuildListBig;
            instructions[buildListIndex + 1] = _currentPage.ConstantTable.Add(
                count + matches.Count - 1
            );
            instructions[buildListIndex + 2] = (byte)InstructionKind.Nop;
        }
        else
        {
            var constantAddress = instructions[buildListIndex + 1];
            var count = _currentPage.ConstantTable.Get<int>(constantAddress);
            _currentPage.ConstantTable.Update(
                constantAddress,
                count + matches.Count - 1
            );
        }

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
        var to = _stack.Pop().As<RuntimeInteger>().Value;
        var from = _stack.Pop().As<RuntimeInteger>().Value;
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

    private void PushFrame(Frame frame)
    {
        _callStack.Push(frame);
        _currentPage = frame.Page;
        _ip = 0;
    }

    private Frame PopFrame()
    {
        var frame = _callStack.Pop();
        if (_callStack.Count > 0)
            _currentPage = _callStack.Peek().Page;

        _ip = frame.ReturnAddress;

        return frame;
    }
}