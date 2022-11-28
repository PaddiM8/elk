using System;
using System.Collections.Generic;
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
        bool globbingEnabled,
        bool isRoot)
    {
        string programName = arguments[0].As<RuntimeString>().Value;

        return EvaluateProgramCall(
            programName,
            arguments.GetRange(1, arguments.Count - 1),
            pipedValue: null,
            globbingEnabled,
            isRoot
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

    private RuntimeObject EvaluateBuiltInClosure(List<RuntimeObject> arguments)
    {
        if (_currentClosureExpr == null)
            throw new RuntimeException("Can only call 'closure' function inside function declarations that have '=> closure' in the signature.");

        var scope = _currentClosureExpr.Body.Scope;
        scope.Clear();
        foreach (var (argument, i) in arguments.WithIndex())
        {
            var parameter = _currentClosureExpr.Parameters.ElementAtOrDefault(i)?.Value ??
                throw new RuntimeException($"Expected exactly {_currentClosureExpr.Parameters.Count} closure parameter(s)");
            scope.AddVariable(parameter, argument);
        }

        return NextBlock(_currentClosureExpr.Body, clearScope: false);
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
                actualArguments, runtimeSymbolFunction.FunctionSymbol.Expr, isRoot
            ),
            RuntimeClosureFunction runtimeClosure => EvaluateRuntimeClosure(actualArguments, runtimeClosure, isRoot),
            _ => EvaluateProgramCall(
                ((RuntimeProgramFunction)functionReference).ProgramName,
                actualArguments,
                pipedValue: null,
                globbingEnabled: false,
                isRoot
            ),
        };
    }

    private RuntimeObject EvaluateRuntimeClosure(
        List<RuntimeObject> arguments,
        RuntimeClosureFunction runtimeClosure,
        bool isRoot)
    {
        var functionReferenceExpr = (FunctionReferenceExpr)runtimeClosure.Closure.Function;
        var innerFunction = functionReferenceExpr.RuntimeFunction!;

        if (innerFunction is RuntimeStdFunction runtimeStdFunction)
        {
            return EvaluateStdCall(
                arguments,
                runtimeStdFunction.StdFunction,
                runtimeClosure.Closure
            );
        }

        if (innerFunction is not RuntimeSymbolFunction runtimeSymbolFunction)
            throw new RuntimeException("Closures are not supported for built-in non-std functions");

        return EvaluateFunctionCall(
            arguments,
            runtimeSymbolFunction.FunctionSymbol.Expr,
            isRoot,
            runtimeClosure.Closure
        );
    }

    private RuntimeError EvaluateBuiltInError(List<RuntimeObject> arguments)
    {
        if (arguments.Count != 1)
            throw new RuntimeWrongNumberOfArgumentsException(1, arguments.Count, true);

        return new RuntimeError(
            arguments[0].As<RuntimeString>().Value,
            _lastExpr?.Position ?? TextPos.Default
        );
    }
}