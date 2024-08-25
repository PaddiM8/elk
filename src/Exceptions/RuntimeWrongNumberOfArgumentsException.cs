namespace Elk.Exceptions;

class RuntimeWrongNumberOfArgumentsException(string? symbolName, int? expected, int got, bool variadic = false)
    : RuntimeException(GetMessage(symbolName, expected, got, variadic))
{
    private static string GetMessage(string? symbolName, int? expected, int got, bool variadic)
    {
        var forName = $" for '{symbolName}'";
        return variadic
            ? $"Wrong number of arguments{forName}. Expected {expected} or more but got {got}"
            : $"Wrong numbers of arguments{forName}. Expected {expected} but got {got}";
    }
}