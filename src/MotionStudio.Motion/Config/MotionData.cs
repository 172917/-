namespace MotionStudio.Motion.Config;

/// <summary>
/// 运动系统配置集合。
/// </summary>
public sealed class MotionData
{
    /// <summary>
    /// 固高控制卡 core 参数，默认 1。
    /// </summary>
    public short GoogolCore { get; set; } = 1;

    public List<AxisBaseConfig> Axes { get; set; } = new()
    {
        new AxisBaseConfig { AxisName = "X", AxisNo = 0 },
        new AxisBaseConfig { AxisName = "Y", AxisNo = 1 },
        new AxisBaseConfig { AxisName = "Z", AxisNo = 2 }
    };

    public List<IOConfig> IOs { get; set; } = new()
    {
        new IOConfig { Name = "DI0", PointNo = 0, IsOutput = false },
        new IOConfig { Name = "DI1", PointNo = 1, IsOutput = false },
        new IOConfig { Name = "DO0", PointNo = 0, IsOutput = true },
        new IOConfig { Name = "DO1", PointNo = 1, IsOutput = true }
    };

    public List<CoordinateConfig> Coordinates { get; set; } = new();

    public List<PositionData> Positions { get; set; } = new();
}
