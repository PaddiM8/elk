namespace Elk.Vm;

enum InstructionKind : byte
{
    Load,
    Store,
    Pop,
    Ret,
    Call,
    RootCall,
    MaybeRootCall,
    CallStd,
    CallProgram,
    RootCallProgram,
    MaybeRootCallProgram,

    Index,
    IndexStore,
    ConstIndex,
    ConstIndexStore,

    Const,
    Dict,

    Add,
    Sub,
    Mul,
    Div,
    Negate,
    Not,
    Equal,
    NotEqual,
    Greater,
    GreaterEqual,
    Less,
    LessEqual,
    And,
    Or,

    Jump,
    JumpBackward,
    JumpIf,
    JumpIfNot,
    PopJumpIf,
    PopJumpIfNot,
    GetIter,
    ForIter,
    EndFor,
}