using Elk.Lexing;

namespace Elk.Vm;

public record struct Variable(Token Name, int Depth);