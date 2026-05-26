using MotionStudio.Motion.Abstractions;

namespace MotionStudio.Motion.Cards;

/// <summary>
/// Future adapter placeholder. Real vendor API is intentionally not implemented in current version.
/// </summary>
public sealed class FutureAcsMotionCard : IMotionCard
{
    public bool IsConnected { get; private set; }

    public Task<bool> InitAsync()
    {
        IsConnected = false;
        return Task.FromResult(false);
    }

    public Task<bool> CloseAsync()
    {
        IsConnected = false;
        return Task.FromResult(true);
    }

    public Task<bool> ServoOnAsync(int axisNo) => Task.FromResult(false);

    public Task<bool> ServoOffAsync(int axisNo) => Task.FromResult(false);

    public Task<bool> HomeAsync(int axisNo, double timeout) => Task.FromResult(false);

    public Task<bool> HomeAsync(int axisNo, HomeMotionOptions options, CancellationToken token = default) => Task.FromResult(false);

    public Task<bool> AbsMoveAsync(int axisNo, double position, double velocity, double acceleration, double deceleration, double smoothTime, double timeout, CancellationToken token) => Task.FromResult(false);

    public Task<bool> RelMoveAsync(int axisNo, double distance, double velocity, double acceleration, double deceleration, double smoothTime, double timeout, CancellationToken token) => Task.FromResult(false);

    public Task<bool> JogStartAsync(int axisNo, int direction, double velocity, double acceleration, double deceleration, double smoothTime, CancellationToken token = default) => Task.FromResult(false);

    public Task<bool> PrepareJogModeAsync(int axisNo) => Task.FromResult(false);

    public Task<bool> PrepareTrapModeAsync(int axisNo) => Task.FromResult(false);

    public Task<long?> GetPlannedPositionAsync(int axisNo, CancellationToken token = default) => Task.FromResult<long?>(null);

    public Task<bool> JogStopAsync(int axisNo) => Task.FromResult(false);

    public Task<bool> StopAxisAsync(int axisNo, bool emergency = false) => Task.FromResult(false);

    public Task<bool> StopAllAsync(bool emergency = false) => Task.FromResult(false);

    public Task<bool> ClearAlarmAsync(int axisNo, CancellationToken token = default) => Task.FromResult(false);

    public Task<bool> ClearAllAlarmAsync(CancellationToken token = default) => Task.FromResult(false);

    public Task<bool> ZeroPositionAsync(int axisNo, CancellationToken token = default) => Task.FromResult(false);

    public Task<bool> ResetControllerAsync(CancellationToken token = default) => Task.FromResult(false);

    public double GetAxisPosition(int axisNo) => 0;

    public AxisState GetAxisState(int axisNo) => new() { AxisNo = axisNo, Message = "Current real motion card driver is not enabled." };

    public bool GetDI(string name) => false;

    public bool GetDO(string name) => false;

    public bool SetDO(string name, bool value) => false;
}
