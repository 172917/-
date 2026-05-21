using System.ComponentModel;
using MotionStudio.Core.Modules;
using MotionStudio.Core.Plugins;

namespace MotionStudio.Plugins.Demo;

/// <summary>
/// 第三方插件示例模块，演示外部 DLL 插件加载。
/// </summary>
[Category("工艺模块")]
[DisplayName("示例工艺")]
[Description("外部插件示例：等待指定时间并返回成功。")]
[MotionModuleIcon("Demo")]
public sealed class SampleProcessModule : MotionModuleBase
{
    private int _workTimeMs = 300;

    [Category("工艺参数")]
    [DisplayName("处理时间(ms)")]
    public int WorkTimeMs
    {
        get => _workTimeMs;
        set => SetModuleProperty(ref _workTimeMs, value);
    }

    public override async Task<ModuleResult> ExecuteAsync(MotionContext context, CancellationToken token)
    {
        if (WorkTimeMs < 0)
        {
            return ModuleResult.Fail("处理时间不能小于 0");
        }

        await Task.Delay(WorkTimeMs, token).ConfigureAwait(false);
        return ModuleResult.Ok("示例工艺完成");
    }
}
