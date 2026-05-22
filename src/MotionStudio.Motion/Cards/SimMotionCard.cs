using MotionStudio.Motion.Abstractions;

namespace MotionStudio.Motion.Cards;

public sealed class SimMotionCard : IMotionCard
{
    private readonly Dictionary<int, AxisState> _axes = new();
    private readonly Dictionary<int, DateTime> _axisSampleTime = new();
    private readonly Dictionary<string, bool> _di = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, bool> _do = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _syncRoot = new();

    public SimMotionCard()
    {
        foreach (var axisNo in new[] { 0, 1, 2, 3 })
        {
            _axes[axisNo] = new AxisState { AxisNo = axisNo, AxisName = $"Axis{axisNo}" };
            _axisSampleTime[axisNo] = DateTime.UtcNow;
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

    public async Task<bool> AbsMoveAsync(
        int axisNo,
        double position,
        double velocity,
        double acceleration,
        double deceleration,
        double smoothTime,
        double timeout,
        CancellationToken token)
    {
        var axis = GetOrCreateAxis(axisNo);
        if (!CanMove(axis, out _))
        {
            return false;
        }

        axis.IsMoving = true;
        var velRatio = Math.Clamp(velocity / 100d, 0.05, 1);
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

    public Task<bool> RelMoveAsync(
        int axisNo,
        double distance,
        double velocity,
        double acceleration,
        double deceleration,
        double smoothTime,
        double timeout,
        CancellationToken token)
    {
        var current = GetAxisPosition(axisNo);
        return AbsMoveAsync(axisNo, current + distance, velocity, acceleration, deceleration, smoothTime, timeout, token);
    }

    public Task<bool> JogStartAsync(
        int axisNo,
        int direction,
        double velocity,
        double acceleration,
        double deceleration,
        double smoothTime,
        CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        var axis = GetOrCreateAxis(axisNo);
        if (!CanMove(axis, out _))
        {
            return Task.FromResult(false);
        }

        var sign = direction >= 0 ? 1 : -1;
        UpdateAxisMotionState(axis);
        axis.IsMoving = true;
        axis.Velocity = sign * Math.Abs(velocity);
        axis.Message = $"Jog启动 dir={sign}, vel={Math.Abs(velocity):F3}";
        _axisSampleTime[axisNo] = DateTime.UtcNow;
        return Task.FromResult(true);
    }

    public Task<bool> PrepareJogModeAsync(int axisNo)
    {
        var axis = GetOrCreateAxis(axisNo);
        axis.Message = "Jog模式已准备";
        return Task.FromResult(true);
    }

    public Task<bool> PrepareTrapModeAsync(int axisNo)
    {
        var axis = GetOrCreateAxis(axisNo);
        axis.Message = "Trap模式已准备";
        return Task.FromResult(true);
    }

    public Task<long?> GetPlannedPositionAsync(int axisNo, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        var axis = GetOrCreateAxis(axisNo);
        return Task.FromResult<long?>((long)Math.Round(axis.Position, MidpointRounding.AwayFromZero));
    }

    public Task<bool> StopAxisAsync(int axisNo, bool emergency = false)
    {
        var axis = GetOrCreateAxis(axisNo);
        UpdateAxisMotionState(axis);
        axis.IsMoving = false;
        axis.Velocity = 0;
        axis.Message = emergency ? "轴急停" : "轴停止";
        _axisSampleTime[axisNo] = DateTime.UtcNow;
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

    public Task<bool> ClearAlarmAsync(int axisNo, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        lock (_syncRoot)
        {
            if (!_axes.TryGetValue(axisNo, out var axis))
            {
                return Task.FromResult(false);
            }

            axis.Alarm = false;
            axis.PositiveLimit = false;
            axis.NegativeLimit = false;
            axis.Message = "轴报警已复位";
            return Task.FromResult(true);
        }
    }

    public Task<bool> ClearAllAlarmAsync(CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        lock (_syncRoot)
        {
            foreach (var axis in _axes.Values)
            {
                axis.Alarm = false;
                axis.PositiveLimit = false;
                axis.NegativeLimit = false;
                axis.Message = "全部轴报警已复位";
            }
        }

        return Task.FromResult(true);
    }

    public Task<bool> ZeroPositionAsync(int axisNo, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        lock (_syncRoot)
        {
            if (!_axes.TryGetValue(axisNo, out var axis))
            {
                return Task.FromResult(false);
            }

            axis.Position = 0;
            axis.IsMoving = false;
            axis.Velocity = 0;
            axis.Message = "轴位置已清零";
            _axisSampleTime[axisNo] = DateTime.UtcNow;
            return Task.FromResult(true);
        }
    }

    public Task<bool> ResetControllerAsync(CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        lock (_syncRoot)
        {
            foreach (var axis in _axes.Values)
            {
                axis.Position = 0;
                axis.IsMoving = false;
                axis.Velocity = 0;
                axis.Message = "全部轴位置已清零";
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
        var axis = GetOrCreateAxis(axisNo);
        UpdateAxisMotionState(axis);
        return axis.Clone();
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
                _axisSampleTime[axisNo] = DateTime.UtcNow;
            }

            return axis;
        }
    }

    private void UpdateAxisMotionState(AxisState axis)
    {
        if (!_axisSampleTime.TryGetValue(axis.AxisNo, out var lastTime))
        {
            _axisSampleTime[axis.AxisNo] = DateTime.UtcNow;
            return;
        }

        var now = DateTime.UtcNow;
        var seconds = (now - lastTime).TotalSeconds;
        if (seconds > 0 && axis.IsMoving)
        {
            axis.Position += axis.Velocity * seconds;
        }

        _axisSampleTime[axis.AxisNo] = now;
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
