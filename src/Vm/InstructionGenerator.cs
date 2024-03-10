using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Elk.Interpreting.Exceptions;
using Elk.Lexing;
using Elk.Parsing;
using Elk.Std.DataTypes;

namespace Elk.Vm;

class InstructionGenerator
{

    //private readonly List<Page> _pages;
    private readonly Stack<Variable> _locals = new();
    private readonly FunctionTable _functionTable;
    private readonly Token _emptyToken = new(TokenKind.Identifier, string.Empty, TextPos.Default);
    private Page _currentPage = new();
    private int _currentBasePointer;
    private int _scopeDepth;

    private InstructionGenerator(FunctionTable functionTable)
    {
        //_pages = [new Page()];
        _functionTable = functionTable;
    }

    public static Page Generate(Ast ast, FunctionTable functionTable)
    {
        var generator = new InstructionGenerator(functionTable);
        /*foreach (var function in ast.Expressions.Where(x => x is FunctionExpr))
        {
            generator._currentPageIndex = generator._pages.Count;

            var functionExpr = (FunctionExpr)function;
            var symbol = functionExpr.Module.FindFunction(functionExpr.Identifier.Value, lookInImports: false)!;
            generator._pages.Add(generator._functionTable[symbol]);
        }*/

        foreach (var expr in ast.Expressions)
        {
            generator.Next(expr);
            if (expr is not (ModuleExpr or FunctionExpr or StructExpr or LetExpr or KeywordExpr))
                generator.Emit(InstructionKind.Pop);
        }

        foreach (var _ in generator._locals)
            generator.Emit(InstructionKind.Pop);

        return generator._currentPage;
    }

    private void Next(Expr expr)
    {
        switch (expr)
        {
            // TODO: For modules, _scopeDepth should probably be set to 0?
            case ModuleExpr:
            case StructExpr:
                break;
            case FunctionExpr functionExpr:
                Visit(functionExpr);
                break;
            case LetExpr letExpr:
                Visit(letExpr);
                break;
            case NewExpr newExpr:
                Visit(newExpr);
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
            case TupleExpr tupleExpr:
                Visit(tupleExpr);
                break;
            case ListExpr listExpr:
                Visit(listExpr);
                break;
            case SetExpr setExpr:
                Visit(setExpr);
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
            case RangeExpr rangeExpr:
                Visit(rangeExpr);
                break;
            case IndexerExpr indexerExpr:
                Visit(indexerExpr);
                break;
            case TypeExpr typeExpr:
                Visit(typeExpr);
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
        var previousPage = _currentPage;
        _currentPage = _functionTable.Get(
            expr.Module.FindFunction(expr.Identifier.Value, lookInImports: false)!
        );

        foreach (var parameter in expr.Parameters)
            _locals.Push(new Variable(parameter.Identifier, 0));

        var previousBasePointer = _currentBasePointer;
        _currentBasePointer = _locals.Count - 1;
        Next(expr.Block);
        _currentBasePointer = previousBasePointer;

        Emit(InstructionKind.Ret);
        _currentPage = previousPage;

        foreach (var _ in expr.Parameters)
            _locals.Pop();
    }

    private void Visit(LetExpr expr)
    {
        foreach (var identifier in expr.IdentifierList)
            _locals.Push(new Variable(identifier, _scopeDepth));

        Next(expr.Value);

        if (expr.IdentifierList.Count > byte.MaxValue)
            throw new RuntimeException("Too many identifiers in destructuring expression");

        if (expr.IdentifierList.Count > 1)
            Emit(InstructionKind.Unpack, (byte)expr.IdentifierList.Count);
    }

    private void Visit(NewExpr expr)
    {
        foreach (var argument in expr.Arguments)
            Next(argument);

        EmitBig(InstructionKind.StructConst, expr.StructSymbol!);
        Emit(InstructionKind.New, (byte)expr.Arguments.Count);
    }

    private void Visit(IfExpr expr)
    {
        Next(expr.Condition);
        var elseJump = EmitJump(InstructionKind.PopJumpIfNot);
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
        Next(expr.Value);

        // Since the Enumerator is going to be kept as a temporary,
        // while the loop variable is underneath, there needs to be
        // a variable entry for the Enumerator as well.
        _locals.Push(new Variable(_emptyToken, _scopeDepth + 1));
        foreach (var identifier in expr.IdentifierList)
            _locals.Push(new Variable(identifier, _scopeDepth + 1));

        Emit(InstructionKind.GetIter);
        var loopBackIndex = CreateBackwardJumpPoint();
        var startIndex = EmitForIter();

        if (expr.IdentifierList.Count > 1)
            Emit(InstructionKind.Unpack, (byte)expr.IdentifierList.Count);

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

        // TODO: when breaking with a value, it should push that instead
        // ...and the same for for loops
        EmitBig(InstructionKind.Const, RuntimeNil.Value);
    }

    private void Visit(TupleExpr expr)
    {
        foreach (var value in expr.Values)
            Next(value);

        Emit(InstructionKind.BuildTuple);
        Emit((ushort)expr.Values.Count);
    }

    private void Visit(ListExpr expr)
    {
        foreach (var value in expr.Values)
            Next(value);

        Emit(InstructionKind.BuildList);
        Emit((ushort)expr.Values.Count);
    }

    private void Visit(SetExpr expr)
    {
        foreach (var value in expr.Entries)
            Next(value);

        Emit(InstructionKind.BuildSet);
        Emit((ushort)expr.Entries.Count);
    }

    private void Visit(DictionaryExpr expr)
    {
        foreach (var entry in expr.Entries)
        {
            Next(entry.Item1);
            Next(entry.Item2);
        }

        Emit(InstructionKind.BuildDict);
        Emit((ushort)expr.Entries.Count);
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
            OperationKind.In => InstructionKind.Contains,
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

    private void Visit(RangeExpr expr)
    {
        if (expr.From == null)
        {
            EmitBig(InstructionKind.Const, RuntimeNil.Value);
        }
        else
        {
            Next(expr.From);
        }

        if (expr.To == null)
        {
            EmitBig(InstructionKind.Const, RuntimeNil.Value);
        }
        else
        {
            Next(expr.To);
        }

        Emit(
            InstructionKind.BuildRange,
            expr.Inclusive
                ? (byte)1
                : (byte)0
        );
    }

    private void Visit(IndexerExpr expr)
    {
        Next(expr.Value);
        Next(expr.Index);
        Emit(InstructionKind.Index);
    }

    private void Visit(TypeExpr expr)
    {
        EmitBig(InstructionKind.Const, expr.RuntimeValue!);
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
        IEnumerable<(Expr? defaultValue, bool isVariadic)> parameters = Array.Empty<(Expr?, bool)>();
        if (expr.FunctionSymbol != null)
        {
            parameters = expr
                .FunctionSymbol
                .Expr
                .Parameters
                .Select(x => (x.DefaultValue, x.IsVariadic));
        }
        else if (expr.StdFunction != null)
        {
            Expr nilExpr = new KeywordExpr(
                new Token(TokenKind.Nil, "nil", TextPos.Default),
                null,
                expr.Scope
            );

            parameters = expr
                .StdFunction
                .Parameters
                .WithIndex()
                .Where(x => !x.item.IsClosure)
                .Select(x => (
                    defaultValues: x.item.IsNullable ? nilExpr : null,
                    isVariadic: x.index == expr.StdFunction.VariadicStart
                ));
        }

        // Program calls are always variadic
        int? variadicStart = expr.FunctionSymbol == null && expr.StdFunction == null
            ? 0
            : null;
        var argumentCount = 0;
        foreach (var ((argument, parameter), i) in expr.Arguments.ZipLongest(parameters).WithIndex())
        {
            if (argument == null)
            {
                if (parameter.defaultValue != null)
                {
                    Next(parameter.defaultValue);
                    argumentCount++;
                }

                continue;
            }

            if (!variadicStart.HasValue)
                argumentCount++;

            if (parameter.isVariadic)
                variadicStart = i;

            Next(argument);

            if (variadicStart.HasValue && argument is StringInterpolationExpr { IsTextArgument: true })
                Emit(InstructionKind.Glob);
        }

        if (variadicStart.HasValue)
        {
            Emit(InstructionKind.BuildList);
            Emit((ushort)(expr.Arguments.Count - variadicStart));
        }

        if (expr.FunctionSymbol != null)
        {
            EmitCall(expr, isMaybeRoot);

            return;
        }

        if (expr.StdFunction != null)
        {
            EmitStdCall(expr, argumentCount);

            return;
        }

        EmitProgramCall(expr, isMaybeRoot);
    }

    private void EmitCall(CallExpr expr, bool isMaybeRoot = false)
    {
        Debug.Assert(expr.FunctionSymbol != null);

        EmitBig(
            InstructionKind.Const,
            _functionTable.Get(expr.FunctionSymbol)
        );
        var kind = (expr.IsRoot, isMaybeRoot) switch
        {
            (_, true) => InstructionKind.MaybeRootCall,
            (true, _) => InstructionKind.RootCall,
            _ => InstructionKind.Call,
        };

        Emit(kind);
        Emit(InstructionKind.PopArgs, (byte)expr.Arguments.Count);
    }

    private void EmitStdCall(CallExpr expr, int argumentCount)
    {
        Debug.Assert(expr.StdFunction != null);

        if (argumentCount > byte.MaxValue)
            throw new RuntimeException("Too many arguments. A call can have at most 255 function arguments.");

        EmitBig(InstructionKind.Const, expr.StdFunction);
        Emit(InstructionKind.CallStd, (byte)argumentCount);
    }

    private void EmitProgramCall(CallExpr expr, bool isMaybeRoot = false)
    {
        // Program name
        EmitBig(InstructionKind.Const, new RuntimeString(expr.Identifier.Value));

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

        // CallProgram [props] [environmentVariableCount]
        var kind = (expr.IsRoot, isMaybeRoot) switch
        {
            (_, true) => InstructionKind.MaybeRootCallProgram,
            (true, _) => InstructionKind.RootCallProgram,
            _ => InstructionKind.CallProgram,
        };

        Emit(kind);
        Emit((ushort)props);
        Emit((byte)expr.EnvironmentVariables.Count);
    }

    private void Visit(LiteralExpr expr)
    {
        EmitBig(InstructionKind.Const, expr.RuntimeValue!);
    }

    private void Visit(StringInterpolationExpr expr)
    {
        foreach (var part in expr.Parts)
            Next(part);

        Emit(InstructionKind.BuildString);
        Emit((ushort)expr.Parts.Count);
    }

    private void Emit(InstructionKind kind, params byte[] arguments)
    {
        _currentPage.Instructions.Add((byte)kind);
        Emit(arguments);
    }

    private void Emit(params byte[] arguments)
    {
        foreach (var argument in arguments)
            _currentPage.Instructions.Add(argument);
    }

    private void Emit(ushort argument)
    {
        var (left, right) = argument.ToBytePair();
        _currentPage.Instructions.Add(left);
        _currentPage.Instructions.Add(right);
    }

    private void EmitBig(InstructionKind kind, object argument)
    {
        _currentPage.Instructions.Add((byte)kind);
        _currentPage.Instructions.Add(
            _currentPage.ConstantTable.Add(argument)
        );
    }

    private int EmitJump(InstructionKind instruction)
    {
        Emit(instruction);

        // The offset will be set later on using PatchJump
        _currentPage.Instructions.Add(0);
        _currentPage.Instructions.Add(0);

        // Return the offset of the first part of the argument,
        // since that is the value that is going to be modified later
        return _currentPage.Instructions.Count - 2;
    }

    private void PatchJump(int offset)
    {
        var currentOffset = _currentPage.Instructions.Count - 1;

        // Need to subtract by one, because the actual jump is going
        // to happen after the argument bytes have been consumed,
        // and the given offset points to argument 1 out of 2,
        // meaning we need to remove the last byte from the calculation.
        var jump = currentOffset - offset - 1;
        if (jump > ushort.MaxValue)
            throw new RuntimeException("Execution error: Too many instructions to jump over.");

        var (left, right) = ((ushort)jump).ToBytePair();
        _currentPage.Instructions[offset] = left;
        _currentPage.Instructions[offset + 1] = right;
    }

    private int CreateBackwardJumpPoint()
    {
        return _currentPage.Instructions.Count - 2;
    }

    private void EmitBackwardJump(int jumpPoint)
    {
        Emit(InstructionKind.JumpBackward);
        Emit((ushort)(_currentPage.Instructions.Count - jumpPoint));
    }

    private int EmitForIter()
    {
        Emit(InstructionKind.ForIter, 0, 0);

        return _currentPage.Instructions.Count - 2;
    }

    private void EmitEndFor(int startIndex)
    {
        var offset = _currentPage.Instructions.Count - startIndex - 1;
        var (left, right) = ((ushort)offset).ToBytePair();
        _currentPage.Instructions[startIndex] = left;
        _currentPage.Instructions[startIndex + 1] = right;
        Emit(InstructionKind.EndFor);
    }
}