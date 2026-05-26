using MotionStudio.Motion.Abstractions;

namespace MotionStudio.Motion.Cards;

public sealed class FutureGoogolMotionCard : IMotionCard, IApiTraceProvider
{
    private readonly GoogolMotionCard _inner = new();

    public bool IsConnected => _inner.IsConnected;

    public void BeginApiTraceScope() => _inner.BeginApiTraceScope();

    public string ConsumeApiTrace() => _inner.ConsumeApiTrace();

    public Task<bool> InitAsync() => _inner.InitAsync();

    public Task<bool> CloseAsync() => _inner.CloseAsync();

    public Task<bool> ServoOnAsync(int axisNo) => _inner.ServoOnAsync(axisNo);

    public Task<bool> ServoOffAsync(int axisNo) => _inner.ServoOffAsync(axisNo);

    public Task<bool> HomeAsync(int axisNo, double timeout) => _inner.HomeAsync(axisNo, timeout);

    public Task<bool> HomeAsync(int axisNo, HomeMotionOptions options, CancellationToken token = default) => _inner.HomeAsync(axisNo, options, token);

    public Task<bool> AbsMoveAsync(int axisNo, double position, double velocity, double acceleration, double deceleration, double smoothTime, double timeout, CancellationToken token) => _inner.AbsMoveAsync(axisNo, position, velocity, acceleration, deceleration, smoothTime, timeout, token);

    public Task<bool> RelMoveAsync(int axisNo, double distance, double velocity, double acceleration, double deceleration, double smoothTime, double timeout, CancellationToken token) => _inner.RelMoveAsync(axisNo, distance, velocity, acceleration, deceleration, smoothTime, timeout, token);

    public Task<bool> JogStartAsync(int axisNo, int direction, double velocity, double acceleration, double deceleration, double smoothTime, CancellationToken token = default) => _inner.JogStartAsync(axisNo, direction, velocity, acceleration, deceleration, smoothTime, token);

    public Task<bool> PrepareJogModeAsync(int axisNo) => _inner.PrepareJogModeAsync(axisNo);

    public Task<bool> PrepareTrapModeAsync(int axisNo) => _inner.PrepareTrapModeAsync(axisNo);

    public Task<long?> GetPlannedPositionAsync(int axisNo, CancellationToken token = default) => _inner.GetPlannedPositionAsync(axisNo, token);

    public Task<bool> JogStopAsync(int axisNo) => _inner.JogStopAsync(axisNo);

    public Task<bool> StopAxisAsync(int axisNo, bool emergency = false) => _inner.StopAxisAsync(axisNo, emergency);

    public Task<bool> StopAllAsync(bool emergency = false) => _inner.StopAllAsync(emergency);

    public Task<bool> ClearAlarmAsync(int axisNo, CancellationToken token = default) => _inner.ClearAlarmAsync(axisNo, token);

    public Task<bool> ClearAllAlarmAsync(CancellationToken token = default) => _inner.ClearAllAlarmAsync(token);

    public Task<bool> ZeroPositionAsync(int axisNo, CancellationToken token = default) => _inner.ZeroPositionAsync(axisNo, token);

    public Task<bool> ResetControllerAsync(CancellationToken token = default) => _inner.ResetControllerAsync(token);

    public double GetAxisPosition(int axisNo) => _inner.GetAxisPosition(axisNo);

    public AxisState GetAxisState(int axisNo) => _inner.GetAxisState(axisNo);

    public bool GetDI(string name) => _inner.GetDI(name);

    public bool GetDO(string name) => _inner.GetDO(name);

    public bool SetDO(string name, bool value) => _inner.SetDO(name, value);
}
