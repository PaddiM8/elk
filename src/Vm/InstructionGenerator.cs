using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Elk.Interpreting;
using Elk.Interpreting.Exceptions;
using Elk.Lexing;
using Elk.Parsing;
using Elk.Std.Bindings;
using Elk.Std.DataTypes;

namespace Elk.Vm;

class InstructionGenerator
{
    private Page CurrentPage
        => _pages[_currentPageIndex];

    private readonly List<Page> _pages;
    private readonly Stack<Variable> _locals = new();
    private readonly Dictionary<string, int> _functions = new();
    private readonly Token _emptyToken = new(TokenKind.Identifier, string.Empty, TextPos.Default);
    private int _currentPageIndex;
    private int _currentBasePointer;
    private int _scopeDepth;

    private InstructionGenerator()
    {
        _pages = [new Page()];
    }

    public static List<Page> Generate(Ast ast)
    {
        var generator = new InstructionGenerator();
        foreach (var expr in ast.Expressions)
        {
            generator.Next(expr);
            if (expr is not (ModuleExpr or FunctionExpr or StructExpr or LetExpr or KeywordExpr))
                generator.Emit(InstructionKind.Pop);
        }

        return generator._pages;
    }

    private void Next(Expr expr)
    {
        switch (expr)
        {
            // TODO: For modules, _scopeDepth should probably be set to 0?
            case FunctionExpr functionExpr:
                Visit(functionExpr);
                break;
            case LetExpr letExpr:
                Visit(letExpr);
                break;
            case IfExpr ifExpr:
                Visit(ifExpr);
                break;
            case ForExpr forExpr:
                Visit(forExpr);
                break;
            case WhileExpr whileExpr:
                Visit(whileExpr);
                break;
            case DictionaryExpr dictionaryExpr:
                Visit(dictionaryExpr);
                break;
            case BlockExpr blockExpr:
                Visit(blockExpr);
                break;
            case KeywordExpr keywordExpr:
                Visit(keywordExpr);
                break;
            case BinaryExpr binaryExpr:
                Visit(binaryExpr);
                break;
            case UnaryExpr unaryExpr:
                Visit(unaryExpr);
                break;
            case FieldAccessExpr fieldAccessExpr:
                Visit(fieldAccessExpr);
                break;
            case IndexerExpr indexerExpr:
                Visit(indexerExpr);
                break;
            case VariableExpr variableExpr:
                Visit(variableExpr);
                break;
            case CallExpr callExpr:
                Visit(callExpr);
                break;
            case LiteralExpr literalExpr:
                Visit(literalExpr);
                break;
            case StringInterpolationExpr stringInterpolationExpr:
                Visit(stringInterpolationExpr);
                break;
            default:
                throw new NotImplementedException();
        }
    }

    private void Visit(FunctionExpr expr)
    {
        var previousPage = _currentPageIndex;
        _pages.Add(new Page());
        _currentPageIndex = _pages.Count - 1;
        _functions.Add(expr.Identifier.Value, _currentPageIndex);

        foreach (var parameter in expr.Parameters)
            _locals.Push(new Variable(parameter.Identifier, 0));

        var previousBasePointer = _currentBasePointer;
        _currentBasePointer = _locals.Count - 1;
        Next(expr.Block);
        _currentBasePointer = previousBasePointer;

        // TODO: Do the same for ReturnExpr (get the parameters
        // from expr.EnclosingFunction)
        foreach (var parameter in expr.Parameters)
            Emit(InstructionKind.Pop);

        Emit(InstructionKind.Ret);

        _currentPageIndex = previousPage;
    }

    private void Visit(LetExpr expr)
    {
        _locals.Push(new Variable(expr.IdentifierList[0], _scopeDepth));
        Next(expr.Value);
    }

    private void Visit(IfExpr expr)
    {
        // TODO: Jumps dont seem to pop the value it uses. Should they?
        // Otherwise, that needs to be done manually here and for ifs
        // and whatever
        Next(expr.Condition);
        var elseJump = EmitJump(InstructionKind.JumpIfNot);
        Next(expr.ThenBranch);
        if (expr.ElseBranch != null)
        {
            var elseEndJump = EmitJump(InstructionKind.Jump);
            PatchJump(elseJump);
            Next(expr.ElseBranch);
            PatchJump(elseEndJump);
        }
    }

    private void Visit(ForExpr expr)
    {
        // TODO: Handle multiple identifiers (maybe there could
        // be some function here to handle those? idk)
        Next(expr.Value);

        // Since the Enumerator is going to be kept as a temporary,
        // while the loop variable is underneath, there needs to be
        // a variable entry for the Enumerator as well.
        _locals.Push(new Variable(_emptyToken, _scopeDepth + 1));
        _locals.Push(new Variable(expr.IdentifierList[0], _scopeDepth + 1));

        Emit(InstructionKind.GetIter);
        var loopBackIndex = CreateBackwardJumpPoint();
        var startIndex = EmitForIter();
        Next(expr.Branch);
        EmitBackwardJump(loopBackIndex);
        EmitEndFor(startIndex);
    }

    private void Visit(WhileExpr expr)
    {
        var jumpBackPoint = CreateBackwardJumpPoint();
        Next(expr.Condition);
        var endJump = EmitJump(InstructionKind.PopJumpIfNot);
        Next(expr.Branch);
        EmitBackwardJump(jumpBackPoint);
        PatchJump(endJump);
    }

    private void Visit(DictionaryExpr expr)
    {
        foreach (var entry in expr.Entries)
        {
            Next(entry.Item1);
            Next(entry.Item2);
        }

        EmitBig(InstructionKind.Dict, expr.Entries.Count);
    }

    private void Visit(BlockExpr expr)
    {
        _scopeDepth++;

        // Skip the last expression, and deal with that later, since
        // a Pop instruction should not be generated for it.
        foreach (var child in expr.Expressions.SkipLast(1))
        {
            Next(child);
            // TODO: Is this right? Probably need to pop values that aren't
            // used (and should never pop variables). Idk
            if (child is not (VariableExpr or KeywordExpr))
                Emit(InstructionKind.Pop);
        }

        // The loop doesn't handle the last expression
        if (expr.Expressions.Any())
        {
            var last = expr.Expressions.Last();
            if (last is CallExpr call)
            {
                Visit(call, isMaybeRoot: true);
            }
            else
            {
                Next(last);
            }
        }

        _scopeDepth--;
        while (_locals.TryPeek(out var local) && local.Depth > _scopeDepth)
        {
            Emit(InstructionKind.Pop);
            _locals.Pop();
        }
    }

    private void Visit(KeywordExpr expr)
    {
        switch (expr.Keyword.Kind)
        {
            case TokenKind.Return:
                if (expr.Value != null)
                {
                    Next(expr.Value);
                }
                else
                {
                    EmitBig(InstructionKind.Const, RuntimeNil.Value);
                }

                Emit(InstructionKind.Ret);
                break;
            default:
                throw new NotImplementedException();
        }
    }

    private void Visit(BinaryExpr expr)
    {
        if (expr.Operator == OperationKind.Equals)
        {
            NextEquals(expr.Left, expr.Right);

            return;
        }

        if (expr.Operator == OperationKind.And)
        {
            Next(expr.Left);
            var jump = EmitJump(InstructionKind.JumpIfNot);
            Emit(InstructionKind.Pop);
            Next(expr.Right);
            PatchJump(jump);

            return;
        }

        if (expr.Operator == OperationKind.Or)
        {
            Next(expr.Left);
            var jump = EmitJump(InstructionKind.JumpIf);
            Emit(InstructionKind.Pop);
            Next(expr.Right);
            PatchJump(jump);

            return;
        }

        var instructionKind = expr.Operator switch
        {
            OperationKind.Addition => InstructionKind.Add,
            OperationKind.Subtraction => InstructionKind.Sub,
            OperationKind.Multiplication => InstructionKind.Mul,
            OperationKind.Division => InstructionKind.Div,
            OperationKind.EqualsEquals => InstructionKind.Equal,
            OperationKind.NotEquals => InstructionKind.NotEqual,
            OperationKind.Less => InstructionKind.Less,
            OperationKind.LessEquals => InstructionKind.LessEqual,
            OperationKind.Greater => InstructionKind.Greater,
            OperationKind.GreaterEquals => InstructionKind.GreaterEqual,
            _ => throw new NotImplementedException(expr.Operator.ToString()),
        };

        Next(expr.Left);
        Next(expr.Right);

        Emit(instructionKind);
    }

    private void NextEquals(Expr left, Expr right)
    {
        if (left is VariableExpr leftValue)
        {
            Next(right);
            EmitBig(InstructionKind.Store, ResolveVariable(leftValue.Identifier.Value));
        }
        else if (left is IndexerExpr indexer)
        {
            Next(indexer.Value);
            Next(indexer.Index);
            Next(right);
            Emit(InstructionKind.IndexStore);
        }
        else if (left is FieldAccessExpr fieldAccess)
        {
            Next(fieldAccess.Object);
            EmitBig(InstructionKind.Const, fieldAccess.RuntimeIdentifier!);
            Next(right);
            Emit(InstructionKind.IndexStore);
        }
        else
        {
            throw new RuntimeException("Invalid assignment");
        }
    }

    private void Visit(UnaryExpr expr)
    {
        Next(expr.Value);

        if (expr.Operator == OperationKind.Not)
        {
            Emit(InstructionKind.Not);
        }
        else if (expr.Operator == OperationKind.Subtraction)
        {
            Emit(InstructionKind.Negate);
        }
        else
        {
            throw new NotImplementedException();
        }
    }

    private void Visit(FieldAccessExpr expr)
    {
        Next(expr.Object);
        EmitBig(InstructionKind.Const, expr.RuntimeIdentifier!);
        Emit(InstructionKind.Index);
    }

    private void Visit(IndexerExpr expr)
    {
        Next(expr.Value);
        Next(expr.Index);
        Emit(InstructionKind.Index);
    }

    private void Visit(VariableExpr expr)
    {
        EmitBig(InstructionKind.Load, ResolveVariable(expr.Identifier.Value));
    }

    private int ResolveVariable(string name)
    {
        // Since _locals is a stack, this is going to start at the top
        foreach (var (local, i) in _locals.WithIndex())
        {
            // The returned index should be 0 if the local is at the bottom
            if (local.Name.Value == name)
            {
                var absoluteOffset = _locals.Count - i - 1;

                return absoluteOffset - _currentBasePointer;
            }
        }

        Debug.Assert(false);

        return -1;
    }

    private void Visit(CallExpr expr, bool isMaybeRoot = false)
    {
        if (expr.FunctionSymbol != null)
        {
            EmitCall(expr, isMaybeRoot);

            return;
        }

        if (expr.StdFunction != null)
        {
            EmitStdCall(expr);

            return;
        }

        EmitProgramCall(expr, isMaybeRoot);
    }

    private void EmitCall(CallExpr expr, bool isMaybeRoot = false)
    {
        Debug.Assert(expr.FunctionSymbol != null);

        foreach (var argument in expr.Arguments)
            Next(argument);

        // TODO: Instead of having a constant, simply pass two
        // bytes to the Call instruction immediately.
        // TODO: Modules
        EmitBig(
            InstructionKind.Const,
            new RuntimeInteger(_functions[expr.Identifier.Value])
        );
        var kind = (expr.IsRoot, isMaybeRoot) switch
        {
            (_, true) => InstructionKind.MaybeRootCall,
            (true, _) => InstructionKind.RootCall,
            _ => InstructionKind.Call,
        };

        Emit(kind);
    }

    private void EmitStdCall(CallExpr expr)
    {
        Debug.Assert(expr.StdFunction != null);

        foreach (var argument in expr.Arguments)
            Next(argument);

        var runtimeFunction = new RuntimeStdFunction(
            expr.StdFunction,
            null,
            Plurality.Singular,
            BuildRuntimeFunctionInvoker
        );
        EmitBig(InstructionKind.Const, runtimeFunction);
        Emit(InstructionKind.CallStd, (byte)expr.Arguments.Count);
    }

    private Invoker BuildRuntimeFunctionInvoker(RuntimeFunction function)
    {
        return (invokerArguments, invokerIsRoot)
            => EvaluateStdCall(invokerArguments, ((RuntimeStdFunction)function).StdFunction);
    }

    private RuntimeObject EvaluateStdCall(
        List<RuntimeObject> arguments,
        StdFunction stdFunction,
        RuntimeClosureFunction? runtimeClosure = null)
    {
        var allArguments = new List<object?>(arguments.Count + 2);
        if (stdFunction.VariadicStart.HasValue)
        {
            var variadicArguments = arguments.GetRange(
                stdFunction.VariadicStart.Value,
                arguments.Count - stdFunction.VariadicStart.Value
            );
            allArguments.AddRange(arguments.GetRange(0, stdFunction.VariadicStart.Value));
            allArguments.Add(variadicArguments);
        }
        else
        {
            allArguments.AddRange(arguments);
        }

        var additionalsIndex = allArguments.Count;
        foreach (var parameter in stdFunction.Parameters.Reverse())
        {
            if (parameter.IsNullable && allArguments.Count < stdFunction.Parameters.Length)
                allArguments.Insert(additionalsIndex, null);
            else if (parameter.Type == typeof(ShellEnvironment))
                throw new NotImplementedException();
                //allArguments.Insert(additionalsIndex, ShellEnvironment);
            else if (parameter.IsClosure)
                throw new NotImplementedException();
        }

        try
        {
            return stdFunction.Invoke(allArguments);
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

    private void EmitProgramCall(CallExpr expr, bool isMaybeRoot = false)
    {
        // TODO: Generate code for this:
        // if (expr is { RedirectionKind: RedirectionKind.None, IsRoot: false })
        //     expr.RedirectionKind = RedirectionKind.Output;

        // Program name
        EmitBig(InstructionKind.Const, new RuntimeString(expr.Identifier.Value));

        // Arguments
        foreach (var argument in expr.Arguments)
            Next(argument);

        // Piped value
        if (expr.PipedToProgram != null)
            Next(expr.PipedToProgram);

        // Props
        var props = ProgramCallProps.None;
        if (expr.PipedToProgram != null)
            props |= ProgramCallProps.HasPipedValue;

        if (expr.DisableRedirectionBuffering)
            props |= ProgramCallProps.DisableRedirectionBuffering;

        if (expr.AutomaticStart)
            props |= ProgramCallProps.AutomaticStart;

        props |= expr.RedirectionKind switch
        {
            RedirectionKind.All => ProgramCallProps.RedirectOutput | ProgramCallProps.RedirectError,
            RedirectionKind.Output => ProgramCallProps.RedirectOutput,
            RedirectionKind.Error => ProgramCallProps.RedirectError,
            _ => ProgramCallProps.None,
        };

        // Environment variables
        foreach (var (key, value) in expr.EnvironmentVariables)
        {
            EmitBig(InstructionKind.Const, new RuntimeString(key));
            Next(value);
        }

        // CallProgram [argumentCount] [props] [environmentVariableCount]
        var kind = (expr.IsRoot, isMaybeRoot) switch
        {
            (_, true) => InstructionKind.MaybeRootCallProgram,
            (true, _) => InstructionKind.RootCallProgram,
            _ => InstructionKind.CallProgram,
        };

        Emit(kind, (byte)expr.Arguments.Count);
        Emit((ushort)props);
        Emit((byte)expr.EnvironmentVariables.Count);
    }

    private void Visit(LiteralExpr expr)
    {
        EmitBig(InstructionKind.Const, expr.RuntimeValue!);
    }

    private void Visit(StringInterpolationExpr expr)
    {
        if (expr.Parts.Count == 1 && expr.Parts.First() is LiteralExpr literal)
        {
            Next(literal);

            return;
        }

        // TODO: When a RuntimeString is created (well, in the executor),
        // the IsTextArgument property needs to be set to expr.IsTextArgument.
        // See Interpreter.cs -> Visit(StringInterpolationExpr)
        throw new NotImplementedException();
    }

    private void Emit(InstructionKind kind, params byte[] arguments)
    {
        CurrentPage.Instructions.Add((byte)kind);
        Emit(arguments);
    }

    private void Emit(params byte[] arguments)
    {
        foreach (var argument in arguments)
            CurrentPage.Instructions.Add(argument);
    }

    private void Emit(ushort argument)
    {
        var (left, right) = argument.ToBytePair();
        CurrentPage.Instructions.Add(left);
        CurrentPage.Instructions.Add(right);
    }

    private void EmitBig(InstructionKind kind, object argument)
    {
        CurrentPage.Instructions.Add((byte)kind);
        CurrentPage.Instructions.Add(
            CurrentPage.ConstantTable.Add(argument)
        );
    }

    private int EmitJump(InstructionKind instruction)
    {
        Emit(instruction);

        // The offset will be set later on using PatchJump
        CurrentPage.Instructions.Add(0);
        CurrentPage.Instructions.Add(0);

        // Return the offset of the first part of the argument,
        // since that is the value that is going to be modified later
        return CurrentPage.Instructions.Count - 2;
    }

    private void PatchJump(int offset)
    {
        var currentOffset = CurrentPage.Instructions.Count - 1;

        // Need to subtract by one, because the actual jump is going
        // to happen after the argument bytes have been consumed,
        // and the given offset points to argument 1 out of 2,
        // meaning we need to remove the last byte from the calculation.
        var jump = currentOffset - offset - 1;
        if (jump > ushort.MaxValue)
            throw new RuntimeException("Execution error: Too many instructions to jump over.");

        var (left, right) = ((ushort)jump).ToBytePair();
        CurrentPage.Instructions[offset] = left;
        CurrentPage.Instructions[offset + 1] = right;
    }

    private int CreateBackwardJumpPoint()
    {
        return CurrentPage.Instructions.Count - 2;
    }

    private void EmitBackwardJump(int jumpPoint)
    {
        Emit(InstructionKind.JumpBackward);
        Emit((ushort)(CurrentPage.Instructions.Count - jumpPoint));
    }

    private int EmitForIter()
    {
        Emit(InstructionKind.ForIter, 0, 0);

        return CurrentPage.Instructions.Count - 2;
    }

    private void EmitEndFor(int startIndex)
    {
        var offset = CurrentPage.Instructions.Count - startIndex - 1;
        var (left, right) = ((ushort)offset).ToBytePair();
        CurrentPage.Instructions[startIndex] = left;
        CurrentPage.Instructions[startIndex + 1] = right;
        Emit(InstructionKind.EndFor);
    }
}