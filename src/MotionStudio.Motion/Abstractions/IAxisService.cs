namespace MotionStudio.Motion.Abstractions;

/// <summary>
/// 轴服务抽象，预留给后续更细粒度的轴管理层。
/// </summary>
public interface IAxisService
{
    IReadOnlyList<int> AxisNos { get; }

    AxisState GetState(int axisNo);
}
