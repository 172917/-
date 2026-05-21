namespace MotionStudio.Motion.Abstractions;

/// <summary>
/// 轴运行状态快照。
/// </summary>
public sealed class AxisState
{
    public int AxisNo { get; set; }

    public string AxisName { get; set; } = string.Empty;

    public bool ServoOn { get; set; }

    public bool Homed { get; set; }

    public bool IsMoving { get; set; }

    public bool Alarm { get; set; }

    public bool PositiveLimit { get; set; }

    public bool NegativeLimit { get; set; }

    public double Position { get; set; }

    public double Velocity { get; set; }

    public string Message { get; set; } = string.Empty;

    public AxisState Clone()
    {
        return (AxisState)MemberwiseClone();
    }
}
