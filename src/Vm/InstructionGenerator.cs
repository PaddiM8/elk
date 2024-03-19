using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Elk.Interpreting.Exceptions;
using Elk.Lexing;
using Elk.Parsing;
using Elk.Std.Bindings;
using Elk.Std.DataTypes;

namespace Elk.Vm;

class InstructionGenerator
{
    private readonly Stack<Variable> _locals = new();
    private readonly FunctionTable _functionTable;
    private readonly InstructionExecutor _executor;
    private readonly Token _emptyToken = new(TokenKind.Identifier, string.Empty, TextPos.Default);
    private Page _currentPage = new("<root>");
    private int _currentBasePointer;
    private int _scopeDepth;

    private InstructionGenerator(FunctionTable functionTable, InstructionExecutor executor)
    {
        _functionTable = functionTable;
        _executor = executor;
    }

    public static Page Generate(Ast ast, FunctionTable functionTable, InstructionExecutor executor)
    {
        var generator = new InstructionGenerator(functionTable, executor);
        foreach (var expr in ast.Expressions)
        {
            generator.Next(expr);
            var shouldPop = expr is not (ModuleExpr or FunctionExpr or StructExpr or LetExpr or KeywordExpr) ||
                (expr is LetExpr letExpr && letExpr.Symbols.Any(x => x.IsCaptured));
            if (shouldPop)
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
            // TODO: Try
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

        foreach (var (parameter, i) in expr.Parameters.WithIndex())
        {
            _locals.Push(new Variable(parameter.Identifier, 0));

            // If the parameter is captured, load it and store it as an upper
            // variable as well. For simplicity, the regular variable is kept
            // as well for now
            var symbol = expr.Block.Scope.FindVariable(parameter.Identifier.Value);
            if (symbol?.IsCaptured is true)
            {
                EmitBig(InstructionKind.Load, i);
                EmitBig(InstructionKind.StoreUpper, symbol);
                Emit(InstructionKind.Pop);
            }
        }

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

        var symbols = expr.Symbols.ToList();
        if (!symbols.Any(x => x.IsCaptured))
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
        _locals.Push(new Variable(_emptyToken, _scopeDepth + 1));

        var symbols = expr.IdentifierList
            .Select(x => expr.Branch.Scope.FindVariable(x.Value))
            .Where(x => x != null)
            .ToList();
        var isCaptured = symbols.Any(x => x?.IsCaptured is true);
        if (!isCaptured)
        {
            foreach (var identifier in expr.IdentifierList)
                _locals.Push(new Variable(identifier, _scopeDepth + 1));
        }

        Emit(InstructionKind.GetIter);
        var loopBackIndex = CreateBackwardJumpPoint();
        var startIndex = EmitForIter();

        if (isCaptured)
        {
            // If just one variable in the identifier list is captured,
            // the other ones will be treated as captured variables too,
            // for simplicity
            foreach (var symbol in symbols)
                symbol!.IsCaptured = true;

            if (expr.IdentifierList.Count > 1)
            {
                foreach (var symbol in symbols)
                    EmitBig(InstructionKind.Const, symbol!);

                Emit(InstructionKind.UnpackUpper, (byte)expr.IdentifierList.Count);
                Emit(InstructionKind.Pop);
            }
            else
            {
                EmitBig(InstructionKind.StoreUpper, symbols.First()!);
            }
        }
        else
        {
            if (expr.IdentifierList.Count > 1)
                Emit(InstructionKind.Unpack, (byte)expr.IdentifierList.Count);
        }

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
            var shouldPop = child is not (LetExpr or KeywordExpr) ||
                (child is LetExpr letExpr && letExpr.Symbols.Any(x => x.IsCaptured));
            if (shouldPop)
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
                throw new NotImplementedException("Keyword not implemented: " + expr.Keyword);
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

            var symbol = leftValue.Scope.FindVariable(leftValue.Identifier.Value);
            if (symbol?.IsCaptured is true)
            {
                EmitBig(InstructionKind.StoreUpper, leftValue.Scope.FindVariable(leftValue.Identifier.Value)!);
            }
            else
            {
                EmitBig(InstructionKind.Store, ResolveVariable(leftValue.Identifier.Value));
            }
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
        var symbol = expr.Scope.FindVariable(expr.Identifier.Value);
        if (symbol?.IsCaptured is true)
        {
            EmitBig(InstructionKind.LoadUpper, expr.Scope.FindVariable(expr.Identifier.Value)!);
        }
        else
        {
            EmitBig(InstructionKind.Load, ResolveVariable(expr.Identifier.Value));
        }
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
            default:
                EmitProgramCall(expr, isMaybeRoot);
                break;
        }
    }

    private int EmitArguments(CallExpr expr, bool skipFirst = false)
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
                .Select(x => (
                    defaultValues: x.item.IsNullable ? nilExpr : null,
                    isVariadic: x.index == expr.StdFunction.VariadicStart
                ));
        }

        // Program calls are always variadic
        var skipCount = skipFirst ? 1 : 0;
        var arguments = new List<Expr>();
        var variadicArguments = expr.FunctionSymbol == null && expr.StdFunction == null
            ? new List<(Expr expr, bool isGlob)>()
            : null;
        foreach (var (argument, parameter) in expr.Arguments.ZipLongest(parameters).Skip(skipCount))
        {
            if (argument == null)
            {
                if (parameter.defaultValue != null)
                    arguments.Add(parameter.defaultValue);

                continue;
            }

            if (parameter.isVariadic)
                variadicArguments = new List<(Expr expr, bool isGlob)>();

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
            foreach (var (variadicArgument, isGlob) in variadicArguments)
            {
                Next(variadicArgument);
                if (isGlob)
                    Emit(InstructionKind.Glob);
            }

            // Dynamic function calls do this on the fly
            if (!expr.IsReference)
            {
                Emit(InstructionKind.BuildList);
                Emit((ushort)variadicArguments.Count);
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
            return (arguments, isRoot) => _executor.ExecuteFunction(
                (RuntimeUserFunction)function,
                arguments,
                isRoot
            );
        };

        if (expr.FunctionSymbol.Expr.Parameters.Count > byte.MaxValue)
            throw new RuntimeException("Too many parameters. A function can have at most 255 parameters.");

        var variadicStart = expr.FunctionSymbol.Expr.Parameters
            .FindIndex(x => x.IsVariadic);
        var runtimeFunction = new RuntimeUserFunction(
            null!,
            _functionTable.Get(expr.FunctionSymbol),
            null,
            expr.Plurality,
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
                : null
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
            return (arguments, _) => _executor.ExecuteFunction(
                (RuntimeStdFunction)function,
                arguments
            );
        };

        var functionReference = new RuntimeStdFunction(
            expr.StdFunction,
            null,
            expr.Plurality,
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

        // TODO: If the std function takes a ShellEnvironment, add that as an argument here too
        if (expr.IsReference)
        {
            var referenceArgumentCount = EmitArguments(expr);
            if (referenceArgumentCount > byte.MaxValue)
                throw new RuntimeException("Too many arguments. A call can have at most 255 function arguments.");

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

        argumentCount += EmitArguments(expr);

        if (argumentCount > byte.MaxValue)
            throw new RuntimeException("Too many arguments. A call can have at most 255 function arguments.");

        EmitBig(InstructionKind.Const, functionReference);
        Emit(InstructionKind.CallStd, (byte)argumentCount);
    }

    private object ConstructClosureFunc(Type closureFuncType, RuntimeFunction runtimeFunction)
    {
        if (closureFuncType == typeof(Func<RuntimeObject>))
        {
            // TODO: Set isRoot
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
        if (closureFuncType == typeof(Action<RuntimeObject>))
        {
            return new Action<RuntimeObject>(a =>
                runtimeFunction.Invoker([a], isRoot: false)
            );
        }

        if (closureFuncType == typeof(Action<RuntimeObject, RuntimeObject>))
        {
            return new Action<RuntimeObject, RuntimeObject>((a, b) =>
                runtimeFunction.Invoker([a, b], isRoot: false)
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
            throw new RuntimeWrongNumberOfArgumentsException(1, 0, variadic: true);

        // The function reference
        Next(expr.Arguments.First());

        // The first argument should be the function reference, so that shouldn't be emitted
        // as an argument to the actual call.
        foreach (var argument in expr.Arguments.Skip(1).Reverse())
            Next(argument);

        var argumentCount = expr.Arguments.Count - 1;
        if (argumentCount > byte.MaxValue)
            throw new RuntimeException("Too many arguments. A call can have at most 255 function arguments.");

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
        // The function reference
        EmitBig(InstructionKind.Load, ResolveVariable("closure"));

        // Arguments
        foreach (var argument in expr.Arguments.AsEnumerable().Reverse())
            Next(argument);

        if (expr.Arguments.Count > byte.MaxValue)
            throw new RuntimeException("Too many arguments. A call can have at most 255 function arguments.");

        Emit(InstructionKind.ResolveArgumentsDynamically, (byte)expr.Arguments.Count);

        byte isRootModifier = (expr.IsRoot, isMaybeRoot) switch
        {
            (_, true) => 2, // Maybe
            (true, _) => 1, // Yes
            _ => 0, // No
        };
        Emit(InstructionKind.DynamicCall, isRootModifier);
    }

    private void EmitProgramCall(CallExpr expr, bool isMaybeRoot = false)
    {
        // Arguments
        var argumentCount = EmitArguments(expr);

        Func<RuntimeFunction, Invoker> invoker = function =>
        {
            return (arguments, isRoot) => _executor.ExecuteFunction(
                (RuntimeProgramFunction)function,
                arguments,
                isRoot
            );
        };

        // Program reference
        var runtimeFunction = new RuntimeProgramFunction(
            expr.Identifier.Value,
            null,
            expr.Plurality,
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

    private void Visit(ClosureExpr expr, bool isMaybeRoot = false)
    {
        var previousPage = _currentPage;
        var page = new Page(name: null);
        _currentPage = page;

        foreach (var (parameter, i) in expr.Parameters.WithIndex())
        {
            _locals.Push(new Variable(parameter, 0));

            // If the parameter is captured, load it and store it as an upper
            // variable as well. For simplicity, the regular variable is kept
            // as well for now
            var symbol = expr.Body.Scope.FindVariable(parameter.Value);
            if (symbol?.IsCaptured is true)
            {
                EmitBig(InstructionKind.Load, i);
                EmitBig(InstructionKind.StoreUpper, symbol);
                Emit(InstructionKind.Pop);
            }
        }

        var previousBasePointer = _currentBasePointer;
        _currentBasePointer = _locals.Count - 1;

        Next(expr.Body);
        _currentBasePointer = previousBasePointer;

        Emit(InstructionKind.Ret);
        _currentPage = previousPage;

        foreach (var _ in expr.Parameters)
            _locals.Pop();

        if (expr.Parameters.Count > byte.MaxValue)
            throw new RuntimeException("Too many parameters. A function can have at most 255 parameters");

        // TODO: Remove unused parts of RuntimeFunction after removing the interpreter
        Func<RuntimeFunction, Invoker> invoker = function =>
        {
            return (arguments, isRoot) => _executor.ExecuteFunction(
                (RuntimeUserFunction)function,
                arguments,
                isRoot
            );
        };

        var runtimeFunction = new RuntimeUserFunction(
            null!,
            page,
            null,
            Plurality.Singular,
            invoker
        )
        {
            ParameterCount = (byte)expr.Parameters.Count,
            DefaultParameters = [],
            VariadicStart = null,
        };

        Visit(expr.Function, isMaybeRoot, runtimeFunction);
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