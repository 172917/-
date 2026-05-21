namespace MotionStudio.Core.Logging;

/// <summary>
/// 流程运行日志。
/// </summary>
public sealed class LogEntry
{
    public DateTime Time { get; set; } = DateTime.Now;

    public LogLevel Level { get; set; } = LogLevel.Info;

    public string Source { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
}
