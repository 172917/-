namespace MotionStudio.Core.Modules;

/// <summary>
/// 模块执行结果。
/// </summary>
public sealed class ModuleResult
{
    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;

    public static ModuleResult Ok(string message = "")
    {
        return new ModuleResult { Success = true, Message = message };
    }

    public static ModuleResult Fail(string message)
    {
        return new ModuleResult { Success = false, Message = message };
    }
}
