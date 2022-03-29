using System;

class ShellFunctionAttribute : Attribute
{
    public string Name { get; }

    public ShellFunctionAttribute(string name)
    {
        Name = name;
    }
}