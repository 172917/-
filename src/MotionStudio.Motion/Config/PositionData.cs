namespace MotionStudio.Motion.Config;

/// <summary>
/// 点位数据。
/// </summary>
public sealed class PositionData
{
    public string Name { get; set; } = string.Empty;

    public Dictionary<string, double> AxisPositions { get; set; } = new();
}
