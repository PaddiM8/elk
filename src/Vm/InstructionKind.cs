namespace Elk.Vm;

enum InstructionKind : byte
{
    Nop,
    Load,
    Store,
    LoadEnvironmentVariable,
    StoreEnvironmentVariable,
    LoadUpper,
    StoreUpper,
    LoadCaptured,
    StoreCaptured,
    Capture,
    Pop,
    PopArgs,
    Unpack,
    UnpackUpper,
    ExitBlock,
    Ret,
    Call,
    RootCall,
    MaybeRootCall,
    CallStd,
    CallProgram,
    RootCallProgram,
    MaybeRootCallProgram,
    /// <summary>
    /// `ResolveArgumentsDynamically` should be used before this
    /// </summary>
    DynamicCall,
    PushArgsToRef,
    ResolveArgumentsDynamically,
    Source,

    Index,
    IndexStore,
    New,
    BuildTuple,
    BuildList,
    BuildListBig,
    BuildGlobbedArgumentList,
    BuildSet,
    BuildDict,
    BuildRange,
    BuildString,
    BuildProgramCallReference,
    Const,
    StructConst,
    Glob,

    Add,
    Sub,
    Mul,
    Div,
    Pow,
    Mod,
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
    Contains,
    Coalesce,
    ErrorIsType,

    Jump,
    JumpBackward,
    JumpIf,
    JumpIfNot,
    PopJumpIf,
    PopJumpIfNot,
    GetIter,
    ForIter,
    EndFor,
    Try,
    EndTry,
    Throw,
}