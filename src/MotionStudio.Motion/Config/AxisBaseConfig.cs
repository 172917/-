using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MotionStudio.Motion.Config;

/// <summary>
/// 轴基础配置。
/// </summary>
public sealed class AxisBaseConfig : INotifyPropertyChanged
{
    private string _axisName = "X";
    private int _axisNo = 1;
    private string _motionCardName = "Sim-1";
    private double _velocityRatio = 0.5;
    private double _targetPosition = 10;
    private double _relativeDistance = 1;
    private double _homeTimeout = 30;
    private double _absVelocity = 50;
    private double _absAcceleration = 100;
    private double _absDeceleration = 100;
    private double _relVelocity = 50;
    private double _relAcceleration = 100;
    private double _relDeceleration = 100;
    private double _homeVelocity = 30;
    private double _homeAcceleration = 80;
    private double _homeDeceleration = 80;

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
        set
        {
            var next = string.IsNullOrWhiteSpace(value) ? _motionCardName : value.Trim();
            if (string.IsNullOrWhiteSpace(next))
            {
                next = "Sim-1";
            }

            SetField(ref _motionCardName, next);
        }
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

    public double AbsVelocity
    {
        get => _absVelocity;
        set => SetField(ref _absVelocity, NormalizePositive(value, 50));
    }

    public double AbsAcceleration
    {
        get => _absAcceleration;
        set => SetField(ref _absAcceleration, NormalizePositive(value, 100));
    }

    public double AbsDeceleration
    {
        get => _absDeceleration;
        set => SetField(ref _absDeceleration, NormalizePositive(value, 100));
    }

    public double RelVelocity
    {
        get => _relVelocity;
        set => SetField(ref _relVelocity, NormalizePositive(value, 50));
    }

    public double RelAcceleration
    {
        get => _relAcceleration;
        set => SetField(ref _relAcceleration, NormalizePositive(value, 100));
    }

    public double RelDeceleration
    {
        get => _relDeceleration;
        set => SetField(ref _relDeceleration, NormalizePositive(value, 100));
    }

    public double HomeVelocity
    {
        get => _homeVelocity;
        set => SetField(ref _homeVelocity, NormalizePositive(value, 30));
    }

    public double HomeAcceleration
    {
        get => _homeAcceleration;
        set => SetField(ref _homeAcceleration, NormalizePositive(value, 80));
    }

    public double HomeDeceleration
    {
        get => _homeDeceleration;
        set => SetField(ref _homeDeceleration, NormalizePositive(value, 80));
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

    private static double NormalizePositive(double value, double fallback)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
        {
            return fallback;
        }

        return value;
    }
}
