using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Elk.Analysis;
using Elk.Exceptions;
using Elk.Lexing;
using Elk.Parsing;
using Elk.Scoping;
using Elk.Services;
using Elk.Std.DataTypes;
using Elk.Vm;

namespace Elk;

public class EvaluationResult
{
    public RuntimeObject? Value { get; init; }

    public Ast? Ast { get; init; }

    public IEnumerable<SemanticToken>? SemanticTokens { get; set; }

    public IEnumerable<DiagnosticMessage> Diagnostics { get; init;  } = Array.Empty<DiagnosticMessage>();
}

public static class ElkProgram
{
    public static EvaluationResult GetSemanticInformation(string input, Scope scope)
    {
        return Evaluate(
            input,
            scope,
            AnalysisScope.OverwriteExistingModule,
            null
        );
    }

    internal static EvaluationResult Evaluate(
        string input,
        Scope scope,
        AnalysisScope analysisScope,
        VirtualMachine? virtualMachine)
    {
        Ast ast;
        try
        {
            ast = Parser.Parse(
                Lexer.Lex(
                    input,
                    scope.ModuleScope.FilePath,
                    out var error
                ),
                scope
            );

            if (error != null)
                throw error;
        }
        catch (RuntimeException ex)
        {
            var diagnostics = new List<DiagnosticMessage>();
            var result = new EvaluationResult
            {
                Diagnostics = diagnostics,
            };

            if (ex.StartPosition == null || ex.EndPosition == null)
                return result;

            var message = new DiagnosticMessage(ex.Message, ex.StartPosition, ex.EndPosition)
            {
                StackTrace = ex.ElkStackTrace,
            };

            if (ex.Message.Length > 0)
                diagnostics.Add(message);

            return result;
        }

        scope.ModuleScope.Ast = ast;
        var semanticTokens = ast.GetSemanticTokens();

        try
        {
            var result = Evaluate(
                ast,
                scope,
                analysisScope,
                virtualMachine
            );
            result.SemanticTokens = semanticTokens;

            return result;
        }
        catch (RuntimeException ex)
        {
            var diagnostics = new List<DiagnosticMessage>();
            if (ex is { StartPosition: not null, EndPosition: not null })
            {
                var message = new DiagnosticMessage(ex.Message, ex.StartPosition, ex.EndPosition)
                {
                    StackTrace = ex.ElkStackTrace,
                };

                if (ex.Message.Length > 0)
                    diagnostics.Add(message);
            }

            return new EvaluationResult
            {
                SemanticTokens = semanticTokens,
                Diagnostics = diagnostics,
            };
        }
    }

    internal static EvaluationResult Evaluate(
        Ast ast,
        Scope scope,
        AnalysisScope analysisScope,
        VirtualMachine? virtualMachine)
    {
        Debug.Assert(scope.ModuleScope is not { Ast: null });

        EvaluateModules(
            scope.ModuleScope.ImportedModules
                .Where(x => x != scope)
                .Where(x =>
                    x.AnalysisStatus != AnalysisStatus.Failed &&
                        x.AnalysisStatus != AnalysisStatus.Evaluated
                ),
            virtualMachine
        );

        var analysedAst = Analyser.Analyse(ast, scope.ModuleScope, analysisScope);
        RuntimeObject? result = null;
        if (virtualMachine != null)
        {
            var page = virtualMachine.Generate(analysedAst);

            // TODO: Is this right? Now it will execute some modules before generating
            // the instructions for others. Might work, might not? Maybe it needs to
            // first traverse the modules and generate instructions, and then traverse
            // and execute them? Maybe even do it lazily somehow?
            EvaluateModules(scope.ModuleScope.Modules, virtualMachine);
            result = virtualMachine.Execute(page);
        }

        return new EvaluationResult
        {
            Ast = analysedAst,
            Value = result,
        };
    }

    private static void EvaluateModules(IEnumerable<ModuleScope> modules, VirtualMachine? virtualMachine)
    {
        foreach (var module in modules)
        {
            if (module.AnalysisStatus == AnalysisStatus.None)
                Analyser.Analyse(module.Ast, module, AnalysisScope.OncePerModule);

            module.AnalysisStatus = AnalysisStatus.Evaluated;
            Evaluate(module.Ast, module, AnalysisScope.OncePerModule, virtualMachine);
        }
    }
}