namespace Elk.Vm;

record struct Instruction(
    InstructionKind Kind,
    byte? Argument = null
);