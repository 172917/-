using MotionStudio.Motion.Abstractions;

namespace MotionStudio.Motion.Cards;

/// <summary>
/// 无硬件仿真运动卡，用于流程调试和 UI 闭环验证。
/// </summary>
public sealed class SimMotionCard : IMotionCard
{
    private readonly Dictionary<int, AxisState> _axes = new();
    private readonly Dictionary<string, bool> _di = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, bool> _do = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _syncRoot = new();

    public SimMotionCard()
    {
        foreach (var axisNo in new[] { 0, 1, 2, 3 })
        {
            _axes[axisNo] = new AxisState { AxisNo = axisNo, AxisName = $"Axis{axisNo}" };
        }

        _di["DI0"] = false;
        _di["DI1"] = false;
        _do["DO0"] = false;
        _do["DO1"] = false;
    }

    public bool IsConnected { get; private set; }

    public async Task<bool> InitAsync()
    {
        await Task.Delay(150).ConfigureAwait(false);
        IsConnected = true;
        return true;
    }

    public async Task<bool> CloseAsync()
    {
        await StopAllAsync(false).ConfigureAwait(false);
        IsConnected = false;
        return true;
    }

    public Task<bool> ServoOnAsync(int axisNo)
    {
        var axis = GetOrCreateAxis(axisNo);
        axis.ServoOn = true;
        axis.Message = "伺服已使能";
        return Task.FromResult(true);
    }

    public Task<bool> ServoOffAsync(int axisNo)
    {
        var axis = GetOrCreateAxis(axisNo);
        axis.ServoOn = false;
        axis.IsMoving = false;
        axis.Velocity = 0;
        axis.Message = "伺服已断使能";
        return Task.FromResult(true);
    }

    public async Task<bool> HomeAsync(int axisNo, double timeout)
    {
        var axis = GetOrCreateAxis(axisNo);
        if (!axis.ServoOn)
        {
            axis.Message = "轴未使能";
            return false;
        }

        axis.IsMoving = true;
        axis.Velocity = 1;
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(Math.Min(timeout * 1000, 500)), CancellationToken.None).ConfigureAwait(false);
            axis.Position = 0;
            axis.Homed = true;
            axis.Message = "回零完成";
        }
        finally
        {
            axis.IsMoving = false;
            axis.Velocity = 0;
        }

        return true;
    }

    public async Task<bool> AbsMoveAsync(int axisNo, double position, double velRatio, double timeout, CancellationToken token)
    {
        var axis = GetOrCreateAxis(axisNo);
        if (!CanMove(axis, out _))
        {
            return false;
        }

        axis.IsMoving = true;
        var duration = EstimateDuration(axis.Position, position, velRatio, timeout);
        axis.Velocity = EstimateVelocity(axis.Position, position, duration);
        try
        {
            await Task.Delay(duration, token).ConfigureAwait(false);
            axis.Position = position;
            axis.Message = $"绝对运动到 {position:F3}";
            return true;
        }
        finally
        {
            axis.IsMoving = false;
            axis.Velocity = 0;
        }
    }

    public Task<bool> RelMoveAsync(int axisNo, double distance, double velRatio, double timeout, CancellationToken token)
    {
        var current = GetAxisPosition(axisNo);
        return AbsMoveAsync(axisNo, current + distance, velRatio, timeout, token);
    }

    public Task<bool> StopAxisAsync(int axisNo, bool emergency = false)
    {
        var axis = GetOrCreateAxis(axisNo);
        axis.IsMoving = false;
        axis.Velocity = 0;
        axis.Message = emergency ? "轴急停" : "轴停止";
        return Task.FromResult(true);
    }

    public Task<bool> StopAllAsync(bool emergency = false)
    {
        lock (_syncRoot)
        {
            foreach (var axis in _axes.Values)
            {
                axis.IsMoving = false;
                axis.Velocity = 0;
                axis.Message = emergency ? "全部轴急停" : "全部轴停止";
            }
        }

        return Task.FromResult(true);
    }

    public double GetAxisPosition(int axisNo)
    {
        return GetOrCreateAxis(axisNo).Position;
    }

    public AxisState GetAxisState(int axisNo)
    {
        return GetOrCreateAxis(axisNo).Clone();
    }

    public bool GetDI(string name)
    {
        lock (_syncRoot)
        {
            return _di.TryGetValue(name, out var value) && value;
        }
    }

    public bool GetDO(string name)
    {
        lock (_syncRoot)
        {
            return _do.TryGetValue(name, out var value) && value;
        }
    }

    public bool SetDO(string name, bool value)
    {
        lock (_syncRoot)
        {
            _do[name] = value;
            if (name.StartsWith("DO", StringComparison.OrdinalIgnoreCase))
            {
                var diName = "DI" + name[2..];
                _di[diName] = value;
            }
        }

        return true;
    }

    private AxisState GetOrCreateAxis(int axisNo)
    {
        lock (_syncRoot)
        {
            if (!_axes.TryGetValue(axisNo, out var axis))
            {
                axis = new AxisState { AxisNo = axisNo, AxisName = $"Axis{axisNo}" };
                _axes[axisNo] = axis;
            }

            return axis;
        }
    }

    private static bool CanMove(AxisState axis, out string message)
    {
        if (!axis.ServoOn)
        {
            message = "轴未使能";
            axis.Message = message;
            return false;
        }

        if (axis.Alarm || axis.PositiveLimit || axis.NegativeLimit)
        {
            message = "轴报警或限位触发";
            axis.Message = message;
            return false;
        }

        message = string.Empty;
        return true;
    }

    private static TimeSpan EstimateDuration(double start, double target, double velRatio, double timeout)
    {
        var distance = Math.Abs(target - start);
        var ratio = Math.Clamp(velRatio, 0.05, 1);
        var milliseconds = Math.Clamp(distance * 20 / ratio, 120, timeout * 1000);
        return TimeSpan.FromMilliseconds(milliseconds);
    }

    private static double EstimateVelocity(double start, double target, TimeSpan duration)
    {
        if (duration.TotalSeconds <= 0)
        {
            return 0;
        }

        return Math.Abs(target - start) / duration.TotalSeconds;
    }
}
