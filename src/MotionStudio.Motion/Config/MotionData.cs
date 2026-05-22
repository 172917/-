using MotionStudio.Motion.Cards;

namespace MotionStudio.Motion.Config;

/// <summary>
/// 运动系统配置集合。
/// </summary>
public sealed class MotionData
{
    public List<MotionCardOptions> MotionCards { get; set; } = new()
    {
        new MotionCardOptions
        {
            CardName = "Sim-1",
            CardType = MotionCardType.Sim,
            Enabled = true,
            Description = "默认仿真运动卡"
        },
        new MotionCardOptions
        {
            CardName = "Googol-1",
            CardType = MotionCardType.Googol,
            Enabled = false,
            DllPath = "C:/Vendor/Googol/gts.dll",
            AxisBaseIndex = 0,
            IoBaseIndex = 0,
            Description = "固高运动卡示例配置（默认禁用）"
        },
        new MotionCardOptions
        {
            CardName = "ACS-1",
            CardType = MotionCardType.Acs,
            Enabled = false,
            DllPath = "C:/Vendor/ACS/ACSCL_x64.dll",
            AxisBaseIndex = 0,
            IoBaseIndex = 0,
            Description = "ACS 运动卡示例配置（默认禁用）"
        }
    };

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
