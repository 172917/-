using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using MotionStudio.Motion.Config;

namespace MotionStudio.App.Services;

/// <summary>
/// 全局运动配置读写服务。
/// </summary>
public sealed class MotionConfigService
{
    private readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public string ConfigFilePath { get; } = ResolveConfigFilePath();

    public MotionData LoadOrCreate()
    {
        if (!File.Exists(ConfigFilePath))
        {
            var defaultData = new MotionData();
            Save(defaultData);
            return defaultData;
        }

        var json = File.ReadAllText(ConfigFilePath);
        var data = JsonSerializer.Deserialize<MotionData>(json, _options) ?? new MotionData();
        data.Axes ??= new List<AxisBaseConfig>();
        data.IOs ??= new List<IOConfig>();
        data.Coordinates ??= new List<CoordinateConfig>();
        data.Positions ??= new List<PositionData>();
        return data;
    }

    public void Save(MotionData data)
    {
        var directory = Path.GetDirectoryName(ConfigFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(data, _options);
        File.WriteAllText(ConfigFilePath, json);
    }

    private static string ResolveConfigFilePath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MotionStudio.sln")))
            {
                return Path.Combine(directory.FullName, "configs", "MotionConfig", "motion-config.json");
            }

            directory = directory.Parent;
        }

        return Path.Combine(AppContext.BaseDirectory, "configs", "MotionConfig", "motion-config.json");
    }
}
