using MotionStudio.Motion.Abstractions;

namespace MotionStudio.Motion.Cards;

/// <summary>
/// ACS 运动卡适配占位。真实实现应封装厂商 API，不能被流程模块直接引用。
/// </summary>
public sealed class AcsMotionCard : IMotionCard
{
    private readonly SimMotionCard _inner = new();

    public bool IsConnected => _inner.IsConnected;

    public Task<bool> InitAsync() => _inner.InitAsync();

    public Task<bool> CloseAsync() => _inner.CloseAsync();

    public Task<bool> ServoOnAsync(int axisNo) => _inner.ServoOnAsync(axisNo);

    public Task<bool> ServoOffAsync(int axisNo) => _inner.ServoOffAsync(axisNo);

    public Task<bool> HomeAsync(int axisNo, double timeout) => _inner.HomeAsync(axisNo, timeout);

    public Task<bool> AbsMoveAsync(int axisNo, double position, double velRatio, double timeout, CancellationToken token) =>
        _inner.AbsMoveAsync(axisNo, position, velRatio, timeout, token);

    public Task<bool> RelMoveAsync(int axisNo, double distance, double velRatio, double timeout, CancellationToken token) =>
        _inner.RelMoveAsync(axisNo, distance, velRatio, timeout, token);

    public Task<bool> StopAxisAsync(int axisNo, bool emergency = false) => _inner.StopAxisAsync(axisNo, emergency);

    public Task<bool> StopAllAsync(bool emergency = false) => _inner.StopAllAsync(emergency);

    public double GetAxisPosition(int axisNo) => _inner.GetAxisPosition(axisNo);

    public AxisState GetAxisState(int axisNo) => _inner.GetAxisState(axisNo);

    public bool GetDI(string name) => _inner.GetDI(name);

    public bool GetDO(string name) => _inner.GetDO(name);

    public bool SetDO(string name, bool value) => _inner.SetDO(name, value);
}
