using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Elk.Interpreting.Exceptions;
using Elk.Parsing;
using Elk.Std.DataTypes;

namespace Elk.Interpreting;

partial class Interpreter
{
    private IRuntimeValue EvaluateBuiltInCd(List<IRuntimeValue> arguments)
    {
        EmptyRedirector(arguments);

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
            ShellEnvironment.WorkingDirectory = path;
        }
        else
        {
            return new RuntimeError($"cd: The directory \"{path}\" does not exist");
        }

        return RuntimeNil.Value;
    }

    private IRuntimeValue EvaluateBuiltInExec(
        List<IRuntimeValue> arguments,
        bool globbingEnabled,
        bool isRoot)
    {
        EmptyRedirector(arguments);

        string programName = arguments[0].As<RuntimeString>().Value;

        return CallProgram(
            programName,
            arguments.GetRange(1, arguments.Count - 1),
            globbingEnabled,
            isRoot
        );
    }

    private IRuntimeValue EvaluateBuiltInScriptPath(List<IRuntimeValue> arguments)
    {
        EmptyRedirector(arguments);

        if (arguments.Any())
            throw new RuntimeWrongNumberOfArgumentsException(0, arguments.Count);

        string path = _lastExpr!.Position.FilePath == null
            ? ShellEnvironment.WorkingDirectory
            : Path.GetDirectoryName(_lastExpr.Position.FilePath)!;

        return new RuntimeString(path);
    }

    private IRuntimeValue EvaluateBuiltInClosureCall(List<IRuntimeValue> arguments)
    {
        EmptyRedirector(arguments);

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

    private IRuntimeValue EvaluateBuiltInCall(List<IRuntimeValue> arguments, bool isRoot)
    {
        EmptyRedirector(arguments);

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
                globbingEnabled: false,
                isRoot
            ),
        };
    }

    private IRuntimeValue EvaluateRuntimeClosure(
        List<IRuntimeValue> arguments,
        RuntimeClosureFunction runtimeClosure,
        bool isRoot)
    {
        EmptyRedirector(arguments);

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

    private void EmptyRedirector(List<IRuntimeValue> arguments)
    {
        if (_redirector.Status == RedirectorStatus.HasData)
        {
            arguments.Insert(0, _redirector.Receive()!);
        }
    }
}