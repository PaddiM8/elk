using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Elk.Exceptions;
using Elk.Lexing;
using Elk.Parsing;
using Elk.Scoping;
using Elk.Std.Bindings;
using Elk.Std.DataTypes;

namespace Elk.Vm;

class LoopState
{
    public int StartPosition { get; set; }

    public readonly Stack<List<int>> BreakPositions = [];

    public int ScopeDepth { get; set; }
}

class InstructionGenerator(
    FunctionTable functionTable,
    ShellEnvironment shellEnvironment,
    InstructionExecutor executor)
{
    private readonly Stack<Variable> _locals = new();
    private ShellEnvironment _shellEnvironment = null!;
    private readonly Token _emptyToken = new(TokenKind.Identifier, string.Empty, TextPos.Default);
    private Page _currentPage = null!;
    private int _currentBasePointer;
    private int _scopeDepth;
    private Expr? _lastExpr;
    private readonly LoopState _currentLoop = new();

    public Page Generate(Ast ast)
    {
        var filePath = ast.Expressions.FirstOrDefault()?.StartPosition.FilePath;
        _currentPage = new Page("<root>", filePath)
        {
            RootExpressionType = ast.Expressions.LastOrDefault()?.GetType(),
        };

        _shellEnvironment = shellEnvironment;
        _shellEnvironment.ScriptPath = filePath;

        try
        {
            // Deal with the last one outside of the loop, since it
            // should never be popped
            foreach (var expr in ast.Expressions.SkipLast(1))
            {
                Next(expr);
                var shouldPop = expr is not (ModuleExpr or FunctionExpr or StructExpr or LetExpr or KeywordExpr) ||
                                (expr is LetExpr letExpr && letExpr.Symbols.Any(x => x.IsCaptured));
                if (shouldPop)
                    Emit(InstructionKind.Pop);
            }

            if (ast.Expressions.Count > 0)
                Next(ast.Expressions.Last());
        }
        catch (RuntimeException ex)
        {
            ex.StartPosition = _lastExpr?.StartPosition;
            ex.EndPosition = _lastExpr?.EndPosition;

            throw;
        }

        // TODO: What if there are more than 255 locals
        if (_locals.Any())
        {
            var localCount = _locals.Count;
            _locals.Clear();
            Emit(InstructionKind.ExitBlock, (byte)localCount);
        }

        _currentPage.ConstantTable.ClearCache();

        return _currentPage;
    }

    private void Next(Expr expr)
    {
        if (_lastExpr == null || expr.StartPosition.Line > _lastExpr.StartPosition.Line)
            _currentPage.AddLine(expr.StartPosition.Line);

        _lastExpr = expr;

        switch (expr)
        {
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
            case ClosureExpr closureExpr:
                Visit(closureExpr);
                break;
            case TryExpr tryExpr:
                Visit(tryExpr);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void Visit(FunctionExpr expr)
    {
        var previousPage = _currentPage;
        _currentPage = functionTable.GetAndUpdate(
            expr.Module.FindFunction(expr.Identifier.Value, lookInImports: false)!
        );

        foreach (var parameter in expr.Parameters.AsEnumerable().Reverse())
            _locals.Push(new Variable(parameter.Identifier, 0));

        if (expr.HasClosure)
        {
            _locals.Push(
                new Variable(
                    new Token(TokenKind.Identifier, "closure", TextPos.Default),
                    0
                )
            );
        }

        var previousBasePointer = _currentBasePointer;
        _currentBasePointer = _locals.Count - 1;
        Next(expr.Block);
        _currentBasePointer = previousBasePointer;

        Emit(InstructionKind.Ret);
        _currentPage.ConstantTable.ClearCache();
        _currentPage = previousPage;

        foreach (var _ in expr.Parameters)
            _locals.Pop();

        if (expr.HasClosure)
            _locals.Pop();
    }

    private void Visit(LetExpr expr)
    {
        if (expr.IdentifierList.Count > byte.MaxValue)
            throw new RuntimeException("Too many identifiers in destructuring expression");

        Next(expr.Value);

        if (expr.IdentifierList.FirstOrDefault()?.Value.StartsWith('$') is true)
        {
            if (expr.IdentifierList.Count > 1)
                throw new RuntimeException("Cannot destructure into environment variables (yet)");

            EmitBig(
                InstructionKind.StoreEnvironmentVariable,
                expr.IdentifierList.First().Value[1..]
            );
            Emit(InstructionKind.Pop);

            return;
        }

        var symbols = expr.Symbols.ToList();
        if (expr.Scope is not ModuleScope)
        {
            foreach (var identifier in expr.IdentifierList)
                _locals.Push(new Variable(identifier, _scopeDepth));

            if (expr.IdentifierList.Count > 1)
                Emit(InstructionKind.Unpack, (byte)expr.IdentifierList.Count);

            return;
        }

        foreach (var symbol in symbols)
            symbol.IsCaptured = true;

        if (expr.IdentifierList.Count > 1)
        {
            foreach (var symbol in symbols)
                EmitBig(InstructionKind.Const, symbol);

            Emit(InstructionKind.UnpackUpper, (byte)expr.IdentifierList.Count);
        }
        else
        {
            EmitBig(InstructionKind.StoreUpper, symbols.First());
        }
    }

    private void Visit(NewExpr expr)
    {
        foreach (var argument in expr.Arguments)
            Next(argument);

        if (expr.Arguments.Count > byte.MaxValue)
            throw new RuntimeException("Too many parameters. A struct can have at most 255 parameters");

        EmitBig(InstructionKind.StructConst, expr.StructSymbol!);
        Emit(InstructionKind.New, (byte)expr.Arguments.Count);
    }

    private void Visit(IfExpr expr)
    {
        Next(expr.Condition);

        var elseJump = EmitJump(InstructionKind.PopJumpIfNot);
        Next(expr.ThenBranch);

        // TODO: Only emit the else branch and nil if the expr.IsRoot is true
        // (or if it might be root). Maybe this could even be optimised away
        // in a future optimisation pass?
        var elseEndJump = EmitJump(InstructionKind.Jump);
        PatchJump(elseJump);
        if (expr.ElseBranch == null)
        {
            EmitBig(InstructionKind.Const, RuntimeNil.Value);
        }
        else
        {
            Next(expr.ElseBranch);
        }

        PatchJump(elseEndJump);
    }

    private void Visit(ForExpr expr)
    {
        Next(expr.Value);

        // Since the Enumerator is going to be kept as a temporary,
        // while the loop variable is underneath, there needs to be
        // a variable entry for the Enumerator as well.
        _locals.Push(new Variable(_emptyToken, _scopeDepth));

        foreach (var identifier in expr.IdentifierList)
            _locals.Push(new Variable(identifier, _scopeDepth + 1));

        Emit(InstructionKind.GetIter);
        var loopBackIndex = CreateBackwardJumpPoint();
        _currentLoop.StartPosition = loopBackIndex;

        var startIndex = EmitForIter();

        if (expr.IdentifierList.Count > byte.MaxValue)
            throw new RuntimeException("Too many identifiers in destructuring expression");

        if (expr.IdentifierList.Count > 1)
            Emit(InstructionKind.Unpack, (byte)expr.IdentifierList.Count);

        _currentLoop.ScopeDepth = _scopeDepth + 1;
        _currentLoop.BreakPositions.Push([]);
        Next(expr.Branch);
        EmitBackwardJump(loopBackIndex);
        EmitBig(InstructionKind.Const, RuntimeNil.Value);
        EmitEndFor(startIndex);

        foreach (var breakPosition in _currentLoop.BreakPositions.Pop())
            PatchJump(breakPosition);
    }

    private void Visit(WhileExpr expr)
    {
        var jumpBackPoint = CreateBackwardJumpPoint();
        _currentLoop.StartPosition = jumpBackPoint;

        Next(expr.Condition);
        var endJump = EmitJump(InstructionKind.PopJumpIfNot);

        _currentLoop.ScopeDepth = _scopeDepth + 1;
        _currentLoop.BreakPositions.Push([]);
        Next(expr.Branch);
        EmitBackwardJump(jumpBackPoint);
        PatchJump(endJump);
        EmitBig(InstructionKind.Const, RuntimeNil.Value);

        foreach (var breakPosition in _currentLoop.BreakPositions.Pop())
            PatchJump(breakPosition);
    }

    private void Visit(TupleExpr expr)
    {
        foreach (var value in expr.Values)
            Next(value);

        if (expr.Values.Count > byte.MaxValue)
            throw new RuntimeException($"Too many tuple values. There can be at most {ushort.MaxValue}");

        Emit(InstructionKind.BuildTuple);
        Emit((ushort)expr.Values.Count);
    }

    private void Visit(ListExpr expr)
    {
        foreach (var value in expr.Values)
            Next(value);

        if (expr.Values.Count > byte.MaxValue)
            throw new RuntimeException($"Too many list values. There can be at most {ushort.MaxValue}");

        Emit(InstructionKind.BuildList);
        Emit((ushort)expr.Values.Count);
    }

    private void Visit(SetExpr expr)
    {
        foreach (var value in expr.Entries)
            Next(value);

        if (expr.Entries.Count > byte.MaxValue)
            throw new RuntimeException($"Too many set entries. There can be at most {ushort.MaxValue}");

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

        if (expr.Entries.Count > byte.MaxValue)
            throw new RuntimeException($"Too many dictionary entries. There can be at most {ushort.MaxValue}");

        Emit(InstructionKind.BuildDict);
        Emit((ushort)expr.Entries.Count);
    }

    private void Visit(BlockExpr expr)
    {
        _scopeDepth++;

        bool ShouldPop(Expr child) =>
            child is not (LetExpr or KeywordExpr);

        // Skip the last expression, and deal with that later, since
        // a Pop instruction should not be generated for it.
        foreach (var child in expr.Expressions.SkipLast(1))
        {
            Next(child);
            if (ShouldPop(child))
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
            else if (last is ClosureExpr closure)
            {
                Visit(closure, isMaybeRoot: true);
            }
            else
            {
                Next(last);
            }

            if (expr.ParentStructureKind == StructureKind.Loop && ShouldPop(last))
                Emit(InstructionKind.Pop);
        }

        if (expr.Expressions.Count == 0 || expr.Expressions.Last() is LetExpr)
        {
            EmitBig(InstructionKind.Const, RuntimeNil.Value);
            if (expr.ParentStructureKind == StructureKind.Loop)
                Emit(InstructionKind.Pop);
        }

        if (expr.ParentStructureKind == StructureKind.Loop)
        {
            ClearBlock(isPrimaryExitPoint: true);

            return;
        }

        ExitBlock(isPrimaryExitPoint: true);
    }

    private void ExitBlock(bool isPrimaryExitPoint, int? newScopeDepth = null)
    {
        var popCount = ConsumeBlockLocals(isPrimaryExitPoint, newScopeDepth);
        if (popCount > byte.MaxValue)
            throw new RuntimeException("Too many variables in block");

        if (popCount > 0)
            Emit(InstructionKind.ExitBlock, (byte)popCount);
    }

    private void ClearBlock(bool isPrimaryExitPoint, int? newScopeDepth = null)
    {
        var popCount = ConsumeBlockLocals(isPrimaryExitPoint, newScopeDepth);
        for (var i = 0; i < popCount; i++)
            Emit(InstructionKind.Pop);
    }

    private int ConsumeBlockLocals(bool isPrimaryExitPoint, int? newScopeDepth = null)
    {
        var scopeDepth = newScopeDepth ?? _scopeDepth;
        var popCount = _locals.TakeWhile(x => x.Depth >= scopeDepth).Count();
        if (isPrimaryExitPoint)
        {
            for (var i = 0; i < popCount; i++)
                _locals.Pop();

            _scopeDepth--;
        }

        return popCount;
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
            case TokenKind.Break:
                if (expr.Value != null)
                {
                    Next(expr.Value);
                }
                else
                {
                    EmitBig(InstructionKind.Const, RuntimeNil.Value);
                }

                ExitBlock(isPrimaryExitPoint: false, _currentLoop.ScopeDepth);

                var jump = EmitJump(InstructionKind.Jump);
                if (_currentLoop.BreakPositions.Count == 0)
                    throw new RuntimeException("Can only break inside loops");

                _currentLoop.BreakPositions.Peek().Add(jump);
                break;
            case TokenKind.Continue:
                ClearBlock(isPrimaryExitPoint: false, _currentLoop.ScopeDepth);
                EmitBackwardJump(_currentLoop.StartPosition);
                break;
            case TokenKind.Throw:
                if (expr.Value != null)
                {
                    Next(expr.Value);
                }
                else
                {
                    EmitBig(InstructionKind.Const, RuntimeNil.Value);
                }

                Emit(InstructionKind.Throw);
                break;
            default:
                throw new ArgumentException("Invalid keyword: " + expr.Keyword);
        }
    }

    private void Visit(BinaryExpr expr)
    {
        Debug.Assert(expr.Operator is not (OperationKind.Pipe or OperationKind.PipeErr or OperationKind.PipeAll));

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

        if (expr.Operator == OperationKind.NonRedirectingAnd)
        {
            Next(expr.Left);
            Next(expr.Right);

            return;
        }

        if (expr.Operator == OperationKind.NonRedirectingOr)
        {
            // Try
            var tryOffset = EmitTry();
            Next(expr.Left);
            Emit(InstructionKind.EndTry);
            var tryEndJump = EmitJump(InstructionKind.Jump);

            // Catch
            PatchJump(tryOffset);
            Emit(InstructionKind.Pop);
            Next(expr.Right);

            // End
            PatchJump(tryEndJump);

            return;
        }

        if (expr.Operator == OperationKind.If)
        {
            // If
            Next(expr.Right);
            var elseJump = EmitJump(InstructionKind.PopJumpIfNot);

            // Then
            Next(expr.Left);
            var endJump = EmitJump(InstructionKind.Jump);

            // Else
            PatchJump(elseJump);
            EmitBig(InstructionKind.Const, RuntimeNil.Value);

            // End
            PatchJump(endJump);

            return;
        }

        var instructionKind = expr.Operator switch
        {
            OperationKind.Addition => InstructionKind.Add,
            OperationKind.Subtraction => InstructionKind.Sub,
            OperationKind.Multiplication => InstructionKind.Mul,
            OperationKind.Division => InstructionKind.Div,
            OperationKind.Power => InstructionKind.Pow,
            OperationKind.Modulo => InstructionKind.Mod,
            OperationKind.EqualsEquals => InstructionKind.Equal,
            OperationKind.NotEquals => InstructionKind.NotEqual,
            OperationKind.Less => InstructionKind.Less,
            OperationKind.LessEquals => InstructionKind.LessEqual,
            OperationKind.Greater => InstructionKind.Greater,
            OperationKind.GreaterEquals => InstructionKind.GreaterEqual,
            OperationKind.In => InstructionKind.Contains,
            OperationKind.Coalescing => InstructionKind.Coalesce,
            _ => throw new ArgumentOutOfRangeException(expr.Operator.ToString()),
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

            if (leftValue.Identifier.Value.StartsWith('$'))
            {
                EmitBig(InstructionKind.StoreEnvironmentVariable, leftValue.Identifier.Value[1..]);

                return;
            }

            if (leftValue.IsCaptured)
            {
                EmitBig(InstructionKind.StoreCaptured, leftValue.Identifier.Value);

                return;
            }

            var local = TryResolveVariable(leftValue.Identifier.Value);
            if (local.HasValue)
            {
                EmitBig(InstructionKind.Store, local);

                return;
            }

            var symbol = leftValue.Scope.ModuleScope.FindVariable(leftValue.Identifier.Value);
            if (symbol == null)
                throw new RuntimeNotFoundException(leftValue.Identifier.Value);

            EmitBig(InstructionKind.StoreUpper, symbol);
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
            throw new ArgumentOutOfRangeException();
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
        if (expr.Identifier.Value.StartsWith('$'))
        {
            EmitBig(InstructionKind.LoadEnvironmentVariable, expr.Identifier.Value[1..]);

            return;
        }

        if (expr.IsCaptured)
        {
            EmitBig(InstructionKind.LoadCaptured, expr.Identifier.Value);

            return;
        }

        var local = TryResolveVariable(expr.Identifier.Value);
        if (local.HasValue)
        {
            EmitBig(InstructionKind.Load, local);

            return;
        }

        EmitBig(InstructionKind.LoadUpper, expr.Scope.FindVariable(expr.Identifier.Value)!);
    }

    private int ResolveVariable(string name)
    {
        var result = TryResolveVariable(name);
        Debug.Assert(result.HasValue);

        return result.Value;
    }

    private int? TryResolveVariable(string name)
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

        return null;
    }

    private void Visit(CallExpr expr, bool isMaybeRoot = false, RuntimeFunction? closure = null)
    {
        switch (expr.CallType)
        {
            case CallType.Function:
                EmitCall(expr, isMaybeRoot, closure);
                break;
            case CallType.StdFunction:
                EmitStdCall(expr, closure);
                break;
            case CallType.BuiltInCall:
                EmitBuiltInCall(expr, isMaybeRoot);
                break;
            case CallType.BuiltInClosure:
                EmitBuiltInClosure(expr, isMaybeRoot);
                break;
            case CallType.BuiltInExec:
                EmitBuiltInExec(expr, isMaybeRoot);
                break;
            case CallType.BuiltInSource:
                EmitBuiltInSource(expr);
                break;
            default:
                EmitProgramCall(expr, isMaybeRoot);
                break;
        }
    }

    private int EmitArguments(CallExpr expr, bool skipFirst = false)
    {
        var parameters = new List<(Expr? defaultValue, bool isVariadic)>();
        if (expr.FunctionSymbol != null)
        {
            parameters = expr
                .FunctionSymbol
                .Expr
                .Parameters
                .Select(x => (x.DefaultValue, x.IsVariadic))
                .ToList();
        }
        else if (expr.StdFunction != null)
        {
            Expr nilExpr = new LiteralExpr(
                new Token(TokenKind.Nil, "nil", expr.EndPosition),
                expr.Scope
            )
            {
                RuntimeValue = RuntimeNil.Value,
            };

            parameters = expr
                .StdFunction
                .Parameters
                .WithIndex()
                .Where(x => !x.item.IsClosure)
                .Where(x => x.item.Type != typeof(ShellEnvironment))
                .Select(x => (
                    defaultValues: x.item.IsNullable ? nilExpr : null,
                    isVariadic: x.index == expr.StdFunction.VariadicStart
                ))
                .ToList();
        }

        var skipCount = skipFirst ? 1 : 0;
        var arguments = new List<Expr>();
        var isProgramCall = expr.FunctionSymbol == null && expr.StdFunction == null;
        var isEmptyVariadic = expr.Arguments.Count == 0 &&
             !expr.IsReference &&
             parameters.Any(x => x.isVariadic);
        var variadicArguments = isProgramCall | isEmptyVariadic
            ? new List<(Expr expr, bool isGlob)>()
            : null;

        foreach (var (argument, parameter) in expr.Arguments.ZipLongest(parameters).Skip(skipCount))
        {
            if (parameter.isVariadic && (argument != null || !expr.IsReference))
                variadicArguments = [];

            if (argument == null)
            {
                if (parameter.defaultValue != null)
                    arguments.Add(parameter.defaultValue);

                continue;
            }

            if (variadicArguments != null)
            {
                var isGlob = argument is StringInterpolationExpr { IsTextArgument: true };
                variadicArguments.Add((argument, isGlob));

                continue;
            }

            arguments.Add(argument);
        }

        // The arguments should be emitted in reverse, meaning the variadic arguments
        // come first. However, the variadic arguments will be in their own list, in
        // non-reversed order.
        if (variadicArguments != null)
        {
            var globbedArgumentCount = new GlobbedArgumentCount(variadicArguments.Count);
            var hasGlobs = false;
            foreach (var (variadicArgument, isGlob) in variadicArguments)
            {
                Next(variadicArgument);
                if (isGlob)
                {
                    EmitBig(InstructionKind.Glob, globbedArgumentCount);
                    hasGlobs = true;
                }
            }

            // Dynamic function calls do this on the fly
            if (!expr.IsReference)
            {
                if (hasGlobs)
                {
                    EmitBig(InstructionKind.BuildGlobbedArgumentList, globbedArgumentCount);
                }
                else
                {
                    Emit(InstructionKind.BuildList);
                    Emit((ushort)variadicArguments.Count);
                }
            }
        }

        foreach (var argument in arguments.AsEnumerable().Reverse())
            Next(argument);

        return arguments.Count + (variadicArguments == null ? 0 : 1);
    }

    private void EmitCall(CallExpr expr, bool isMaybeRoot = false, RuntimeFunction? closure = null)
    {
        Debug.Assert(expr.FunctionSymbol != null);

        var argumentCount = EmitArguments(expr);

        // TODO: Why isn't this above the line with EmitArguments?
        // It seems to work... but this is inconsistent with how it's
        // done in other places
        if (closure != null && !expr.IsReference)
        {
            EmitBig(InstructionKind.Const, closure);
            argumentCount++;
        }

        Func<RuntimeFunction, Invoker> invoker = function =>
        {
            return (arguments, isRoot) => executor.ExecuteFunction(
                (RuntimeUserFunction)function,
                arguments,
                isRoot,
                isIndependentCall: false
            );
        };

        if (expr.FunctionSymbol.Expr.Parameters.Count > byte.MaxValue)
            throw new RuntimeException("Too many parameters. A function can have at most 255 parameters.");

        var variadicStart = expr.FunctionSymbol.Expr.Parameters
            .FindIndex(x => x.IsVariadic);
        var runtimeFunction = new RuntimeUserFunction(
            functionTable.Get(expr.FunctionSymbol),
            null,
            invoker
        )
        {
            ParameterCount = (byte)expr.FunctionSymbol.Expr.Parameters.Count,
            DefaultParameters = expr.FunctionSymbol.Expr.Parameters
                .Where(x => x.DefaultValue != null)
                .Select(x => GetExprRuntimeValue(x.DefaultValue!))
                .ToList(),
            VariadicStart = variadicStart == -1
                ? null
                : (byte)variadicStart,
            Closure = expr.IsReference
                ? closure
                : null,
        };

        EmitBig(InstructionKind.Const, runtimeFunction);

        if (expr.IsReference)
        {
            Emit(InstructionKind.PushArgsToRef, (byte)argumentCount);

            return;
        }

        var kind = (expr.IsRoot, isMaybeRoot) switch
        {
            (_, true) => InstructionKind.MaybeRootCall,
            (true, _) => InstructionKind.RootCall,
            _ => InstructionKind.Call,
        };

        Emit(kind);
        Emit(InstructionKind.PopArgs, (byte)argumentCount);
    }

    private static RuntimeObject GetExprRuntimeValue(Expr expr)
    {
        return expr switch
        {
            LiteralExpr literal => literal.RuntimeValue ?? RuntimeNil.Value,
            ListExpr => new RuntimeList([]),
            DictionaryExpr => new RuntimeDictionary(),
            StringInterpolationExpr interpolation =>
                (interpolation.Parts.FirstOrDefault() as LiteralExpr)?.RuntimeValue ?? RuntimeNil.Value,
            _ => RuntimeNil.Value,
        };
    }

    private void EmitStdCall(CallExpr expr, RuntimeFunction? closure = null)
    {
        Debug.Assert(expr.StdFunction != null);

        Func<RuntimeFunction, Invoker> invoker = function =>
        {
            return (arguments, _) => executor.ExecuteFunction(
                (RuntimeStdFunction)function,
                arguments
            );
        };

        var functionReference = new RuntimeStdFunction(
            expr.StdFunction,
            null,
            invoker
        )
        {
            ParameterCount = (byte)expr.StdFunction.Parameters.Length,
            DefaultParameters = expr.StdFunction.Parameters
                .Where(x => x.IsNullable)
                .Select<StdFunctionParameter, RuntimeObject>(_ => RuntimeNil.Value)
                .ToList(),
            VariadicStart = (byte?)expr.StdFunction.VariadicStart,
        };

        if (expr.IsReference)
        {
            var referenceArgumentCount = 0;
            if (expr.StdFunction.Parameters.LastOrDefault()?.Type == typeof(ShellEnvironment))
            {
                EmitBig(InstructionKind.Const, _shellEnvironment);
                referenceArgumentCount++;
            }

            referenceArgumentCount += EmitArguments(expr);
            if (referenceArgumentCount > byte.MaxValue)
                throw new RuntimeException("Too many arguments. A call can have at most 255 function arguments");

            EmitBig(InstructionKind.Const, functionReference);

            if (referenceArgumentCount > 0)
                Emit(InstructionKind.PushArgsToRef, (byte)referenceArgumentCount);

            if (closure != null)
            {
                var closureFuncType = expr.StdFunction.Parameters.First(x => x.IsClosure).Type;
                functionReference.Closure = ConstructClosureFunc(closureFuncType, closure);
            }

            return;
        }

        var argumentCount = 0;
        if (closure != null)
        {
            var closureFuncType = expr.StdFunction.Parameters.First(x => x.IsClosure).Type;
            EmitBig(InstructionKind.Const, ConstructClosureFunc(closureFuncType, closure));
            argumentCount++;
        }

        if (expr.StdFunction.Parameters.LastOrDefault()?.Type == typeof(ShellEnvironment))
        {
            EmitBig(InstructionKind.Const, _shellEnvironment);
            argumentCount++;
        }

        argumentCount += EmitArguments(expr);

        if (argumentCount > byte.MaxValue)
            throw new RuntimeException("Too many arguments. A call can have at most 255 function arguments");

        EmitBig(InstructionKind.Const, functionReference);
        Emit(InstructionKind.CallStd, (byte)argumentCount);
    }

    private object ConstructClosureFunc(Type closureFuncType, RuntimeFunction runtimeFunction)
    {
        if (closureFuncType == typeof(Func<RuntimeObject>))
        {
            return new Func<RuntimeObject>(() =>
                runtimeFunction.Invoker([], isRoot: false)
            );
        }

        if (closureFuncType == typeof(Func<RuntimeObject, RuntimeObject>))
        {
            return new Func<RuntimeObject, RuntimeObject>(a =>
                runtimeFunction.Invoker([a], isRoot: false)
            );
        }

        if (closureFuncType == typeof(Func<RuntimeObject, RuntimeObject, RuntimeObject>))
        {
            return new Func<RuntimeObject, RuntimeObject, RuntimeObject>((a, b) =>
                runtimeFunction.Invoker([a, b], isRoot: false)
            );
        }

        // Action
        if (closureFuncType == typeof(Action))
        {
            return new Action(() =>
                runtimeFunction.Invoker([], isRoot: true)
            );
        }

        if (closureFuncType == typeof(Action<RuntimeObject>))
        {
            return new Action<RuntimeObject>(a =>
                runtimeFunction.Invoker([a], isRoot: true)
            );
        }

        if (closureFuncType == typeof(Action<RuntimeObject, RuntimeObject>))
        {
            return new Action<RuntimeObject, RuntimeObject>((a, b) =>
                runtimeFunction.Invoker([a, b], isRoot: true)
            );
        }

        // Fallback, variadic
        return new Func<IEnumerable<RuntimeObject>, RuntimeObject>(args =>
            runtimeFunction.Invoker(args.ToList(), isRoot: false)
        );
    }

    private void EmitBuiltInCall(CallExpr expr, bool isMaybeRoot = false)
    {
        if (expr.Arguments.Count == 0)
            throw new RuntimeWrongNumberOfArgumentsException("call", 1, 0, variadic: true);

        // The function reference
        Next(expr.Arguments.First());

        // The first argument should be the function reference, so that shouldn't be emitted
        // as an argument to the actual call.
        foreach (var argument in expr.Arguments.Skip(1).Reverse())
            Next(argument);

        var argumentCount = expr.Arguments.Count - 1;
        if (argumentCount > byte.MaxValue)
            throw new RuntimeException("Too many arguments. A call can have at most 255 function arguments");

        Emit(InstructionKind.ResolveArgumentsDynamically, (byte)argumentCount);

        byte isRootModifier = (expr.IsRoot, isMaybeRoot) switch
        {
            (_, true) => 2, // Maybe
            (true, _) => 1, // Yes
            _ => 0, // No
        };
        Emit(InstructionKind.DynamicCall, isRootModifier);
    }

    private void EmitBuiltInClosure(CallExpr expr, bool isMaybeRoot = false)
    {
        if (expr.IsReference)
            throw new RuntimeException("Can't get the reference of 'closure' (yet)");

        // The function reference
        if (expr.EnclosingClosureProvidingFunction?.ClosureSymbol?.IsCaptured is true)
        {
            EmitBig(InstructionKind.LoadCaptured, "closure");
        }
        else
        {
            EmitBig(InstructionKind.Load, ResolveVariable("closure"));
        }

        // Arguments
        foreach (var argument in expr.Arguments.AsEnumerable().Reverse())
            Next(argument);

        if (expr.Arguments.Count > byte.MaxValue)
            throw new RuntimeException("Too many arguments. A call can have at most 255 function arguments");

        Emit(InstructionKind.ResolveArgumentsDynamically, (byte)expr.Arguments.Count);

        byte isRootModifier = (expr.IsRoot, isMaybeRoot) switch
        {
            (_, true) => 2, // Maybe
            (true, _) => 1, // Yes
            _ => 0, // No
        };
        Emit(InstructionKind.DynamicCall, isRootModifier);
    }

    private void EmitBuiltInExec(CallExpr expr, bool isMaybeRoot = false)
    {
        if (expr.IsReference)
            throw new RuntimeException("Can't get the reference of 'exec' (yet)");

        if (expr.Arguments.Count == 0)
            throw new RuntimeWrongNumberOfArgumentsException("exec", 1, 0, variadic: true);

        // Arguments
        EmitArguments(expr, skipFirst: true);

        // Program reference
        Next(expr.Arguments.First());
        Emit(InstructionKind.BuildProgramCallReference);

        // CallProgram [props] [environmentVariableCount]
        var kind = (expr.IsRoot, isMaybeRoot) switch
        {
            (_, true) => InstructionKind.MaybeRootCallProgram,
            (true, _) => InstructionKind.RootCallProgram,
            _ => InstructionKind.CallProgram,
        };

        Emit(kind);
        Emit((ushort)ProgramCallProps.None);
        Emit((byte)0);
    }

    private void EmitBuiltInSource(CallExpr expr)
    {
        if (expr.Arguments.Count != 1)
            throw new RuntimeWrongNumberOfArgumentsException("source", 1, expr.Arguments.Count);

        Next(expr.Arguments.Single());
        EmitBig(InstructionKind.Const, expr.Scope.ModuleScope.RootModule);
        Emit(InstructionKind.Source);
    }

    private void EmitProgramCall(CallExpr expr, bool isMaybeRoot = false)
    {
        // Arguments
        var argumentCount = EmitArguments(expr);

        Func<RuntimeFunction, Invoker> invoker = function =>
        {
            return (arguments, isRoot) => executor.ExecuteFunction(
                (RuntimeProgramFunction)function,
                arguments,
                isRoot
            );
        };

        // Program reference
        var runtimeFunction = new RuntimeProgramFunction(
            expr.Identifier.Value,
            null,
            invoker
        )
        {
            ParameterCount = 1,
            DefaultParameters = [],
            VariadicStart = 0,
        };

        EmitBig(InstructionKind.Const, runtimeFunction);

        if (expr.IsReference)
        {
            if (expr.Arguments.Count > byte.MaxValue)
                throw new RuntimeException("Too many arguments. A call can have at most 255 function arguments");

            Emit(InstructionKind.PushArgsToRef, (byte)argumentCount);

            return;
        }

        // Piped value
        if (expr.PipedToProgram != null)
            Next(expr.PipedToProgram);

        // Props
        var props = ProgramCallProps.None;
        if (expr.PipedToProgram != null)
            props |= ProgramCallProps.HasPipedValue;

        if (expr.DisableRedirectionBuffering)
            props |= ProgramCallProps.DisableRedirectionBuffering;

        if (!expr.AutomaticStart)
            props |= ProgramCallProps.NoAutomaticStart;

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

        if (expr.EnvironmentVariables.Count > byte.MaxValue)
            throw new RuntimeException("Too many environment variables. A program invocation can have at most 255 environment variables");

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

        if (expr.Parts.Count > byte.MaxValue)
            throw new RuntimeException($"Too many string interpolation parts. There can be at most {ushort.MaxValue}");

        if (expr.Parts is not [LiteralExpr { Value.Kind:
            TokenKind.DoubleQuoteStringLiteral or
            TokenKind.SingleQuoteStringLiteral or
            TokenKind.TextArgumentStringLiteral }])
        {
            Emit(InstructionKind.BuildString);
            Emit((ushort)expr.Parts.Count);
        }
    }

    private void Visit(ClosureExpr expr, bool isMaybeRoot = false)
    {
        var previousPage = _currentPage;
        var page = new Page(name: null, expr.StartPosition.FilePath)
        {
            RootExpressionType = expr.Body.Expressions.LastOrDefault()?.GetType(),
        };

        page.AddLine(expr.StartPosition.Line);
        _currentPage = page;

        foreach (var parameter in expr.Parameters.AsEnumerable().Reverse())
            _locals.Push(new Variable(parameter, 0));

        var previousBasePointer = _currentBasePointer;
        _currentBasePointer = _locals.Count - 1;

        Next(expr.Body);
        Emit(InstructionKind.Ret);

        _currentBasePointer = previousBasePointer;
        _currentPage = previousPage;

        foreach (var _ in expr.Parameters)
            _locals.Pop();

        if (expr.Parameters.Count > byte.MaxValue)
            throw new RuntimeException("Too many parameters. A function can have at most 255 parameters");

        Func<RuntimeFunction, Invoker> invoker = function =>
        {
            return (arguments, isRoot) => executor.ExecuteFunction(
                (RuntimeUserFunction)function,
                arguments,
                isRoot,
                isIndependentCall: false
            );
        };

        var environment = new LocalScope(expr.Scope);
        foreach (var captured in expr.CapturedVariables)
            environment.AddVariable(captured, RuntimeNil.Value);

        var runtimeFunction = new RuntimeClosureFunction(
            page,
            environment,
            null,
            invoker
        )
        {
            ParameterCount = (byte)expr.Parameters.Count,
            DefaultParameters = [],
            VariadicStart = null,
        };

        foreach (var captured in expr.CapturedVariables)
        {
            if (expr.EnclosingFunction is ClosureExpr parentClosure &&
                parentClosure.CapturedVariables.Contains(captured))
            {
                EmitBig(InstructionKind.LoadCaptured, captured);
                EmitBig(InstructionKind.Const, captured);

                continue;
            }

            var localIndex = TryResolveVariable(captured);
            if (localIndex.HasValue)
            {
                EmitBig(InstructionKind.Load, localIndex.Value);
                EmitBig(InstructionKind.Const, captured);

                continue;
            }

            EmitBig(InstructionKind.LoadUpper, expr.Scope.ModuleScope.FindVariable(captured)!);
            EmitBig(InstructionKind.Const, captured);
        }

        if (expr.CapturedVariables.Count > byte.MaxValue)
            throw new RuntimeException("Too many captured variables. Can capture at most 255");

        if (expr.CapturedVariables.Count > 0)
        {
            EmitBig(InstructionKind.Const, runtimeFunction);
            Emit(InstructionKind.Capture, (byte)expr.CapturedVariables.Count);
        }

        Visit(expr.Function, isMaybeRoot, runtimeFunction);
    }

    private void Visit(TryExpr expr)
    {
        var tryOffset = EmitTry();
        Next(expr.Body);
        Emit(InstructionKind.EndTry);
        var tryEndJump = EmitJump(InstructionKind.Jump);
        PatchJump(tryOffset);

        int? lastJump = null;
        List<int> endJumps = [];
        foreach (var catchExpression in expr.CatchExpressions)
        {
            if (lastJump.HasValue)
            {
                PatchJump(lastJump.Value);
                lastJump = null;
            }

            if (catchExpression.Type != null)
            {
                Next(catchExpression.Type);
                Emit(InstructionKind.ErrorIsType);
                lastJump = EmitJump(InstructionKind.PopJumpIfNot);
            }

            if (catchExpression.Identifier?.Value != null)
                _locals.Push(new Variable(catchExpression.Identifier!, _scopeDepth + 1));

            Next(catchExpression.Body);

            endJumps.Add(EmitJump(InstructionKind.Jump));
        }

        if (lastJump.HasValue)
        {
            PatchJump(lastJump.Value);
        }

        foreach (var jump in endJumps)
            PatchJump(jump);

        PatchJump(tryEndJump);
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
        var (key1, key2) = _currentPage.ConstantTable.Add(argument).ToBytePair();
        _currentPage.Instructions.Add((byte)kind);
        _currentPage.Instructions.Add(key1);
        _currentPage.Instructions.Add(key2);
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
        // It should jump to the instruction right above EndFor,
        // since that instruction pushes Nil to the stack, which
        // is necessary
        var offset = _currentPage.Instructions.Count - startIndex - 5;
        var (left, right) = ((ushort)offset).ToBytePair();
        _currentPage.Instructions[startIndex] = left;
        _currentPage.Instructions[startIndex + 1] = right;
        Emit(InstructionKind.EndFor);
    }

    private int EmitTry()
    {
        Emit(InstructionKind.Try, 0, 0);

        return _currentPage.Instructions.Count - 2;
    }
}