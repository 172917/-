namespace MotionStudio.Motion.Abstractions;

/// <summary>
/// 统一运动控制卡接口，流程模块只能依赖该抽象。
/// </summary>
public interface IMotionCard
{
    bool IsConnected { get; }

    Task<bool> InitAsync();

    Task<bool> CloseAsync();

    Task<bool> ServoOnAsync(int axisNo);

    Task<bool> ServoOffAsync(int axisNo);

    Task<bool> HomeAsync(int axisNo, double timeout);

    Task<bool> AbsMoveAsync(
        int axisNo,
        double position,
        double velocity,
        double acceleration,
        double deceleration,
        double smoothTime,
        double timeout,
        CancellationToken token);

    Task<bool> RelMoveAsync(
        int axisNo,
        double distance,
        double velocity,
        double acceleration,
        double deceleration,
        double smoothTime,
        double timeout,
        CancellationToken token);

    Task<bool> JogStartAsync(
        int axisNo,
        int direction,
        double velocity,
        double acceleration,
        double deceleration,
        double smoothTime,
        CancellationToken token = default);

    Task<bool> PrepareJogModeAsync(int axisNo);

    Task<bool> PrepareTrapModeAsync(int axisNo);

    Task<long?> GetPlannedPositionAsync(int axisNo, CancellationToken token = default);

    Task<bool> JogStopAsync(int axisNo);

    Task<bool> StopAxisAsync(int axisNo, bool emergency = false);

    Task<bool> StopAllAsync(bool emergency = false);

    Task<bool> ClearAlarmAsync(int axisNo, CancellationToken token = default);

    Task<bool> ClearAllAlarmAsync(CancellationToken token = default);

    Task<bool> ZeroPositionAsync(int axisNo, CancellationToken token = default);

    Task<bool> ResetControllerAsync(CancellationToken token = default);

    double GetAxisPosition(int axisNo);

    AxisState GetAxisState(int axisNo);

    bool GetDI(string name);

    bool GetDO(string name);

    bool SetDO(string name, bool value);
}
