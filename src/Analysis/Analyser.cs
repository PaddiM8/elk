using System;
using System.Collections.Generic;
using System.Linq;
using Elk.Interpreting;
using Elk.Interpreting.Exceptions;
using Elk.Interpreting.Scope;
using Elk.Lexing;
using Elk.Parsing;
using Elk.Std.DataTypes;

namespace Elk.Analysis;

class Analyser
{
    private static Scope _scope = new ModuleScope();
    private ModuleBag _modules = new();

    public static List<Expr> Analyse(List<Expr> ast, ModuleBag modules, ModuleScope scope)
    {
        _scope = scope;

        var analyser = new Analyser
        {
            _modules = modules,
        };

        foreach (var functionSymbol in modules.SelectMany(x => x.Functions).Where(x => !x.Expr.IsAnalysed))
            analyser.Next(functionSymbol.Expr);

        return ast.Where(expr => expr is not FunctionExpr).Select(expr => analyser.Next(expr)).ToList();
    }

    private Expr Next(Expr expr)
    {
        switch (expr)
        {
            case FunctionExpr e:
                foreach (var parameter in e.Parameters)
                {
                    if (parameter.DefaultValue != null)
                        Next(parameter.DefaultValue!);

                    e.Block.Scope.AddVariable(parameter.Identifier.Value, RuntimeNil.Value);
                }

                var newFunction = new FunctionExpr(
                    e.Identifier,
                    e.Parameters,
                    (BlockExpr)Next(e.Block),
                    e.Module,
                    e.HasClosure,
                    isAnalysed: true
                );

                e.Module.AddFunction(newFunction);

                return newFunction;
            case LetExpr e:
                return new LetExpr(e.IdentifierList, Next(e.Value));
            case KeywordExpr e:
                Expr? keywordValue = null;
                if (e.Value != null)
                    keywordValue = Next(e.Value);

                return new KeywordExpr(e.Kind, keywordValue, e.Position);
            case IfExpr e:
                var ifCondition = Next(e.Condition);
                var thenBranch = Next(e.ThenBranch);
                Expr? elseBranch = null;
                if (e.ElseBranch != null)
                    elseBranch = Next(e.ElseBranch);

                return new IfExpr(ifCondition, thenBranch, elseBranch);
            case ForExpr e:
                var forValue = Next(e.Value);
                foreach (var identifier in e.IdentifierList)
                    e.Branch.Scope.AddVariable(identifier.Value, RuntimeNil.Value);

                var branch = (BlockExpr)Next(e.Branch);

                return new ForExpr(e.IdentifierList, forValue, branch);
            case WhileExpr e:
                var whileCondition = Next(e.Condition);
                var whileBranch = (BlockExpr)Next(e.Branch);

                return new WhileExpr(whileCondition, whileBranch);
            case TupleExpr e:
                var tupleValues = e.Values.Select(Next).ToList();

                return new TupleExpr(tupleValues, e.Position);
            case ListExpr e:
                var listValues = e.Values.Select(Next).ToList();

                return new ListExpr(listValues, e.Position);
            case DictionaryExpr e:
                foreach (var (_, value) in e.Entries)
                    Next(value);
                var dictEntries = e.Entries
                    .Select(x => (x.Item1, Next(x.Item2)))
                    .ToList();

                return new DictionaryExpr(dictEntries, e.Position);
            case BlockExpr e:
                _scope = e.Scope;
                var blockExpressions = e.Expressions.Select(Next).ToList();
                var newExpr = new BlockExpr(
                    blockExpressions,
                    e.ParentStructureKind,
                    e.Position,
                    e.Scope
                );
                _scope = _scope.Parent!;

                return newExpr;
            case LiteralExpr e:
                return Visit(e);
            case StringInterpolationExpr e:
                var parts = e.Parts.Select(Next).ToList();

                return new StringInterpolationExpr(parts, e.Position);
            case BinaryExpr e:
                return new BinaryExpr(Next(e.Left), e.Operator, Next(e.Right));
            case UnaryExpr e:
                return new UnaryExpr(e.Operator, Next(e.Value));
            case RangeExpr e:
                var from = e.From == null
                    ? null
                    : Next(e.From);
                var to = e.To == null
                    ? null
                    : Next(e.To);

                return new RangeExpr(from, to, e.Inclusive);
            case IndexerExpr e:
                return new IndexerExpr(Next(e.Value), Next(e.Index));
            case VariableExpr e:
                var variableExpr = new VariableExpr(e.Identifier, e.ModuleName);
                if (!e.Identifier.Value.StartsWith("$"))
                {
                    variableExpr.VariableSymbol = _scope.FindVariable(e.Identifier.Value);
                    if (variableExpr.VariableSymbol == null)
                        throw new RuntimeNotFoundException(e.Identifier.Value);
                }

                return variableExpr;
            case TypeExpr e:
                return Visit(e);
            case CallExpr e:
                return Visit(e);
        }

        return expr;
    }

    private LiteralExpr Visit(LiteralExpr expr)
    {
        IRuntimeValue value = expr.Value.Kind switch
        {
            TokenKind.IntegerLiteral => new RuntimeInteger(int.Parse(expr.Value.Value)),
            TokenKind.FloatLiteral => new RuntimeFloat(double.Parse(expr.Value.Value)),
            TokenKind.StringLiteral => new RuntimeString(expr.Value.Value),
            TokenKind.True => RuntimeBoolean.True,
            TokenKind.False => RuntimeBoolean.False,
            TokenKind.Nil => RuntimeNil.Value,
            _ => throw new ArgumentOutOfRangeException(),
        };

        var newExpr = new LiteralExpr(expr.Value)
        {
            RuntimeValue = value,
        };

        return newExpr;
    }

    private TypeExpr Visit(TypeExpr expr)
    {
        if (!LanguageInfo.RuntimeTypes.TryGetValue(expr.Identifier.Value, out var type))
            throw new RuntimeNotFoundException(expr.Identifier.Value);

        var newExpr = new TypeExpr(expr.Identifier)
        {
            RuntimeValue = new RuntimeType(type)
        };

        return newExpr;
    }

    private CallExpr Visit(CallExpr expr)
    {
        var newExpr = new CallExpr(
            expr.Identifier,
            expr.Arguments.Select(Next).ToList(),
            expr.CallStyle,
            expr.ModuleName
        );

        string name = expr.Identifier.Value;
        string? moduleName = expr.ModuleName?.Value;
        bool isStdModule = moduleName != null && StdGateway.ContainsModule(moduleName);
        if (isStdModule || StdGateway.Contains(name))
        {
            if (isStdModule && !StdGateway.Contains(name, moduleName))
                throw new RuntimeNotFoundException(name);

            newExpr.StdFunction = StdGateway.GetFunction(name, moduleName);

            return newExpr;
        }

        var module = expr.ModuleName == null
            ? _scope.ModuleScope
            : _modules.Find(expr.ModuleName.Value);
        if (module == null)
            throw new RuntimeModuleNotFoundException(expr.ModuleName!.Value);

        newExpr.FunctionSymbol = module.FindFunction(expr.Identifier.Value);

        return newExpr;
    }
}
