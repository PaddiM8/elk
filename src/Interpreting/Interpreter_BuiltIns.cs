using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Elk.Interpreting.Exceptions;
using Elk.Lexing;
using Elk.Parsing;
using Elk.Std.DataTypes;

namespace Elk.Interpreting;

partial class Interpreter
{
    private RuntimeObject EvaluateBuiltInCd(List<RuntimeObject> arguments)
    {
        if (arguments.Count > 1)
            throw new RuntimeWrongNumberOfArgumentsException(1, arguments.Count);

        var argument = arguments.Any()
            ? arguments.First().As<RuntimeString>().Value
            : "";
        if (argument == "")
        {
            ShellEnvironment.WorkingDirectory = "";
            return RuntimeNil.Value;
        }

        var path = argument == "-"
            ? Environment.GetEnvironmentVariable("OLDPWD") ?? ""
            : ShellEnvironment.GetAbsolutePath(argument);

        if (Directory.Exists(path))
        {
            Environment.SetEnvironmentVariable("OLDPWD", ShellEnvironment.WorkingDirectory);
            ShellEnvironment.WorkingDirectory = path;
        }
        else
        {
            throw new RuntimeException(
                $"cd: The directory \"{path}\" does not exist",
                Position,
                _lastExpr?.EndPosition ?? TextPos.Default
            );
        }

        return RuntimeNil.Value;
    }

    private RuntimeObject EvaluateBuiltInExec(
        List<RuntimeObject> arguments,
        RedirectionKind redirectionKind,
        bool disableRedirectionBuffering)
    {
        var programName = arguments[0].As<RuntimeString>().Value;

        return EvaluateProgramCall(
            programName,
            arguments.GetRange(1, arguments.Count - 1),
            pipedValue: null,
            redirectionKind,
            disableRedirectionBuffering,
            automaticStart: true,
            environmentVariables: null
        );
    }

    private RuntimeObject EvaluateBuiltInScriptPath(List<RuntimeObject> arguments)
    {
        if (arguments.Any())
            throw new RuntimeWrongNumberOfArgumentsException(0, arguments.Count);

        var path = _lastExpr!.StartPosition.FilePath == null
            ? ShellEnvironment.WorkingDirectory
            : Path.GetDirectoryName(_lastExpr.StartPosition.FilePath)!;

        return new RuntimeString(path);
    }

    private RuntimeObject EvaluateBuiltInClosure(FunctionExpr enclosingFunction, List<RuntimeObject> arguments)
    {
        var givenClosure = enclosingFunction.GivenClosure!;
        var scope = givenClosure.Environment;
        var parameters = givenClosure.Expr.Parameters;
        foreach (var (argument, i) in arguments.WithIndex())
        {
            var parameter = parameters.ElementAtOrDefault(i)?.Value ??
                throw new RuntimeException($"Expected exactly {parameters.Count} closure parameter(s)");
            scope.AddVariable(parameter, argument);
        }

        return NextBlock(givenClosure.Expr.Body);
    }

    private RuntimeObject EvaluateBuiltInCall(List<RuntimeObject> arguments, bool isRoot)
    {
        if (arguments.Count == 0)
            throw new RuntimeWrongNumberOfArgumentsException(1, 0, true);

        var functionReference = arguments
            .First()
            .As<RuntimeFunction>();
        var actualArguments = arguments
            .Skip(1)
            .Concat(functionReference.Arguments)
            .ToList();

        return EvaluateBuiltInCall(functionReference, actualArguments, isRoot);
    }

    private RuntimeObject EvaluateBuiltInCall(RuntimeFunction functionReference, List<RuntimeObject> arguments, bool isRoot)
    {
        if (arguments.Count == 0)
            throw new RuntimeWrongNumberOfArgumentsException(1, 0, true);

        RuntimeObject Evaluate()
            => functionReference switch
            {
                RuntimeStdFunction runtimeStdFunction => EvaluateStdCall(arguments, runtimeStdFunction.StdFunction),
                RuntimeSymbolFunction runtimeSymbolFunction => EvaluateFunctionCall(
                    arguments,
                    runtimeSymbolFunction.FunctionSymbol.Expr,
                    isRoot
                ),
                RuntimeClosureFunction runtimeClosure => EvaluateRuntimeClosure(arguments, runtimeClosure, isRoot),
                _ => EvaluateProgramCall(
                    ((RuntimeProgramFunction)functionReference).ProgramName,
                    arguments,
                    pipedValue: null,
                    RedirectionKind.None,
                    disableRedirectionBuffering: false,
                    automaticStart: true,
                    environmentVariables: null
                ),
            };

        if (functionReference.Plurality == Plurality.Singular || arguments.Count == 0)
            return Evaluate();

        if (arguments.First() is not IEnumerable<RuntimeObject> firstArguments)
            throw new RuntimeCastException(arguments.First().GetType(), "Iterable");

        var evaluatedWithPlurality = firstArguments.Select(x =>
        {
            arguments[0] = x;
            return Evaluate();
        });

        return new RuntimeList(evaluatedWithPlurality.ToList());
    }

    private RuntimeObject EvaluateRuntimeClosure(
        List<RuntimeObject> arguments,
        RuntimeClosureFunction runtimeClosure,
        bool isRoot)
    {
        var call = runtimeClosure.Expr.Function;
        if (call.StdFunction != null)
        {
            var stdResult = EvaluateStdCall(
                arguments,
                call.StdFunction,
                runtimeClosure
            );

            return stdResult;
        }

        if (call.FunctionSymbol == null)
            throw new RuntimeException("Closures are not supported for built-in non-std functions");

        var result = EvaluateFunctionCall(
            arguments,
            call.FunctionSymbol.Expr,
            isRoot
        );

        return result;
    }
}