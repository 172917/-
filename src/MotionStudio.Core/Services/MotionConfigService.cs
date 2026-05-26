using System.Text.Json;
using System.Text.Json.Serialization;
using MotionStudio.Motion.Config;
using MotionStudio.Motion.Cards;

namespace MotionStudio.Core.Services;

/// <summary>
/// Unified motion configuration service used by runtime and debug UI.
/// </summary>
public sealed class MotionConfigService
{
    private readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly object _syncRoot = new();
    private MotionData _data = new();

    public MotionConfigService()
    {
        ConfigFilePath = ResolveConfigFilePath();
        Reload();
    }

    public string ConfigFilePath { get; }

    public MotionData LoadOrCreate()
    {
        lock (_syncRoot)
        {
            return CloneMotionData(_data);
        }
    }

    public void Save(MotionData data)
    {
        var target = data ?? CreateDefaultData();
        var directory = Path.GetDirectoryName(ConfigFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(target, _options);
        File.WriteAllText(ConfigFilePath, json);
        lock (_syncRoot)
        {
            _data = Normalize(target);
        }
    }

    public void Reload()
    {
        MotionData loaded;
        if (!File.Exists(ConfigFilePath))
        {
            loaded = CreateDefaultData();
            Save(loaded);
            return;
        }

        try
        {
            var json = File.ReadAllText(ConfigFilePath);
            loaded = JsonSerializer.Deserialize<MotionData>(json, _options) ?? CreateDefaultData();
        }
        catch
        {
            loaded = CreateDefaultData();
        }

        lock (_syncRoot)
        {
            _data = Normalize(loaded);
        }
    }

    public AxisBaseConfig? GetAxisByName(string axisName)
    {
        if (string.IsNullOrWhiteSpace(axisName))
        {
            return null;
        }

        lock (_syncRoot)
        {
            var axis = _data.Axes.FirstOrDefault(item =>
                item.AxisName.Equals(axisName, StringComparison.OrdinalIgnoreCase));
            return axis is null ? null : CloneAxis(axis);
        }
    }

    public AxisBaseConfig? GetAxisByNo(int axisNo)
    {
        lock (_syncRoot)
        {
            var axis = _data.Axes.FirstOrDefault(item => item.AxisNo == axisNo);
            return axis is null ? null : CloneAxis(axis);
        }
    }

    public IOConfig? GetInputByName(string name)
    {
        return GetIoByName(name, isOutput: false);
    }

    public IOConfig? GetOutputByName(string name)
    {
        return GetIoByName(name, isOutput: true);
    }

    private IOConfig? GetIoByName(string name, bool isOutput)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        lock (_syncRoot)
        {
            var io = _data.IOs.FirstOrDefault(item =>
                item.IsOutput == isOutput
                && item.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            return io is null ? null : CloneIo(io);
        }
    }

    private static MotionData Normalize(MotionData data)
    {
        data.Axes ??= new List<AxisBaseConfig>();
        data.IOs ??= new List<IOConfig>();
        data.Coordinates ??= new List<CoordinateConfig>();
        data.Positions ??= new List<PositionData>();
        data.MotionCards ??= new List<MotionCardOptions>();

        if (data.MotionCards.Count == 0)
        {
            data.MotionCards.AddRange(CreateDefaultData().MotionCards);
        }

        if (data.Axes.Count == 0)
        {
            data.Axes.AddRange(CreateDefaultData().Axes);
        }

        if (data.IOs.Count == 0)
        {
            data.IOs.AddRange(CreateDefaultData().IOs);
        }

        return data;
    }

    private static MotionData CreateDefaultData()
    {
        return new MotionData();
    }

    private static MotionData CloneMotionData(MotionData data)
    {
        return new MotionData
        {
            MotionCards = data.MotionCards.Select(item => new MotionCardOptions
            {
                CardName = item.CardName,
                CardType = item.CardType,
                Enabled = item.Enabled,
                DllPath = item.DllPath,
                ConfigFilePath = item.ConfigFilePath,
                AxisBaseIndex = item.AxisBaseIndex,
                IoBaseIndex = item.IoBaseIndex,
                Description = item.Description
            }).ToList(),
            Axes = data.Axes.Select(CloneAxis).ToList(),
            IOs = data.IOs.Select(CloneIo).ToList(),
            Coordinates = data.Coordinates.Select(item => new CoordinateConfig
            {
                CoordinateName = item.CoordinateName,
                AxisNames = item.AxisNames.ToArray()
            }).ToList(),
            Positions = data.Positions.Select(item => new PositionData
            {
                Name = item.Name,
                AxisPositions = new Dictionary<string, double>(item.AxisPositions)
            }).ToList()
        };
    }

    private static AxisBaseConfig CloneAxis(AxisBaseConfig item)
    {
        return new AxisBaseConfig
        {
            AxisName = item.AxisName,
            AxisNo = item.AxisNo,
            MotionCardName = item.MotionCardName,
            VelocityRatio = item.VelocityRatio,
            TargetPosition = item.TargetPosition,
            RelativeDistance = item.RelativeDistance,
            HomeTimeout = item.HomeTimeout,
            AbsVelocity = item.AbsVelocity,
            AbsAcceleration = item.AbsAcceleration,
            AbsDeceleration = item.AbsDeceleration,
            RelVelocity = item.RelVelocity,
            RelAcceleration = item.RelAcceleration,
            RelDeceleration = item.RelDeceleration,
            HomeVelocity = item.HomeVelocity,
            HomeLowVelocity = item.HomeLowVelocity,
            HomeAcceleration = item.HomeAcceleration,
            HomeDeceleration = item.HomeDeceleration,
            HomeMode = item.HomeMode,
            HomeSearchDirection = item.HomeSearchDirection,
            HomeEncoderDirection = item.HomeEncoderDirection,
            HomePositiveLimitTriggerLevel = item.HomePositiveLimitTriggerLevel,
            HomeNegativeLimitTriggerLevel = item.HomeNegativeLimitTriggerLevel,
            HomeCaptureEdge = item.HomeCaptureEdge,
            HomeSearchDistance = item.HomeSearchDistance,
            HomeEscapeStep = item.HomeEscapeStep
        };
    }

    private static IOConfig CloneIo(IOConfig item)
    {
        return new IOConfig
        {
            Name = item.Name,
            CardIndex = item.CardIndex,
            PointNo = item.PointNo,
            IsOutput = item.IsOutput,
            MotionCardName = item.MotionCardName
        };
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
