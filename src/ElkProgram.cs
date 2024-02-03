using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Elk.Analysis;
using Elk.Highlighting;
using Elk.Interpreting;
using Elk.Interpreting.Exceptions;
using Elk.Interpreting.Scope;
using Elk.Lexing;
using Elk.Parsing;
using Elk.Std.DataTypes;

namespace Elk;

class EvaluationResult
{
    public RuntimeObject? Value { get; init; }

    public IList<SemanticToken>? SemanticTokens { get; init; }
}

public static class ElkProgram
{
    public static IList<SemanticToken> GetSemanticInformation(string input, Scope scope)
    {
        try
        {
            return Evaluate(
                input,
                scope,
                AnalysisScope.OverwriteExistingModule,
                null
            ).SemanticTokens!;
        }
        catch
        {
            // TODO: Return error messages
            return new List<SemanticToken>();
        }
    }

    internal static EvaluationResult Evaluate(
        string input,
        Scope scope,
        AnalysisScope analysisScope,
        Interpreter? interpreter)
    {
        var ast = Parser.Parse(
            Lexer.Lex(
                input,
                scope.ModuleScope.FilePath,
                out var lexError
            ),
            scope
        );
        scope.ModuleScope.Ast = ast;

        if (lexError != null)
            throw new RuntimeException(lexError.Message, lexError.Position);

        return Evaluate(ast, scope, analysisScope, interpreter);
    }

    internal static EvaluationResult Evaluate(
        IList<Expr> ast,
        Scope scope,
        AnalysisScope analysisScope,
        Interpreter? interpreter)
    {
        Debug.Assert(scope.ModuleScope is not { Ast: null });

        EvaluateModules(
            scope.ModuleScope.ImportedModules
                .Where(x => x != scope)
                .Where(x => x.AnalysisStatus != AnalysisStatus.Failed && x.AnalysisStatus != AnalysisStatus.Evaluated),
            interpreter
        );

        if (interpreter == null)
        {
            return new EvaluationResult
            {
                SemanticTokens = Analyser.GetSemanticTokens(ast, scope.ModuleScope),
            };
        }

        var analysedAst = Analyser.Analyse(ast, scope.ModuleScope, analysisScope);
        var result = new Interpreter(scope.ModuleScope.FilePath).Interpret(analysedAst, scope);
        EvaluateModules(scope.ModuleScope.Modules, interpreter);

        return new EvaluationResult
        {
            Value = result,
        };
    }

    private static void EvaluateModules(IEnumerable<ModuleScope> modules, Interpreter? interpreter)
    {
        foreach (var module in modules)
        {
            if (module.AnalysisStatus == AnalysisStatus.None)
                Analyser.Analyse(module.Ast, module, AnalysisScope.OncePerModule);

            module.AnalysisStatus = AnalysisStatus.Evaluated;
            Evaluate(module.Ast, module, AnalysisScope.OncePerModule, interpreter);
        }
    }
}