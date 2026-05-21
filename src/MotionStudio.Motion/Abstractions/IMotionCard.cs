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

    Task<bool> AbsMoveAsync(int axisNo, double position, double velRatio, double timeout, CancellationToken token);

    Task<bool> RelMoveAsync(int axisNo, double distance, double velRatio, double timeout, CancellationToken token);

    Task<bool> StopAxisAsync(int axisNo, bool emergency = false);

    Task<bool> StopAllAsync(bool emergency = false);

    double GetAxisPosition(int axisNo);

    AxisState GetAxisState(int axisNo);

    bool GetDI(string name);

    bool GetDO(string name);

    bool SetDO(string name, bool value);
}
