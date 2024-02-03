using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Elk.Analysis;
using Elk.Interpreting;
using Elk.Interpreting.Exceptions;
using Elk.Interpreting.Scope;
using Elk.Lexing;
using Elk.Parsing;
using Elk.Std.DataTypes;

namespace Elk;

public static class ElkProgram
{
    public static RuntimeObject? Evaluate(
        string input,
        Scope scope,
        AnalysisScope analysisScope)
    {
        return Evaluate(input, scope, analysisScope, null);
    }

    internal static RuntimeObject? Evaluate(
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

        return interpreter != null
            ? Evaluate(ast, scope, analysisScope, interpreter)
            : null;
    }

    internal static RuntimeObject? Evaluate(
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

        var analysedAst = Analyser.Analyse(ast, scope.ModuleScope, analysisScope);
        var result = interpreter?.Interpret(analysedAst, scope);
        EvaluateModules(scope.ModuleScope.Modules, interpreter);

        return result;
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