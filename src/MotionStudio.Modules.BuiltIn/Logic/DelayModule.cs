using System.ComponentModel;
using MotionStudio.Core.Modules;
using MotionStudio.Core.Plugins;

namespace MotionStudio.Modules.BuiltIn.Logic;

/// <summary>
/// 异步延时模块。
/// </summary>
[Category("逻辑控制")]
[DisplayName("延时")]
[Description("等待指定毫秒数。")]
[MotionModuleIcon("Delay")]
public sealed class DelayModule : MotionModuleBase
{
    private int _delayMs = 500;

    [Category("逻辑参数")]
    [DisplayName("延时(ms)")]
    public int DelayMs
    {
        get => _delayMs;
        set => SetModuleProperty(ref _delayMs, value);
    }

    public override async Task<ModuleResult> ExecuteAsync(MotionContext context, CancellationToken token)
    {
        if (DelayMs < 0)
        {
            return ModuleResult.Fail("延时不能小于 0");
        }

        await Task.Delay(DelayMs, token).ConfigureAwait(false);
        return ModuleResult.Ok("延时完成");
    }
}
