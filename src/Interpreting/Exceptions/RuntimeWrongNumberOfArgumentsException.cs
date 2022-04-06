using System;

namespace Shel.Interpreting;

class RuntimeWrongNumberOfArgumentsException : RuntimeException
{
    public RuntimeWrongNumberOfArgumentsException(int expected, int got)
        : base($"Wrong numbers of arguments. Expected {expected} but got {got}.")
    {
    }
}