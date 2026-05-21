using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MotionStudio.Motion.Config;

/// <summary>
/// 轴基础配置。
/// </summary>
public sealed class AxisBaseConfig : INotifyPropertyChanged
{
    private string _axisName = "X";
    private int _axisNo;
    private string _motionCardName = "Sim-1";
    private double _velocityRatio = 0.5;
    private double _targetPosition = 10;
    private double _relativeDistance = 1;
    private double _homeTimeout = 30;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string AxisName
    {
        get => _axisName;
        set => SetField(ref _axisName, string.IsNullOrWhiteSpace(value) ? "X" : value);
    }

    public int AxisNo
    {
        get => _axisNo;
        set => SetField(ref _axisNo, value);
    }

    public string MotionCardName
    {
        get => _motionCardName;
        set => SetField(ref _motionCardName, string.IsNullOrWhiteSpace(value) ? "Sim-1" : value);
    }

    public double VelocityRatio
    {
        get => _velocityRatio;
        set => SetField(ref _velocityRatio, Math.Clamp(value, 0.01, 1));
    }

    public double TargetPosition
    {
        get => _targetPosition;
        set => SetField(ref _targetPosition, value);
    }

    public double RelativeDistance
    {
        get => _relativeDistance;
        set => SetField(ref _relativeDistance, value);
    }

    public double HomeTimeout
    {
        get => _homeTimeout;
        set => SetField(ref _homeTimeout, value <= 0 ? 1 : value);
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
