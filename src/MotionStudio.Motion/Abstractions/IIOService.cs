namespace MotionStudio.Motion.Abstractions;

/// <summary>
/// IO 服务抽象，预留给后续 IO 配置与监控页面。
/// </summary>
public interface IIOService
{
    IReadOnlyList<string> DiNames { get; }

    IReadOnlyList<string> DoNames { get; }

    bool GetDi(string name);

    bool GetDo(string name);
}
