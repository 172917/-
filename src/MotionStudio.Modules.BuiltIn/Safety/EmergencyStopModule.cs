using System.ComponentModel;
using MotionStudio.Core.Modules;
using MotionStudio.Core.Plugins;

namespace MotionStudio.Modules.BuiltIn.Safety;

/// <summary>
/// 急停模块。
/// </summary>
[Category("安全模块")]
[DisplayName("急停")]
[Description("立即急停所有注册运动卡并置急停状态。")]
[MotionModuleIcon("ESTOP")]
public sealed class EmergencyStopModule : MotionModuleBase
{
    public override bool BypassAxisFaultPreCheck => true;

    public override async Task<ModuleResult> ExecuteAsync(MotionContext context, CancellationToken token)
    {
        context.RuntimeState.IsEmergencyStop = true;
        await context.StopAllMotionCardsAsync(true).ConfigureAwait(false);
        return ModuleResult.Fail("急停模块已触发");
    }
}
