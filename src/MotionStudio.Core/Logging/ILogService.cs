namespace MotionStudio.Core.Logging;

/// <summary>
/// 日志服务接口，Core 层只依赖该抽象。
/// </summary>
public interface ILogService
{
    event EventHandler<LogEntry>? LogAdded;

    void Write(LogLevel level, string source, string message, string apiCall = "");
}
