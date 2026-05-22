namespace MotionStudio.Motion.Config;

/// <summary>
/// IO 点位配置。
/// </summary>
public sealed class IOConfig
{
    public string Name { get; set; } = string.Empty;

    public string MotionCardName { get; set; } = string.Empty;

    public int CardIndex { get; set; }

    public int PointNo { get; set; }

    public bool IsOutput { get; set; }
}
