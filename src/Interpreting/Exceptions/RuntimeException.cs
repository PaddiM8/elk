#region

using System;

#endregion

namespace Elk.Interpreting.Exceptions;

class RuntimeException : Exception
{
    public RuntimeException(string message)
        : base(message)
    {
    }
}