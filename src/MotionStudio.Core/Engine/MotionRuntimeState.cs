using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MotionStudio.Core.Engine;

/// <summary>
/// 流程运行时状态。
/// </summary>
public sealed class MotionRuntimeState : INotifyPropertyChanged
{
    private bool _isRunning;
    private bool _isStopRequested;
    private bool _isEmergencyStop;
    private bool _motionCardConnected;
    private string _currentModuleName = string.Empty;
    private int _totalCostTimeMs;
    private int _alarmCount;

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsRunning
    {
        get => _isRunning;
        set => SetField(ref _isRunning, value);
    }

    public bool IsStopRequested
    {
        get => _isStopRequested;
        set => SetField(ref _isStopRequested, value);
    }

    public bool IsEmergencyStop
    {
        get => _isEmergencyStop;
        set => SetField(ref _isEmergencyStop, value);
    }

    public bool MotionCardConnected
    {
        get => _motionCardConnected;
        set => SetField(ref _motionCardConnected, value);
    }

    public string CurrentModuleName
    {
        get => _currentModuleName;
        set => SetField(ref _currentModuleName, value);
    }

    public int TotalCostTimeMs
    {
        get => _totalCostTimeMs;
        set => SetField(ref _totalCostTimeMs, value);
    }

    public int AlarmCount
    {
        get => _alarmCount;
        set => SetField(ref _alarmCount, value);
    }

    public void ResetForRun()
    {
        IsStopRequested = false;
        IsEmergencyStop = false;
        CurrentModuleName = string.Empty;
        TotalCostTimeMs = 0;
        AlarmCount = 0;
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
