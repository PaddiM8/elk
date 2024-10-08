using System;
using System.Collections.Generic;
using Elk.Std.Attributes;
using Elk.Std.DataTypes;

namespace Elk.Std;

/// <summary>
/// Used to generate documentation for the hardcoded functions
/// in Interpreter_BuiltIns.cs. These methods do not contain
/// the actual implementation.
/// </summary>
[ElkModule("elk")]
public class HardCoded
{
    /// <summary>
    /// Executes a program.
    /// </summary>
    /// <param name="programName">The name of the program to execute.</param>
    [ElkFunction("exec", Reachability.Everywhere)]
    public static RuntimeObject Exec(RuntimeString programName)
        => throw new NotSupportedException();

    /// <summary>
    /// Calls the closure given to the function call used to call
    /// the containing function. This function can only be called
    /// from within function definitions for functions that take
    /// a closure.
    /// </summary>
    /// <param name="closureArguments">Arguments for the closure call.</param>
    /// <returns>The result of the closure call.</returns>
    [ElkFunction("closure", Reachability.Everywhere)]
    public static RuntimeObject Closure([ElkVariadic] IEnumerable<RuntimeObject> closureArguments)
        => throw new NotSupportedException();

    /// <summary>Calls a function with the help of a function reference.</summary>
    /// <param name="function">A reference to the function that should be called.</param>
    /// <param name="callArguments">Arguments for the function call.</param>
    /// <returns>The value returned by the function that was called.</returns>
    [ElkFunction("call", Reachability.Everywhere)]
    public static RuntimeObject Call(RuntimeFunction function, [ElkVariadic] IEnumerable<RuntimeObject> callArguments)
        => throw new NotSupportedException();

    /// <summary>
    /// Evaluate a file in the context of the current environment.
    /// </summary>
    /// <param name="path">Path to the script to evaluate.</param>
    [ElkFunction("source", Reachability.Everywhere)]
    public static RuntimeObject Source(RuntimeString path)
        => throw new NotSupportedException();
}
