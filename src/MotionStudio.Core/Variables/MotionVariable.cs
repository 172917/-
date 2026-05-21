namespace MotionStudio.Core.Variables;

/// <summary>
/// 流程变量。
/// </summary>
public sealed class MotionVariable
{
    public string Name { get; set; } = string.Empty;

    public object? Value { get; set; }
}
