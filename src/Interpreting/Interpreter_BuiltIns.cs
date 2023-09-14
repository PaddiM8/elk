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

        string argument = arguments.Any()
            ? arguments.First().As<RuntimeString>().Value
            : "";
        if (argument == "")
        {
            ShellEnvironment.WorkingDirectory = "";
            return RuntimeNil.Value;
        }

        string path = argument == "-"
            ? Environment.GetEnvironmentVariable("OLDPWD") ?? ""
            : ShellEnvironment.GetAbsolutePath(argument);

        if (Directory.Exists(path))
        {
            Environment.SetEnvironmentVariable("OLDPWD", ShellEnvironment.WorkingDirectory);
            ShellEnvironment.WorkingDirectory = path;
        }
        else
        {
            return Error($"cd: The directory \"{path}\" does not exist");
        }

        return RuntimeNil.Value;
    }

    private RuntimeObject EvaluateBuiltInExec(
        List<RuntimeObject> arguments,
        RedirectionKind redirectionKind,
        bool globbingEnabled)
    {
        string programName = arguments[0].As<RuntimeString>().Value;

        return EvaluateProgramCall(
            programName,
            arguments.GetRange(1, arguments.Count - 1),
            pipedValue: null,
            redirectionKind,
            globbingEnabled
        );
    }

    private RuntimeObject EvaluateBuiltInScriptPath(List<RuntimeObject> arguments)
    {
        if (arguments.Any())
            throw new RuntimeWrongNumberOfArgumentsException(0, arguments.Count);

        string path = _lastExpr!.Position.FilePath == null
            ? ShellEnvironment.WorkingDirectory
            : Path.GetDirectoryName(_lastExpr.Position.FilePath)!;

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

        var functionReference = arguments.First().As<RuntimeFunction>();
        var actualArguments = arguments.Skip(1).ToList();
        return functionReference switch
        {
            RuntimeStdFunction runtimeStdFunction => EvaluateStdCall(actualArguments, runtimeStdFunction.StdFunction),
            RuntimeSymbolFunction runtimeSymbolFunction => EvaluateFunctionCall(
                actualArguments,
                runtimeSymbolFunction.FunctionSymbol.Expr,
                isRoot
            ),
            RuntimeClosureFunction runtimeClosure => EvaluateRuntimeClosure(actualArguments, runtimeClosure, isRoot),
            _ => EvaluateProgramCall(
                ((RuntimeProgramFunction)functionReference).ProgramName,
                actualArguments,
                pipedValue: null,
                RedirectionKind.None,
                globbingEnabled: false
            ),
        };
    }

    private RuntimeObject EvaluateRuntimeClosure(
        List<RuntimeObject> arguments,
        RuntimeClosureFunction runtimeClosure,
        bool isRoot)
    {
        var functionReferenceExpr = (FunctionReferenceExpr)runtimeClosure.Expr.Function;

        var innerFunction = functionReferenceExpr.RuntimeFunction!;
        if (innerFunction is RuntimeStdFunction runtimeStdFunction)
        {
            var stdResult = EvaluateStdCall(
                arguments,
                runtimeStdFunction.StdFunction,
                runtimeClosure
            );

            return stdResult;
        }

        if (innerFunction is not RuntimeSymbolFunction runtimeSymbolFunction)
            throw new RuntimeException("Closures are not supported for built-in non-std functions");

        var result = EvaluateFunctionCall(
            arguments,
            runtimeSymbolFunction.FunctionSymbol.Expr,
            isRoot
        );

        return result;
    }

    private RuntimeError EvaluateBuiltInError(List<RuntimeObject> arguments)
    {
        if (arguments.Count != 1)
            throw new RuntimeWrongNumberOfArgumentsException(1, arguments.Count);

        return new RuntimeError(
            arguments[0].As<RuntimeString>().Value,
            _lastExpr?.Position ?? TextPos.Default
        );
    }

    private RuntimeObject EvaluateBuiltInTime(IList<Expr> arguments)
    {
        if (arguments.Count != 1)
            throw new RuntimeWrongNumberOfArgumentsException(1, arguments.Count);

        var stopWatch = new Stopwatch();
        stopWatch.Start();
        var result = Next(arguments.Single());
        stopWatch.Stop();

        long milliseconds = Math.Max(0, stopWatch.ElapsedMilliseconds - 1);
        string paddedMilliseconds = milliseconds.ToString().PadLeft(3, '0');
        Console.WriteLine($"time: {stopWatch.Elapsed.Minutes}m{stopWatch.Elapsed.Seconds}.{paddedMilliseconds}\n");

        return result;
    }
}