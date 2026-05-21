namespace MotionStudio.Motion.Config;

/// <summary>
/// 坐标系配置占位，后续用于多轴插补和工艺坐标转换。
/// </summary>
public sealed class CoordinateConfig
{
    public string CoordinateName { get; set; } = "默认坐标系";

    public string[] AxisNames { get; set; } = ["X", "Y"];
}
