namespace Elk.Interpreting.Exceptions;

class RuntimeModuleNotFoundException : RuntimeException
{
    public RuntimeModuleNotFoundException(string name)
        : base($"No such module: {name}")
    {
    }
}