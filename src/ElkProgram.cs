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
            null,
            semanticInformationOnly: true
        );
    }

    internal static EvaluationResult Evaluate(
        string input,
        Scope scope,
        AnalysisScope analysisScope,
        VirtualMachine? virtualMachine,
        bool semanticInformationOnly = false)
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
                scope,
                ignoreErrors: semanticInformationOnly
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
        var semanticTokens = semanticInformationOnly
            ? ast.GetSemanticTokens()
            : null;

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
                Ast = ast,
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

        var generated = GeneratePages(ast, scope, analysisScope, virtualMachine).ToList();
        RuntimeObject? result = null;
        if (virtualMachine != null)
        {
            result = generated
                .Select(pair => virtualMachine.Execute(pair.page!))
                .LastOrDefault();
        }

        return new EvaluationResult
        {
            Ast = generated.LastOrDefault().analysedAst,
            Value = result,
        };
    }

    private static IEnumerable<(Ast analysedAst, Page? page)> GeneratePages(
        Ast ast,
        Scope scope,
        AnalysisScope analysisScope,
        VirtualMachine? virtualMachine)
    {
        Debug.Assert(scope.ModuleScope is not { Ast: null });

        var importedModules = scope.ModuleScope.ImportedModules
            .Where(x => x != scope)
            .Where(x =>
                x.AnalysisStatus != AnalysisStatus.Failed &&
                    x.AnalysisStatus != AnalysisStatus.Evaluated
            );
        var pages = EvaluateModules(importedModules, virtualMachine)
            .Concat(EvaluateModules(scope.ModuleScope.Modules, virtualMachine));

        var analysedAst = Analyser.Analyse(ast, scope.ModuleScope, analysisScope);
        var result = virtualMachine?.Generate(analysedAst);

        return pages.Append((analysedAst, result));
    }

    private static IEnumerable<(Ast, Page?)> EvaluateModules(
        IEnumerable<ModuleScope> modules,
        VirtualMachine? virtualMachine)
    {
        return modules.SelectMany(module =>
        {
            if (module.AnalysisStatus == AnalysisStatus.None)
                Analyser.Analyse(module.Ast, module, AnalysisScope.OncePerModule);

            module.AnalysisStatus = AnalysisStatus.Evaluated;

            return GeneratePages(module.Ast, module, AnalysisScope.OncePerModule, virtualMachine);
        });
    }
}