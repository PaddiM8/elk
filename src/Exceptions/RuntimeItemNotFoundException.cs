namespace Elk.Exceptions;

class RuntimeItemNotFoundException(string item) : RuntimeException($"The item '{item}' was not found");