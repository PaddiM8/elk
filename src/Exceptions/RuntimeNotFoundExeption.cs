namespace Elk.Exceptions;

class RuntimeNotFoundException(string identifier)
    : RuntimeException($"No such file/function/variable: {identifier}");