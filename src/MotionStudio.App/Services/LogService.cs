using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
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

    public void Write(LogLevel level, string source, string message, string apiCall = "")
    {
        var entry = new LogEntry
        {
            Time = DateTime.Now,
            Level = level,
            Source = source,
            Message = message,
            ApiCall = apiCall ?? string.Empty
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

    public string ExportToCsv()
    {
        var logDirectory = ResolveLogDirectory();
        Directory.CreateDirectory(logDirectory);

        var filePath = Path.Combine(
            logDirectory,
            $"MotionStudio_Log_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

        var entries = Logs
            .OrderBy(entry => entry.Time)
            .ToList();

        var builder = new StringBuilder();
        builder.AppendLine("时间,级别,来源,内容,API调用");

        foreach (var entry in entries)
        {
            builder
                .Append(ToCsvField(entry.Time.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture))).Append(',')
                .Append(ToCsvField(entry.Level.ToString())).Append(',')
                .Append(ToCsvField(entry.Source)).Append(',')
                .Append(ToCsvField(entry.Message)).Append(',')
                .Append(ToCsvField(entry.ApiCall))
                .AppendLine();
        }

        File.WriteAllText(filePath, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        return filePath;
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

    private static string ResolveLogDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MotionStudio.sln"))
                || File.Exists(Path.Combine(directory.FullName, "README.md")))
            {
                return Path.Combine(directory.FullName, "logs");
            }

            directory = directory.Parent;
        }

        return Path.Combine(AppContext.BaseDirectory, "logs");
    }

    private static string ToCsvField(string? value)
    {
        value ??= string.Empty;
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
