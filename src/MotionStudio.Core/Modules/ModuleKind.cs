namespace MotionStudio.Core.Modules;

/// <summary>
/// 流程模块类型，预留条件分支和子流程扩展。
/// </summary>
public enum ModuleKind
{
    Normal,
    If,
    EndIf,
    Loop,
    LoopEnd,
    SubProcess
}
