using System.Collections.ObjectModel;
using System.Windows;
using HandyControl.Controls;
using MotionStudio.Core.Logging;

namespace MotionStudio.App.Services;

/// <summary>
/// UI 日志服务，同时写入日志面板和 Growl。
/// </summary>
public sealed class LogService : ILogService
{
    public event EventHandler<LogEntry>? LogAdded;

    public ObservableCollection<LogEntry> Logs { get; } = new();

    public void Info(string source, string message) => Write(LogLevel.Info, source, message);

    public void Success(string source, string message) => Write(LogLevel.Success, source, message);

    public void Warning(string source, string message) => Write(LogLevel.Warning, source, message);

    public void Error(string source, string message) => Write(LogLevel.Error, source, message);

    public void Write(LogLevel level, string source, string message)
    {
        var entry = new LogEntry
        {
            Time = DateTime.Now,
            Level = level,
            Source = source,
            Message = message
        };

        void AddLog()
        {
            Logs.Insert(0, entry);
            LogAdded?.Invoke(this, entry);
            ShowGrowl(entry);
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            AddLog();
        }
        else
        {
            dispatcher.BeginInvoke(AddLog);
        }
    }

    private static void ShowGrowl(LogEntry entry)
    {
        var text = $"[{entry.Source}] {entry.Message}";
        switch (entry.Level)
        {
            case LogLevel.Success:
                Growl.Success(text);
                break;
            case LogLevel.Warning:
                Growl.Warning(text);
                break;
            case LogLevel.Error:
                Growl.Error(text);
                break;
            default:
                Growl.Info(text);
                break;
        }
    }
}
